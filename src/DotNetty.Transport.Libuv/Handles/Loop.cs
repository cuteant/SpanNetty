/*
 * Copyright (c) Johnny Z. All rights reserved.
 *
 *   https://github.com/StormHub/NetUV
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com)
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Transport.Libuv.Handles
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Native;
    using DotNetty.Transport.Libuv.Requests;

    public sealed class Loop : IDisposable
    {
        internal static readonly ThreadLocalPool<WriteRequest> WriteRequestPool = new ThreadLocalPool<WriteRequest>(
            handle => new WriteRequest(uv_req_type.UV_WRITE, handle));
        internal static readonly ThreadLocalPool<WriteRequest> SendRequestPool = new ThreadLocalPool<WriteRequest>(
            handle => new WriteRequest(uv_req_type.UV_UDP_SEND, handle));

        private readonly LoopContext _handle;

        public Loop()
        {
            _handle = new LoopContext();
        }

        internal IntPtr InternalHandle
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => _handle.Handle;
        }

        public bool IsAlive => _handle.IsAlive;

        public long Now => _handle.Now;

        public long NowInHighResolution => _handle.NowInHighResolution;

        public int ActiveHandleCount() => _handle.ActiveHandleCount();

        public void UpdateTime() => _handle.UpdateTime();

        internal int GetBackendTimeout() => _handle.GetBackendTimeout();

        public int RunDefault() => _handle.Run(uv_run_mode.UV_RUN_DEFAULT);

        public int RunOnce() => _handle.Run(uv_run_mode.UV_RUN_ONCE);

        public int RunNoWait() => _handle.Run(uv_run_mode.UV_RUN_NOWAIT);

        public void Stop() => _handle.Stop();

        public Udp CreateUdp()
        {
            _handle.Validate();
            return new Udp(_handle);
        }

        public Pipe CreatePipe(bool ipc = false)
        {
            _handle.Validate();
            return new Pipe(_handle, ipc);
        }

        public Tcp CreateTcp(uint flags = 0u /* AF_UNSPEC */)
        {
            _handle.Validate();
            return new Tcp(_handle, flags);
        }

        public Tty CreateTty(TtyType type)
        {
            _handle.Validate();
            return new Tty(_handle, type);
        }

        public Timer CreateTimer()
        {
            _handle.Validate();
            return new Timer(_handle);
        }

        public Timer CreateTimer(Action<Timer> callback)
        {
            _handle.Validate();
            return new Timer(_handle, callback);
        }

        public Timer CreateTimer(Action<Timer, object> callback, object state)
        {
            _handle.Validate();
            return new Timer(_handle, callback, state);
        }

        public Prepare CreatePrepare()
        {
            _handle.Validate();
            return new Prepare(_handle);
        }

        public Check CreateCheck()
        {
            _handle.Validate();
            return new Check(_handle);
        }

        public Idle CreateIdle()
        {
            _handle.Validate();
            return new Idle(_handle);
        }

        public Async CreateAsync(Action<Async> callback)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            _handle.Validate();
            return new Async(_handle, callback);
        }

        public Async CreateAsync(Action<Async, object> callback, object state)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            _handle.Validate();
            return new Async(_handle, callback, state);
        }

        public Poll CreatePoll(int fileDescriptor)
        {
            _handle.Validate();
            return new Poll(_handle, fileDescriptor);
        }

        public Poll CreatePoll(IntPtr socket)
        {
            if (socket == IntPtr.Zero) { ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.socket); }

            _handle.Validate();
            return new Poll(_handle, socket);
        }

        public Signal CreateSignal()
        {
            _handle.Validate();
            return new Signal(_handle);
        }

        public FSEvent CreateFSEvent()
        {
            _handle.Validate();
            return new FSEvent(_handle);
        }

        public FSPoll CreateFSPoll()
        {
            _handle.Validate();
            return new FSPoll(_handle);
        }

        public Work CreateWorkRequest(Action<Work> workCallback, Action<Work> afterWorkCallback)
        {
            if (workCallback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.workCallback); }

            _handle.Validate();
            return new Work(_handle, workCallback, afterWorkCallback);
        }

        public AddressInfoRequest CreateAddressInfoRequest()
        {
            _handle.Validate();
            return new AddressInfoRequest(_handle);
        }

        public NameInfoRequest CreateNameInfoRequest()
        {
            _handle.Validate();
            return new NameInfoRequest(_handle);
        }

        public static Version NativeVersion => NativeMethods.GetVersion();

        public void Dispose() => _handle.Dispose();
    }
}
