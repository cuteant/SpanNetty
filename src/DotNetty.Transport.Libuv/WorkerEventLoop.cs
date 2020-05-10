// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    sealed class WorkerEventLoop : LoopExecutor, IEventLoop
    {
        readonly IPromise connectCompletion;
        readonly string pipeName;
        Pipe pipe;

        public WorkerEventLoop(WorkerEventLoopGroup parent)
            : base(parent, null)
        {
            if (null == parent) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.parent); }

            string name = parent.PipeName;
            if (string.IsNullOrEmpty(name))
            {
                ThrowHelper.ThrowArgumentException_PipeName();
            }

            this.pipeName = name;
            this.connectCompletion = this.NewPromise();
            this.Start();
        }

        /// <summary>
        /// Awaitable for connecting to the dispatcher pipe.
        /// </summary>
        internal Task ConnectTask => this.connectCompletion.Task;

        protected override void Initialize()
        {
            Debug.Assert(this.pipe == null);

            this.pipe = new Pipe(this.UnsafeLoop, true);
            PipeConnect request = null;
            try
            {
                request = new PipeConnect(this);
            }
            catch (Exception exception)
            {
                if (Logger.WarnEnabled) Logger.FailedToCreateConnectRequestToDispatcher(exception);
                request?.Dispose();
                this.connectCompletion.TrySetException(exception);
            }
        }

        protected override void Release() => this.pipe.CloseHandle();

        void OnConnected(ConnectRequest request)
        {
            try
            {
                if (request.Error is object)
                {
                    if (Logger.WarnEnabled) Logger.FailedToConnectToDispatcher(request);
                    this.connectCompletion.TrySetException(request.Error);
                }
                else
                {
                    if (Logger.InfoEnabled)
                    {
                        Logger.DispatcherPipeConnected(this.LoopThreadId, this.pipeName);
                    }

                    this.pipe.ReadStart(this.OnRead);
                    this.connectCompletion.TryComplete();
                }
            }
            finally
            {
                request.Dispose();
            }
        }

        void OnRead(Pipe handle, int status)
        {
            if (status < 0)
            {
                handle.CloseHandle();
                if (status != NativeMethods.EOF)
                {
                    OperationException error = NativeMethods.CreateError((uv_err_code)status);
                    if (Logger.WarnEnabled) Logger.IPCPipeReadError(error);
                }
            }
            else
            {
                Tcp tcp = handle.GetPendingHandle();
                ((WorkerEventLoopGroup)this.Parent).Accept(tcp);
            }
        }

        public new IEventLoop GetNext() => (IEventLoop)base.GetNext();

        public Task RegisterAsync(IChannel channel) => channel.Unsafe.RegisterAsync(this);

        public new IEventLoopGroup Parent => (IEventLoopGroup)base.Parent;

        IEnumerable<IEventLoop> IEventLoopGroup.Items => new[] { this };

        sealed class PipeConnect : ConnectRequest
        {
            const int MaximumRetryCount = 10;

            readonly WorkerEventLoop workerEventLoop;
            int retryCount;

            public PipeConnect(WorkerEventLoop workerEventLoop)
            {
                Debug.Assert(workerEventLoop is object);

                this.workerEventLoop = workerEventLoop;
                this.Connect();
                this.retryCount = 0;
            }

            protected override void OnWatcherCallback()
            {
                if (this.Error is object && this.retryCount < MaximumRetryCount)
                {
                    if (Logger.InfoEnabled) Logger.FailedToConnectToDispatcher(this.retryCount, this.Error);
                    this.Connect();
                    this.retryCount++;
                }
                else
                {
                    this.workerEventLoop.OnConnected(this);
                }
            }

            void Connect() => NativeMethods.uv_pipe_connect(
                this.Handle,
                this.workerEventLoop.pipe.Handle,
                this.workerEventLoop.pipeName,
                WatcherCallback);
        }
    }
}