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
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using DotNetty.NetUV.Requests;

#pragma warning disable IDE1006 // 命名样式
    internal enum uv_req_type
    {
        UV_UNKNOWN_REQ = 0,
        UV_REQ,
        UV_CONNECT,
        UV_WRITE,
        UV_SHUTDOWN,
        UV_UDP_SEND,
        UV_FS,
        UV_WORK,
        UV_GETADDRINFO,
        UV_GETNAMEINFO,
        UV_REQ_TYPE_PRIVATE,
        UV_REQ_TYPE_MAX
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_req_t
    {
        public IntPtr data;
        public uv_req_type type;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_write_t
    {
        /* uv_req_t fields */
        public IntPtr data;
        public uv_req_type type;

        /* uv_write_t fields */

        // Write callback
        public IntPtr cb; // uv_write_cb cb;

        // Pointer to the stream being sent using this write request.
        public IntPtr send_handle;  // uv_stream_t* send_handle;

        // Pointer to the stream where this write request is running.
        public IntPtr handle; // uv_stream_t* handle;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_shutdown_t
    {
        /* uv_req_t fields */
        public IntPtr data;
        public uv_req_type type;

        public IntPtr handle;  // uv_stream_t*
        public IntPtr cb;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_work_t
    {
        /* uv_handle_t fields */
        public IntPtr data;
        public IntPtr loop;
        public uv_req_type type;
        public IntPtr close_cb;

        /* work fields */
        public IntPtr work_cb;
        public IntPtr after_work_cb;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_connect_t
    {
        /* uv_req_t fields */
        public IntPtr data;
        public uv_req_type type;

        /* connect fields */
        public IntPtr cb; // uv_connect_cb
        public IntPtr handle; // uv_stream_t*
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_getaddrinfo_t
    {
        /* uv_req_t fields */
        public IntPtr data;
        public uv_req_type type;

        /* getaddrinfo fields */
        public IntPtr loop;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct addrinfo
    {
        private static readonly bool s_isWindows = Platform.IsWindows;

        public readonly int ai_flags;
        public readonly int ai_family;   // AF_INET or AF_INET6
        public readonly int ai_socktype; // SOCK_DGRAM or SOCK_STREAM
        public readonly int ai_protocol;
        public readonly IntPtr ai_addrlen;
        private readonly IntPtr _field1;
        private readonly IntPtr _field2;

        /* Windows 
           IntPtr ai_canonname; // char*
           IntPtr ai_addr;      // sockaddr
        */

        /* Unix 
           IntPtr ai_addr;      // sockaddr
           IntPtr ai_canonname; // char*
        */

        internal string GetCanonName()
        {
            IntPtr value = s_isWindows ? _field1 : _field2;
            return value != IntPtr.Zero 
                ? Marshal.PtrToStringUni(value) 
                : null;
        }

        internal unsafe IPAddress GetAddress()
        {
            // Only for IP/IPv6
            if (ai_family != (int)AddressFamily.InterNetwork
                && ai_family != (int)AddressFamily.InterNetworkV6)
            {
                return null;
            }

            IntPtr value = s_isWindows ? _field2 : _field1;
            if (value == IntPtr.Zero)
            {
                return null;
            }

            var addr = Unsafe.Read<sockaddr>((void*)value);
            IPEndPoint endPoint = addr.GetIPEndPoint();
            return endPoint?.Address;
        }

        public IntPtr Addr => s_isWindows ? _field2 : _field1;

        public readonly IntPtr ai_next; // addrinfo
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_getnameinfo_t
    {
        /* uv_req_t fields */
        public IntPtr data;
        public uv_req_type type;

        /* getaddrinfo fields */
        public IntPtr loop;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void uv_getaddrinfo_cb(IntPtr req, int status, ref addrinfo res);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void uv_getnameinfo_cb(IntPtr req, int status, string hostname, string service);

    internal static partial class NativeMethods
    {
        internal static unsafe void GetAddressInfo(
            IntPtr loopHandle, 
            IntPtr handle, 
            string node, 
            string service, 
            uv_getaddrinfo_cb callback)
        {
            Debug.Assert(loopHandle != IntPtr.Zero);
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(!string.IsNullOrEmpty(node) || !string.IsNullOrEmpty(service));

            int result = uv_getaddrinfo(
                loopHandle, 
                handle, 
                callback, 
                node, 
                service, 
                null);
            ThrowIfError(result);
        }

        internal static void GetNameInfo(
            IntPtr loopHandle,
            IntPtr handle,
            IPEndPoint endPoint,
            NameInfoFlags flags,
            uv_getnameinfo_cb callback)
        {
            Debug.Assert(loopHandle != IntPtr.Zero);
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(endPoint is object);

            GetSocketAddress(endPoint, out sockaddr addr);

            int result = uv_getnameinfo(loopHandle, handle, callback, ref addr, (int)flags);
            ThrowIfError(result);
        }

        internal static unsafe void FreeAddressInfo(ref addrinfo addrinfo)
        {
            var handle = (IntPtr)Unsafe.AsPointer(ref addrinfo);
            if (handle != IntPtr.Zero)
            {
                uv_freeaddrinfo(handle);
            }
        }

        internal static void Shutdown(IntPtr requestHandle, IntPtr streamHandle)
        {
            Debug.Assert(requestHandle != IntPtr.Zero);
            Debug.Assert(streamHandle != IntPtr.Zero);

            int result = uv_shutdown(requestHandle, streamHandle, WatcherRequest.WatcherCallback);
            ThrowIfError(result);
        }

        internal static void QueueWork(IntPtr loopHandle, IntPtr handle)
        {
            Debug.Assert(loopHandle != IntPtr.Zero);
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_queue_work(loopHandle, handle, Work.WorkCallback, Work.AfterWorkCallback);
            ThrowIfError(result);
        }

        internal static bool Cancel(IntPtr handle) =>  handle != IntPtr.Zero && 0u >= (uint)uv_cancel(handle);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetSize(uv_req_type requestType) =>  RequestSizeTable[unchecked((int)requestType - 1)];

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_getnameinfo(IntPtr loopHandle, IntPtr handle, uv_getnameinfo_cb cb, ref sockaddr addr, int flags);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int uv_getaddrinfo(IntPtr loopHandle, IntPtr handle, uv_getaddrinfo_cb cb, string node, string service, addrinfo* hints);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void uv_freeaddrinfo(IntPtr ai);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_queue_work(IntPtr loopHandle, IntPtr handle, uv_work_cb work_cb, uv_watcher_cb after_work_cb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_cancel(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_shutdown(IntPtr requestHandle, IntPtr streamHandle, uv_watcher_cb callback);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr uv_req_size(uv_req_type reqType);
    }
#pragma warning restore IDE1006 // 命名样式
}
