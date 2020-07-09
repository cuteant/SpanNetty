// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Libuv.Native;

    public sealed class WorkerEventLoop : LoopExecutor
    {
        private readonly IPromise _connectCompletion;
        private readonly string _pipeName;
        private Pipe _pipe;

        internal WorkerEventLoop(WorkerEventLoopGroup parent, IThreadFactory threadFactory, IRejectedExecutionHandler rejectedHandler, TimeSpan breakoutInterval)
            : base(parent, threadFactory, rejectedHandler, breakoutInterval)
        {
            if (parent is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.parent); }

            string name = parent.PipeName;
            if (string.IsNullOrEmpty(name))
            {
                ThrowHelper.ThrowArgumentException_PipeName();
            }

            _pipeName = name;
            _connectCompletion = NewPromise();
            Start();
        }

        /// <summary>
        /// Awaitable for connecting to the dispatcher pipe.
        /// </summary>
        internal Task ConnectTask => _connectCompletion.Task;

        protected override void Initialize()
        {
            Debug.Assert(_pipe is null);

            _pipe = new Pipe(UnsafeLoop, true);
            PipeConnect request = null;
            try
            {
                request = new PipeConnect(this);
            }
            catch (Exception exception)
            {
                if (Logger.WarnEnabled) Logger.FailedToCreateConnectRequestToDispatcher(exception);
                request?.Dispose();
                _ = _connectCompletion.TrySetException(exception);
            }
        }

        protected override void Release() => _pipe.CloseHandle();

        void OnConnected(ConnectRequest request)
        {
            try
            {
                if (request.Error is object)
                {
                    if (Logger.WarnEnabled) Logger.FailedToConnectToDispatcher(request);
                    _ = _connectCompletion.TrySetException(request.Error);
                }
                else
                {
                    if (Logger.InfoEnabled)
                    {
                        Logger.DispatcherPipeConnected(LoopThreadId, _pipeName);
                    }

                    _pipe.ReadStart(OnRead);
                    _ = _connectCompletion.TryComplete();
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
                ((WorkerEventLoopGroup)Parent).Accept(tcp);
            }
        }

        sealed class PipeConnect : ConnectRequest
        {
            const int MaximumRetryCount = 10;

            readonly WorkerEventLoop _workerEventLoop;
            int _retryCount;

            public PipeConnect(WorkerEventLoop workerEventLoop)
            {
                Debug.Assert(workerEventLoop is object);

                _workerEventLoop = workerEventLoop;
                Connect();
                _retryCount = 0;
            }

            protected override void OnWatcherCallback()
            {
                if (Error is object && _retryCount < MaximumRetryCount)
                {
                    if (Logger.InfoEnabled) Logger.FailedToConnectToDispatcher(_retryCount, Error);
                    Connect();
                    _retryCount++;
                }
                else
                {
                    _workerEventLoop.OnConnected(this);
                }
            }

            void Connect() => NativeMethods.uv_pipe_connect(
                Handle,
                _workerEventLoop._pipe.Handle,
                _workerEventLoop._pipeName,
                WatcherCallback);
        }
    }
}