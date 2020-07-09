// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Diagnostics;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    public sealed class DispatcherEventLoop : LoopExecutor
    {
        private PipeListener _pipeListener;
        private IServerNativeUnsafe _nativeUnsafe;

        internal DispatcherEventLoop(IEventLoopGroup parent)
            : this(parent, DefaultThreadFactory<DispatcherEventLoop>.Instance)
        {
        }

        internal DispatcherEventLoop(IEventLoopGroup parent, IThreadFactory threadFactory)
            : base(parent, threadFactory, RejectedExecutionHandlers.Reject(), DefaultBreakoutInterval)
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
    }
}
