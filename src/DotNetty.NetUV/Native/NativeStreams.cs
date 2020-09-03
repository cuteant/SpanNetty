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
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Requests;

#pragma warning disable IDE1006 // 命名样式
    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_buf_t
    {
        private static readonly bool IsWindows = Platform.IsWindows;
        private static readonly int Size = IntPtr.Size;

        /*
           Windows 
           public int length;
           public IntPtr data;

           Unix
           public IntPtr data;
           public IntPtr length;
        */

        private readonly IntPtr first;
        private readonly IntPtr second;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void InitMemory(IntPtr buf, IntPtr memory, int length)
        {
            var len = (IntPtr)length;
            if (IsWindows)
            {
                *(IntPtr*)buf = len;
                *(IntPtr*)(buf + Size) = memory;
            }
            else
            {
                *(IntPtr*)buf = memory;
                *(IntPtr*)(buf + Size) = len;
            }
        }

        internal uv_buf_t(IntPtr memory, int length)
        {
            Debug.Assert(length >= 0);

            if (IsWindows)
            {
                this.first = (IntPtr)length;
                this.second = memory;
            }
            else
            {
                this.first = memory;
                this.second = (IntPtr)length;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_stream_t
    {
        /* handle fields */
        public IntPtr data;
        public IntPtr loop;
        public uv_handle_type type;
        public IntPtr close_cb;

        /* stream fields */
        public IntPtr write_queue_size; /* number of bytes queued for writing */
        public IntPtr alloc_cb;
        public IntPtr read_cb;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void uv_alloc_cb(IntPtr handle, IntPtr suggested_size, out uv_buf_t buf);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void uv_read_cb(IntPtr handle, IntPtr nread, ref uv_buf_t buf);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void uv_udp_recv_cb(IntPtr handle, IntPtr nread, ref uv_buf_t buf, ref sockaddr addr, int flags);

    internal static partial class NativeMethods
    {
        internal static void StreamReadStart(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_read_start(handle, StreamHandle.AllocateCallback, StreamHandle.ReadCallback);
            ThrowIfError(result);
        }

        internal static void StreamReadStop(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_read_stop(handle);
            ThrowIfError(result);
        }

        internal static bool IsStreamReadable(IntPtr handle) => handle != IntPtr.Zero && uv_is_readable(handle) == 1;

        internal static bool IsStreamWritable(IntPtr handle) => handle != IntPtr.Zero && uv_is_writable(handle) == 1;

        internal static void TryWriteStream(IntPtr handle, ref uv_buf_t buf)
        {
            Debug.Assert(handle != IntPtr.Zero);

            var bufs = new [] { buf };
            int result = uv_try_write(handle , bufs, bufs.Length);
            ThrowIfError(result);
        }

        internal static unsafe void WriteStream(IntPtr requestHandle, IntPtr streamHandle, uv_buf_t* bufs, ref int size)
        {
            Debug.Assert(requestHandle != IntPtr.Zero);
            Debug.Assert(streamHandle != IntPtr.Zero);

            int result = uv_write(requestHandle, streamHandle, bufs, size, WriteRequest.WriteCallback);
            ThrowIfError(result);
        }

        internal static unsafe void WriteStream(IntPtr requestHandle, IntPtr streamHandle, uv_buf_t* bufs, ref int size, IntPtr sendHandle)
        {
            Debug.Assert(requestHandle != IntPtr.Zero);
            Debug.Assert(streamHandle != IntPtr.Zero);
            Debug.Assert(sendHandle != IntPtr.Zero);

            int result = uv_write2(requestHandle, streamHandle, bufs, size, sendHandle, WriteRequest.WriteCallback);
            ThrowIfError(result);
        }

        internal static void StreamListen(IntPtr handle, int backlog)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(backlog > 0);

            int result = uv_listen(handle, backlog, ServerStream.ConnectionCallback);
            ThrowIfError(result);
        }

        internal static void StreamAccept(IntPtr serverHandle, IntPtr clientHandle)
        {
            Debug.Assert(serverHandle != IntPtr.Zero);
            Debug.Assert(clientHandle != IntPtr.Zero);

            int result = uv_accept(serverHandle, clientHandle);
            ThrowIfError(result);
        }

        // If *value == 0, it will return the current send buffer size, 
        // otherwise it will use *value to set the new send buffersize.
        // This function works for TCP, pipe and UDP handles on Unix and for TCP and UDP handles on Windows.
        internal static int SendBufferSize(IntPtr handle, int value)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(value >= 0);

            var size = (IntPtr)value;
            int result = uv_send_buffer_size(handle, ref size);
            ThrowIfError(result);

            return size.ToInt32();
        }

        // If *value == 0, it will return the current receive buffer size,
        // otherwise it will use *value to set the new receive buffer size.
        // This function works for TCP, pipe and UDP handles on Unix and for TCP and UDP handles on Windows.

        internal static int ReceiveBufferSize(IntPtr handle, int value)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(value >= 0);

            var size = (IntPtr)value;
            int result = uv_recv_buffer_size(handle, ref size);
            ThrowIfError(result);

            return size.ToInt32();
        }

        // Gets the platform dependent file descriptor equivalent.
        // The following handles are supported: TCP, pipes, TTY, UDP and poll. Passing any other handle 
        // type will fail with UV_EINVAL.
        // If a handle doesn’t have an attached file descriptor yet or the handle itself has been closed, 
        // this function will return UV_EBADF.
        // Warning: Be very careful when using this function. libuv assumes it’s in control of the 
        // file descriptor so any change to it may lead to malfunction.
        internal static void GetFileDescriptor(IntPtr handle, ref IntPtr value)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_fileno(handle, ref value);
            ThrowIfError(result);
        }

        #region Stream Status

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_listen(IntPtr handle, int backlog, uv_watcher_cb connection_cb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_accept(IntPtr server, IntPtr client);

        #endregion Stream Status

        #region Read/Write

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_fileno(IntPtr handle, ref IntPtr value);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_send_buffer_size(IntPtr handle, ref IntPtr value);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_recv_buffer_size(IntPtr handle, ref IntPtr value);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_is_readable(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_is_writable(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_read_start(IntPtr handle, uv_alloc_cb alloc_cb, uv_read_cb read_cb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_read_stop(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_try_write(IntPtr handle, uv_buf_t[] bufs, int bufcnt);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int uv_write(IntPtr req, IntPtr handle, uv_buf_t* bufs, int nbufs, uv_watcher_cb cb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int uv_write2(IntPtr req, IntPtr handle, uv_buf_t* bufs, int nbufs, IntPtr sendHandle, uv_watcher_cb cb);

        #endregion
    }
#pragma warning restore IDE1006 // 命名样式
}
