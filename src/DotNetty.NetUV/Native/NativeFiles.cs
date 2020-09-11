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

namespace DotNetty.NetUV.Native
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using DotNetty.NetUV.Handles;

#pragma warning disable IDE1006 // 命名样式
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void uv_fs_event_cb(IntPtr handle, string filename, int events, int status);

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_timespec_t
    {
        private static readonly DateTime StartDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public readonly long tv_sec;
        public readonly long tv_nsec;

        public static explicit operator DateTime(uv_timespec_t timespec)
        {
            if (timespec.tv_sec <= 0L)
            {
                return StartDateTime;
            }


            try
            {
                return StartDateTime
                    .AddSeconds(timespec.tv_sec)
                    .AddTicks(timespec.tv_nsec / 100);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Invalid time values, sometimes happens on Window
                // for last change time.
                return StartDateTime;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_stat_t
    {
        public readonly long st_dev;
        public readonly long st_mode;
        public readonly long st_nlink;
        public readonly long st_uid;
        public readonly long st_gid;
        public readonly long st_rdev;
        public readonly long st_ino;
        public readonly long st_size;
        public readonly long st_blksize;
        public readonly long st_blocks;
        public readonly long st_flags;
        public readonly long st_gen;
        public readonly uv_timespec_t st_atim;
        public readonly uv_timespec_t st_mtim;
        public readonly uv_timespec_t st_ctim;
        public readonly uv_timespec_t st_birthtim;

        public static explicit operator FileStatus(uv_stat_t stat) => new FileStatus(stat);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void uv_fs_poll_cb(IntPtr handle, int status, ref uv_stat_t prev, ref uv_stat_t curr);

    internal static partial class NativeMethods
    {
        private const int FileNameBufferSize = 2048;

        #region FSPoll

        internal static string FSPollGetPath(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            string path;
            IntPtr buf = IntPtr.Zero;
            try
            {
                buf = Allocate(FileNameBufferSize);
                var length = (IntPtr)FileNameBufferSize;

                int result = uv_fs_poll_getpath(handle, buf, ref length);
                ThrowIfError(result);

                path = Marshal.PtrToStringAnsi(buf, length.ToInt32());
            }
            finally
            {
                if (buf != IntPtr.Zero)
                {
                    FreeMemory(buf);
                }
            }

            return path;
        }

        internal static void FSPollStart(IntPtr handle, string path, int interval)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(!string.IsNullOrEmpty(path));
            Debug.Assert(interval > 0);

            int result = uv_fs_poll_start(handle, FSPoll.FSPollCallback, path, interval);
            ThrowIfError(result);
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_fs_poll_init(IntPtr loopHandle, IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_fs_poll_stop(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_fs_poll_start(IntPtr handle, uv_fs_poll_cb cb, string path, int interval);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_fs_poll_getpath(IntPtr handle, IntPtr buffer, ref IntPtr size);

        #endregion FSPoll

        #region FSEvent

        internal static void FSEventStart(IntPtr handle, string path, FSEventMask mask)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(!string.IsNullOrEmpty(path));

            int result = uv_fs_event_start(handle, FSEvent.FSEventCallback, path, (int)mask);
            ThrowIfError(result);
        }

        internal static string FSEventGetPath(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            string path;
            IntPtr buf = IntPtr.Zero;
            try
            {
                buf = Allocate(FileNameBufferSize);
                var length = (IntPtr)FileNameBufferSize;

                int result = uv_fs_event_getpath(handle, buf, ref length);
                ThrowIfError(result);

                path = Marshal.PtrToStringAnsi(buf, length.ToInt32());
            }
            finally
            {
                if (buf != IntPtr.Zero)
                {
                    FreeMemory(buf);
                }
            }

            return path;
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_fs_event_init(IntPtr loopHandle, IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_fs_event_start(IntPtr handle, uv_fs_event_cb cb, string path, int flags);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_fs_event_stop(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_fs_event_getpath(IntPtr handle, IntPtr buffer, ref IntPtr size);

        #endregion FSEvent
    }
#pragma warning restore IDE1006 // 命名样式
}
