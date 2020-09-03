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
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

#pragma warning disable IDE1006 // 命名样式
    #region uv_err_t

    internal enum uv_err_code
    {
        UV_OK = 0,
        UV_E2BIG,
        UV_EACCES,
        UV_EADDRINUSE,
        UV_EADDRNOTAVAIL,
        UV_EAFNOSUPPORT,
        UV_EAGAIN,
        UV_EAI_ADDRFAMILY,
        UV_EAI_AGAIN,
        UV_EAI_BADFLAGS,
        UV_EAI_BADHINTS,
        UV_EAI_CANCELED,
        UV_EAI_FAIL,
        UV_EAI_FAMILY,
        UV_EAI_MEMORY,
        UV_EAI_NODATA,
        UV_EAI_NONAME,
        UV_EAI_OVERFLOW,
        UV_EAI_PROTOCOL,
        UV_EAI_SERVICE,
        UV_EAI_SOCKTYPE,
        UV_EALREADY,
        UV_EBADF,
        UV_EBUSY,
        UV_ECANCELED,
        UV_ECHARSET,
        UV_ECONNABORTED,
        UV_ECONNREFUSED,
        UV_ECONNRESET,
        UV_EDESTADDRREQ,
        UV_EEXIST,
        UV_EFAULT,
        UV_EFBIG,
        UV_EHOSTUNREACH,
        UV_EINTR,
        UV_EINVAL,
        UV_EIO,
        UV_EISCONN,
        UV_EISDIR,
        UV_ELOOP,
        UV_EMFILE,
        UV_EMSGSIZE,
        UV_ENAMETOOLONG,
        UV_ENETDOWN,
        UV_ENETUNREACH,
        UV_ENFILE,
        UV_ENOBUFS,
        UV_ENODEV,
        UV_ENOENT,
        UV_ENOMEM,
        UV_ENONET,
        UV_ENOPROTOOPT,
        UV_ENOSPC,
        UV_ENOSYS,
        UV_ENOTCONN,
        UV_ENOTDIR,
        UV_ENOTEMPTY,
        UV_ENOTSOCK,
        UV_ENOTSUP,
        UV_EPERM,
        UV_EPIPE,
        UV_EPROTO,
        UV_EPROTONOSUPPORT,
        UV_EPROTOTYPE,
        UV_ERANGE,
        UV_EROFS,
        UV_ESHUTDOWN,
        UV_ESPIPE,
        UV_ESRCH,
        UV_ETIMEDOUT,
        UV_ETXTBSY,
        UV_EXDEV,
        UV_UNKNOWN,
        UV_EOF = -4095,
        UV_ENXIO,
        UV_EMLINK,
    }

    #endregion

    #region Native Callbacks

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void uv_close_cb(IntPtr conn);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void uv_work_cb(IntPtr watcher);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void uv_watcher_cb(IntPtr watcher, int status);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void uv_poll_cb(IntPtr handle, int status, int events);

    #endregion Native Callbacks

    internal static partial class NativeMethods
    {
        private const string LibraryName = "libuv";

#if NETCOREAPP || NETSTANDARD
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        internal static IntPtr Allocate(int size) => Marshal.AllocCoTaskMem(size);

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        internal static void FreeMemory(IntPtr ptr) => Marshal.FreeCoTaskMem(ptr);
#else
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        internal static IntPtr Allocate(int size) => Marshal.AllocHGlobal(size);

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        internal static void FreeMemory(IntPtr ptr) => Marshal.FreeHGlobal(ptr);
#endif

        #region Common

        internal static bool IsHandleActive(IntPtr handle) =>
            handle != IntPtr.Zero && (uint)uv_is_active(handle) > 0u;

        internal static void AddReference(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            uv_ref(handle);
        }

        internal static void ReleaseReference(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            uv_unref(handle);
        }

        internal static bool HadReference(IntPtr handle) =>
            handle != IntPtr.Zero && (uint)uv_has_ref(handle) > 0u;

        internal static void CloseHandle(IntPtr handle, uv_close_cb callback)
        {
            if (handle == IntPtr.Zero || callback is null)
            {
                return;
            }

            int result = uv_is_closing(handle);
            if (0u >= (uint)result)
            {
                uv_close(handle, callback);
            }
        }

        internal static bool IsHandleClosing(IntPtr handle) =>
            handle != IntPtr.Zero && (uint)uv_is_closing(handle) > 0u;

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void uv_close(IntPtr handle, uv_close_cb close_cb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_is_closing(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void uv_ref(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void uv_unref(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_has_ref(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_is_active(IntPtr handle);

        #endregion Common

        #region Error

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        internal static void ThrowIfError(int code)
        {
            if ((uint)code > SharedConstants.TooBigOrNegative) // < 0
            {
                ThrowOperationException((uv_err_code)code);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowOperationException(uv_err_code error) => throw CreateError(error);

        internal static OperationException CreateError(uv_err_code error)
        {
            string name = GetErrorName(error);
            string description = GetErrorDescription(error);
            return new OperationException((int)error, name, description);
        }

        private static string GetErrorDescription(uv_err_code code)
        {
            IntPtr ptr = uv_strerror(code);
            if (ptr == IntPtr.Zero) { return null; }
            return Marshal.PtrToStringAnsi(ptr);
        }

        private static string GetErrorName(uv_err_code code)
        {
            IntPtr ptr = uv_err_name(code);
            if (ptr == IntPtr.Zero) { return null; }
            return Marshal.PtrToStringAnsi(ptr);
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr uv_strerror(uv_err_code err);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr uv_err_name(uv_err_code err);

        #endregion Error

        #region Version

        internal static Version GetVersion()
        {
            uint version = uv_version();
            int major = (int)(version & 0xFF0000) >> 16;
            int minor = (int)(version & 0xFF00) >> 8;
            int patch = (int)(version & 0xFF);

            return new Version(major, minor, patch);
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint uv_version();

        #endregion Version
    }
#pragma warning restore IDE1006 // 命名样式
}
