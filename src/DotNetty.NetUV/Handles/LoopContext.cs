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

namespace DotNetty.NetUV.Handles
{
    using System;
    using System.Runtime.InteropServices;
    using DotNetty.NetUV.Native;

    internal sealed unsafe class LoopContext : NativeHandle
    {
        private static readonly uv_walk_cb WalkCallback = (h, a) => OnWalkCallback(h, a);

        public LoopContext()
        {
            int size = NativeMethods.GetLoopSize();
            IntPtr handle = NativeMethods.Allocate(size);

            Handle = handle;
            try
            {
                NativeMethods.InitializeLoop(handle);
            }
            catch
            {
                NativeMethods.FreeMemory(handle);
                throw;
            }

            GCHandle gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
            ((uv_loop_t*)handle)->data = GCHandle.ToIntPtr(gcHandle);

            if (Log.InfoEnabled) { Log.Loop_allocated(handle); }
        }

        public bool IsAlive =>
            IsValid
            && NativeMethods.IsLoopAlive(Handle);

        public long Now
        {
            get
            {
                Validate();
                return NativeMethods.LoopNow(Handle);
            }
        }

        public long NowInHighResolution
        {
            get
            {
                Validate();
                return NativeMethods.LoopNowInHighResolution(Handle);
            }
        }

        public int ActiveHandleCount() =>
            IsValid
                ? (int)((uv_loop_t*)Handle)->active_handles
                : 0;

        public void UpdateTime()
        {
            Validate();
            NativeMethods.LoopUpdateTime(Handle);
        }

        internal int GetBackendTimeout()
        {
            Validate();
            return NativeMethods.GetBackendTimeout(Handle);
        }

        internal int Run(uv_run_mode mode)
        {
            Validate();
            return NativeMethods.RunLoop(Handle, mode);
        }

        public void Stop()
        {
            Validate();
            NativeMethods.StopLoop(Handle);
        }

        protected override void CloseHandle()
        {
            IntPtr handle = Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            // Get gc handle before close loop
            IntPtr pHandle = ((uv_loop_t*)handle)->data;

            // Fully close the loop, similar to 
            //https://github.com/libuv/libuv/blob/v1.x/test/task.h#L190

            int count = 0;
            int result;
            while (true)
            {
#if DEBUG
                if (Log.DebugEnabled) { Log.Debug($"Loop {handle} walking handles, count = {count}."); }
#endif
                NativeMethods.WalkLoop(handle, WalkCallback);

#if DEBUG
                if (Log.DebugEnabled) { Log.Debug($"Loop {handle} running default to call close callbacks, count = {count}."); }
#endif
                NativeMethods.RunLoop(Handle, uv_run_mode.UV_RUN_DEFAULT);

                result = NativeMethods.CloseLoop(handle);
#if DEBUG
                if (Log.DebugEnabled) { Log.Debug($"Loop {handle} close result = {result}, count = {count}."); }
#endif
                if (0u >= (uint)result)
                {
                    break;
                }
#if DEBUG
                else
                {
                    if (Log.TraceEnabled)
                    {
                        OperationException error = NativeMethods.CreateError((uv_err_code)result);
                        Log.Trace($"Loop {handle} close error {error}");
                    }
                }
#endif
                count++;
                if ((uint)count >= 20u)
                {
                    Log.Loop_close_all_handles_limit_20_times_exceeded(handle);
                    break;
                }
            }

            if (Log.InfoEnabled) { Log.Loop_closed(handle); }

            // Free GCHandle
            if (pHandle != IntPtr.Zero)
            {
                GCHandle nativeHandle = GCHandle.FromIntPtr(pHandle);
                if (nativeHandle.IsAllocated)
                {
                    nativeHandle.Free();
                    ((uv_loop_t*)handle)->data = IntPtr.Zero;
                    if (Log.InfoEnabled) { Log.Loop_GCHandle_released(handle); }
                }
            }

            // Release memory
            NativeMethods.FreeMemory(handle);
            Handle = IntPtr.Zero;
            if (Log.InfoEnabled) { Log.Loop_memory_released(handle); }
        }

        private static void OnWalkCallback(IntPtr handle, IntPtr loopHandle)
        {
            if (handle == IntPtr.Zero) { return; }

            try
            {
                var target = HandleContext.GetTarget<IDisposable>(handle);
                if (Log.InfoEnabled) { Log.LoopWalkCallbackDisposed(loopHandle, handle, target); }
                target?.Dispose();
            }
            catch (Exception exception)
            {
                Log.Loop_Walk_callback_attempt_to_close_handle_failed(loopHandle, handle, exception);
            }
        }
    }
}
