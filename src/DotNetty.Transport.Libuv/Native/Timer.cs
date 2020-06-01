// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    sealed class Timer : NativeHandle
    {
        static readonly uv_work_cb WorkCallback = OnWorkCallback;

        readonly Action<object> _callback;
        readonly object _state;

        public unsafe Timer(Loop loop, Action<object> callback, object state) 
            : base(uv_handle_type.UV_TIMER)
        {
            Debug.Assert(loop is object);
            Debug.Assert(callback is object);

            IntPtr handle = NativeMethods.Allocate(uv_handle_type.UV_TIMER);

            try
            {
                int result = NativeMethods.uv_timer_init(loop.Handle, handle);
                NativeMethods.ThrowIfError(result);
            }
            catch
            {
                NativeMethods.FreeMemory(handle);
                throw;
            }

            GCHandle gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
            ((uv_handle_t*)handle)->data = GCHandle.ToIntPtr(gcHandle);

            Handle = handle;
            _callback = callback;
            _state = state;
        }

        public Timer Start(long timeout, long repeat)
        {
            Debug.Assert(timeout >= 0);
            Debug.Assert(repeat >= 0);

            Validate();
            int result = NativeMethods.uv_timer_start(Handle, WorkCallback, timeout, repeat);
            NativeMethods.ThrowIfError(result);

            return this;
        }

        public Timer SetRepeat(long repeat)
        {
            Debug.Assert(repeat >= 0);

            Validate();
            NativeMethods.uv_timer_set_repeat(Handle, repeat);
            return this;
        }

        public long GetRepeat()
        {
            Validate();
            return NativeMethods.uv_timer_get_repeat(Handle);
        }

        public Timer Again()
        {
            Validate();
            int result = NativeMethods.uv_timer_again(Handle);
            NativeMethods.ThrowIfError(result);
            return this;
        }

        public void Stop()
        {
            if (!IsValid)
            {
                return;
            }

            int result = NativeMethods.uv_timer_stop(Handle);
            NativeMethods.ThrowIfError(result);
        }

        void OnWorkCallback()
        {
            try
            {
                _callback(_state);
            }
            catch (Exception exception)
            {
                Logger.CallbackRrror(HandleType, Handle, exception);
            }
        }

        static void OnWorkCallback(IntPtr handle)
        {
            var workHandle = GetTarget<Timer>(handle);
            workHandle?.OnWorkCallback();
        }
    }
}
