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

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

#pragma warning disable IDE1006 // 命名样式
    internal enum uv_run_mode
    {
        UV_RUN_DEFAULT = 0,
        UV_RUN_ONCE,
        UV_RUN_NOWAIT
    };

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_loop_t
    {
        /* User data - use this for whatever. */
        public IntPtr data;

        /* Loop reference counting. */
        public uint active_handles;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void uv_walk_cb(IntPtr handle, IntPtr arg);

    internal static partial class NativeMethods
    {
        internal static int GetLoopSize()
        {
            return uv_loop_size().ToInt32();
        }

        internal static void InitializeLoop(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_loop_init(handle);
            ThrowIfError(result);
        }

        internal static int CloseLoop(IntPtr handle) => handle == IntPtr.Zero ? 0 : uv_loop_close(handle);

        internal static void WalkLoop(IntPtr handle, uv_walk_cb callback)
        {
            if (handle == IntPtr.Zero || callback is null) { return; }

            uv_walk(handle, callback, handle);
        }

        internal static int RunLoop(IntPtr handle, uv_run_mode mode)
        {
            Debug.Assert(handle != IntPtr.Zero);

            /*
              UV_RUN_DEFAULT: 
                Runs the event loop until there are no more active and referenced handles or requests.
                Returns non-zero if uv_stop() was called and there are still active handles or requests.
                Returns zero in all other cases.

              UV_RUN_ONCE: 
                Poll for i/o once. Note that this function blocks if there are no pending callbacks.
                Returns zero when done (no active handles or requests left), 
                or non-zero if more callbacks are expected(meaning you should run the event loop again sometime in the future).

              UV_RUN_NOWAIT: 
                Poll for i/o once but don’t block if there are no pending callbacks.
                Returns zero if done(no active handles or requests left), 
                or non-zero if more callbacks are expected(meaning you should run the event loop again sometime in the future).
            */

            return uv_run(handle, mode);
        }

        internal static void StopLoop(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            uv_stop(handle);
        }

        internal static bool IsLoopAlive(IntPtr handle) =>
            handle != IntPtr.Zero && (uint)uv_loop_alive(handle) > 0u;

        internal static long LoopNow(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return uv_now(handle);
        }

        internal static long LoopNowInHighResolution(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return uv_hrtime(handle);
        }

        internal static int GetBackendTimeout(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            // The return value is in milliseconds, or -1 for no timeout.
            int timeout = uv_backend_timeout(handle);
            return timeout > 0 ? timeout : 0;
        }

        internal static void LoopUpdateTime(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            uv_update_time(handle);
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr uv_default_loop();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_loop_init(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void uv_stop(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_loop_close(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_loop_alive(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_run(IntPtr handle, uv_run_mode mode);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr uv_loop_size();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void uv_update_time(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern long uv_now(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern long uv_hrtime(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_backend_timeout(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void uv_walk(IntPtr handle, uv_walk_cb walk_cb, IntPtr arg);
    }
#pragma warning restore IDE1006 // 命名样式
}
