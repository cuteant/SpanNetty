// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    sealed class DispatcherEventLoop : LoopExecutor, IEventLoop
    {
        PipeListener _pipeListener;
        IServerNativeUnsafe _nativeUnsafe;

        internal DispatcherEventLoop(IEventLoopGroup parent, string threadName = null)
            : base(parent, threadName)
        {
            if (parent is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.parent); }

            string pipeName = "DotNetty_" + Guid.NewGuid().ToString("n");
            PipeName = (PlatformApi.IsWindows
                ? @"\\.\pipe\"
                : "/tmp/") + pipeName;
            Start();
        }

        internal string PipeName { get; }

        internal void Register(IServerNativeUnsafe serverChannel)
        {
            Debug.Assert(serverChannel is object);
            _nativeUnsafe = serverChannel;
        }

        protected override void Initialize()
        {
            _pipeListener = new PipeListener(UnsafeLoop, false);
            _pipeListener.Listen(PipeName);

            if (Logger.InfoEnabled)
            {
                Logger.ListeningOnPipe(LoopThreadId, PipeName);
            }
        }

        protected override void Release() => _pipeListener.Shutdown();

        internal void Dispatch(NativeHandle handle)
        {
            try
            {
                _pipeListener.DispatchHandle(handle);
            }
            catch
            {
                handle.CloseHandle();
                throw;
            }
        }

        internal void Accept(NativeHandle handle) => _nativeUnsafe.Accept(handle);

        public new IEventLoop GetNext() => (IEventLoop)base.GetNext();

        public Task RegisterAsync(IChannel channel) => channel.Unsafe.RegisterAsync(this);

        public new IEventLoopGroup Parent => (IEventLoopGroup)base.Parent;

        public new IEnumerable<IEventLoop> Items => new[] { this };
    }
}
