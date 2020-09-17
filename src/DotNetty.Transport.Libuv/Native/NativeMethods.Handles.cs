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
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Handles;
    using DotNetty.Transport.Libuv.Requests;

#pragma warning disable IDE1006 // 命名样式
    internal enum uv_handle_type
    {
        UV_UNKNOWN_HANDLE = 0,
        UV_ASYNC,
        UV_CHECK,
        UV_FS_EVENT,
        UV_FS_POLL,
        UV_HANDLE,
        UV_IDLE,
        UV_NAMED_PIPE,
        UV_POLL,
        UV_PREPARE,
        UV_PROCESS,
        UV_STREAM,
        UV_TCP,
        UV_TIMER,
        UV_TTY,
        UV_UDP,
        UV_SIGNAL,
        UV_FILE,
        UV_HANDLE_TYPE_MAX
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_handle_t
    {
        public IntPtr data;
        public IntPtr loop;
        public uv_handle_type type;
        public IntPtr close_cb;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_prepare_t
    {
        /* uv_handle_t fields */
        public IntPtr data;
        public IntPtr loop;
        public uv_handle_type type;
        public IntPtr close_cb;

        /* prepare fields */
        public IntPtr prepare_prev;
        public IntPtr prepare_next;
        public IntPtr prepare_cb;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct uv_check_t
    {
        /* uv_handle_t fields */
        public IntPtr data;
        public IntPtr loop;
        public uv_handle_type type;
        public IntPtr close_cb;

        /* prepare fields */
        public IntPtr check_prev;
        public IntPtr check_next;
        public IntPtr uv_check_cb;
    }

    /// <summary>
    /// https://github.com/aspnet/KestrelHttpServer/blob/dev/src/Microsoft.AspNetCore.Server.Kestrel/Internal/Networking/SockAddr.cs
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct sockaddr
    {
        // this type represents native memory occupied by sockaddr struct
        // https://msdn.microsoft.com/en-us/library/windows/desktop/ms740496(v=vs.85).aspx
        // although the c/c++ header defines it as a 2-byte short followed by a 14-byte array,
        // the simplest way to reserve the same size in c# is with four nameless long values
        public long _field0;
        public long _field1;
        public long _field2;
        public long _field3;

        // ReSharper disable once UnusedParameter.Local
#pragma warning disable IDE0060 // 删除未使用的参数
        internal sockaddr(long ignored)
#pragma warning restore IDE0060 // 删除未使用的参数
        {
            _field0 = _field1 = _field2 = _field3 = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe IPEndPoint GetIPEndPoint()
        {
            // The bytes are represented in network byte order.
            //
            // Example 1: [2001:4898:e0:391:b9ef:1124:9d3e:a354]:39179
            //
            // 0000 0000 0b99 0017  => The third and fourth bytes 990B is the actual port
            // 9103 e000 9848 0120  => IPv6 address is represented in the 128bit field1 and field2.
            // 54a3 3e9d 2411 efb9     Read these two 64-bit long from right to left byte by byte.
            // 0000 0000 0000 0010  => Scope ID 0x10 (eg [::1%16]) the first 4 bytes of field3 in host byte order.
            //
            // Example 2: 10.135.34.141:39178 when adopt dual-stack sockets, IPv4 is mapped to IPv6
            //
            // 0000 0000 0a99 0017  => The port representation are the same
            // 0000 0000 0000 0000
            // 8d22 870a ffff 0000  => IPv4 occupies the last 32 bit: 0A.87.22.8d is the actual address.
            // 0000 0000 0000 0000
            //
            // Example 3: 10.135.34.141:12804, not dual-stack sockets
            //
            // 8d22 870a fd31 0002  => sa_family == AF_INET (02)
            // 0000 0000 0000 0000
            // 0000 0000 0000 0000
            // 0000 0000 0000 0000
            //
            // Example 4: 127.0.0.1:52798, on a Mac OS
            //
            // 0100 007F 3ECE 0210  => sa_family == AF_INET (02) Note that struct sockaddr on mac use
            // 0000 0000 0000 0000     the second unint8 field for sa family type
            // 0000 0000 0000 0000     http://www.opensource.apple.com/source/xnu/xnu-1456.1.26/bsd/sys/socket.h
            // 0000 0000 0000 0000
            //
            // Reference:
            //  - Windows: https://msdn.microsoft.com/en-us/library/windows/desktop/ms740506(v=vs.85).aspx
            //  - Linux: https://github.com/torvalds/linux/blob/6a13feb9c82803e2b815eca72fa7a9f5561d7861/include/linux/socket.h
            //  - Linux (sin6_scope_id): https://github.com/torvalds/linux/blob/5924bbecd0267d87c24110cbe2041b5075173a25/net/sunrpc/addr.c#L82
            //  - Apple: http://www.opensource.apple.com/source/xnu/xnu-1456.1.26/bsd/sys/socket.h

            // Quick calculate the port by mask the field and locate the byte 3 and byte 4
            // and then shift them to correct place to form a int.
            var port = ((int)(_field0 & 0x00FF0000) >> 8) | (int)((_field0 & 0xFF000000) >> 24);

            int family = (int)_field0;
            if (Platform.IsDarwin)
            {
                // see explaination in example 4
                family >>= 8;
            }
            family &= 0xFF;

            if (family == 2)
            {
                // AF_INET => IPv4
                return new IPEndPoint(new IPAddress((_field0 >> 32) & 0xFFFFFFFF), port);
            }
            else if (IsIPv4MappedToIPv6())
            {
                var ipv4bits = (_field2 >> 32) & 0x00000000FFFFFFFF;
                return new IPEndPoint(new IPAddress(ipv4bits), port);
            }
            else
            {
                // otherwise IPv6
                var bytes = new byte[16];
                fixed (byte* b = bytes)
                {
                    *((long*)b) = _field1;
                    *((long*)(b + 8)) = _field2;
                }

                return new IPEndPoint(new IPAddress(bytes, ScopeId), port);
            }
        }

        public uint ScopeId
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => (uint)_field3;
            set
            {
                _field3 &= unchecked((long)0xFFFFFFFF00000000);
                _field3 |= value;
            }
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        private bool IsIPv4MappedToIPv6()
        {
            // If the IPAddress is an IPv4 mapped to IPv6, return the IPv4 representation instead.
            // For example [::FFFF:127.0.0.1] will be transform to IPAddress of 127.0.0.1
            if ((ulong)_field1 > 0ul)
            {
                return false;
            }

            return (_field2 & 0xFFFFFFFF) == 0xFFFF0000;
        }
    }

    internal enum uv_udp_flags
    {
        /* Disables dual stack mode. */
        UV_UDP_IPV6ONLY = 1,
        /*
         * Indicates message was truncated because read buffer was too small. The
         * remainder was discarded by the OS. Used in uv_udp_recv_cb.
         */
        UV_UDP_PARTIAL = 2,
        /*
         * Indicates if SO_REUSEADDR will be set when binding the handle in
         * uv_udp_bind.
         * This sets the SO_REUSEPORT socket flag on the BSDs and OS X. On other
         * Unix platforms, it sets the SO_REUSEADDR flag. What that means is that
         * multiple threads or processes can bind to the same address without error
         * (provided they all set the flag) but only the last one to bind will receive
         * any traffic, in effect "stealing" the port from the previous listener.
        */
        UV_UDP_REUSEADDR = 4
    };

    internal enum uv_membership
    {
        UV_LEAVE_GROUP = 0,
        UV_JOIN_GROUP = 1
    }

    internal enum uv_tty_mode_t
    {
        /* Initial/normal terminal mode */
        UV_TTY_MODE_NORMAL = 0,

        /* Raw input mode (On Windows, ENABLE_WINDOW_INPUT is also enabled) */
        UV_TTY_MODE_RAW = 1,

        /* Binary-safe I/O mode for IPC (Unix-only) */
        UV_TTY_MODE_IO
    }

    internal static partial class NativeMethods
    {
        private const int NameBufferSize = 512;

        #region Common

        internal static HandleContext Initialize(IntPtr loopHandle, uv_handle_type handleType, IInternalScheduleHandle target, object[] args)
        {
            Debug.Assert(loopHandle != IntPtr.Zero);
            Debug.Assert(target is object);

            return handleType switch
            {
                uv_handle_type.UV_TIMER => new HandleContext(handleType, s_initializeTimerFunc, loopHandle, target, args),
                uv_handle_type.UV_PREPARE => new HandleContext(handleType, s_initializePrepareFunc, loopHandle, target, args),
                uv_handle_type.UV_CHECK => new HandleContext(handleType, s_initializeCheckFunc, loopHandle, target, args),
                uv_handle_type.UV_IDLE => new HandleContext(handleType, s_initializeIdleFunc, loopHandle, target, args),
                uv_handle_type.UV_ASYNC => new HandleContext(handleType, s_initializeAsyncFunc, loopHandle, target, args),
                uv_handle_type.UV_POLL => new HandleContext(handleType, s_initializePollFunc, loopHandle, target, args),
                uv_handle_type.UV_SIGNAL => new HandleContext(handleType, s_initializeSignalFunc, loopHandle, target, args),
                uv_handle_type.UV_TCP => new HandleContext(handleType, s_initializeTcpFunc, loopHandle, target, args),
                uv_handle_type.UV_NAMED_PIPE => new HandleContext(handleType, s_initializePipeFunc, loopHandle, target, args),
                uv_handle_type.UV_TTY => new HandleContext(handleType, s_initializeTtyFunc, loopHandle, target, args),
                uv_handle_type.UV_UDP => new HandleContext(handleType, s_initializeUdpFunc, loopHandle, target, args),
                uv_handle_type.UV_FS_EVENT => new HandleContext(handleType, s_initializeFSEventFunc, loopHandle, target, args),
                uv_handle_type.UV_FS_POLL => new HandleContext(handleType, s_initializeFSPollFunc, loopHandle, target, args),
                _ => throw ThrowHelper.GetNotSupportedException_Handle_type_to_initialize_not_supported(handleType),
            };
        }

        private static readonly Func<IntPtr, IntPtr, object[], int> s_initializeTimerFunc = (lh, h, args) => InitializeTimer(lh, h/*, args*/);
        private static int InitializeTimer(IntPtr loopHandle, IntPtr handle/*, object[] args*/)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return uv_timer_init(loopHandle, handle);
        }

        private static readonly Func<IntPtr, IntPtr, object[], int> s_initializePrepareFunc = (lh, h, args) => InitializePrepare(lh, h/*, args*/);
        private static int InitializePrepare(IntPtr loopHandle, IntPtr handle/*, object[] args*/)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return uv_prepare_init(loopHandle, handle);
        }

        private static readonly Func<IntPtr, IntPtr, object[], int> s_initializeCheckFunc = (lh, h, args) => InitializeCheck(lh, h/*, args*/);
        private static int InitializeCheck(IntPtr loopHandle, IntPtr handle/*, object[] args*/)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return uv_check_init(loopHandle, handle);
        }

        private static readonly Func<IntPtr, IntPtr, object[], int> s_initializeIdleFunc = (lh, h, args) => InitializeIdle(lh, h/*, args*/);
        private static int InitializeIdle(IntPtr loopHandle, IntPtr handle/*, object[] args*/)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return uv_idle_init(loopHandle, handle);
        }

        private static readonly Func<IntPtr, IntPtr, object[], int> s_initializeAsyncFunc = (lh, h, args) => InitializeAsync(lh, h/*, args*/);
        private static int InitializeAsync(IntPtr loopHandle, IntPtr handle/*, object[] args*/)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return uv_async_init(loopHandle, handle, WorkHandle.WorkCallback);
        }

        private static readonly Func<IntPtr, IntPtr, object[], int> s_initializePollFunc = (lh, h, args) => InitializePoll(lh, h, args);
        private static int InitializePoll(IntPtr loopHandle, IntPtr handle, object[] args)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(args is object && args.Length > 0);

            object arg = args[0];
            if (arg is IntPtr)
            {
                return uv_poll_init_socket(loopHandle, handle, (IntPtr)args[0]);
            }
            else if (arg is int)
            {
                return uv_poll_init(loopHandle, handle, (int)args[0]);
            }

            throw ThrowHelper.GetNotSupportedException_Poll_argument_must_be_either_IntPtr_or_int();
        }

        private static readonly Func<IntPtr, IntPtr, object[], int> s_initializeSignalFunc = (lh, h, args) => InitializeSignal(lh, h/*, args*/);
        private static int InitializeSignal(IntPtr loopHandle, IntPtr handle/*, object[] args*/)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return uv_signal_init(loopHandle, handle);
        }

        private static readonly Func<IntPtr, IntPtr, object[], int> s_initializeTcpFunc = (lh, h, args) => InitializeTcp(lh, h, args);
        private static int InitializeTcp(IntPtr loopHandle, IntPtr handle, object[] args)
        {
            Debug.Assert(handle != IntPtr.Zero);

            if (args is null || 0u >= (uint)args.Length)
            {
                return uv_tcp_init(loopHandle, handle);
            }
            else
            {
                uint flags = (uint)args[0];
                // if flags is specified as AF_INET or AF_INET6, Libuv 
                // creates the socket when tcp handle is created.
                // Otherwise the socket is created when bind to an address.
                return uv_tcp_init_ex(loopHandle, handle, flags);
            }
        }

        private static readonly Func<IntPtr, IntPtr, object[], int> s_initializePipeFunc = (lh, h, args) => InitializePipe(lh, h, args);
        private static int InitializePipe(IntPtr loopHandle, IntPtr handle, object[] args)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(args is object && args.Length > 0);

            bool value = (bool)args[0];
            return uv_pipe_init(loopHandle, handle, value ? 1 : 0);
        }

        private static readonly Func<IntPtr, IntPtr, object[], int> s_initializeTtyFunc = (lh, h, args) => InitializeTty(lh, h, args);
        private static int InitializeTty(IntPtr loopHandle, IntPtr handle, object[] args)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(args is object && args.Length > 0);

            var ttyType = (TtyType)args[0];
            return uv_tty_init(loopHandle, handle, (int)ttyType, ttyType == TtyType.In ? 1 : 0);
        }

        private static readonly Func<IntPtr, IntPtr, object[], int> s_initializeUdpFunc = (lh, h, args) => InitializeUdp(lh, h/*, args*/);
        private static int InitializeUdp(IntPtr loopHandle, IntPtr handle/*, object[] args*/)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return uv_udp_init(loopHandle, handle);
        }

        private static readonly Func<IntPtr, IntPtr, object[], int> s_initializeFSEventFunc = (lh, h, args) => InitializeFSEvent(lh, h/*, args*/);
        private static int InitializeFSEvent(IntPtr loopHandle, IntPtr handle/*, object[] args*/)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return uv_fs_event_init(loopHandle, handle);
        }

        private static readonly Func<IntPtr, IntPtr, object[], int> s_initializeFSPollFunc = (lh, h, args) => InitializeFSPoll(lh, h/*, args*/);
        private static int InitializeFSPoll(IntPtr loopHandle, IntPtr handle/*, object[] args*/)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return uv_fs_poll_init(loopHandle, handle);
        }

        internal static void Start(uv_handle_type handleType, IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);
            var result = handleType switch
            {
                uv_handle_type.UV_PREPARE => uv_prepare_start(handle, WorkHandle.WorkCallback),
                uv_handle_type.UV_CHECK => uv_check_start(handle, WorkHandle.WorkCallback),
                uv_handle_type.UV_IDLE => uv_idle_start(handle, WorkHandle.WorkCallback),
                _ => throw ThrowHelper.GetNotSupportedException_Handle_type_to_start_not_supported(handleType),
            };
            ThrowIfError(result);
        }

        internal static void Stop(uv_handle_type handleType, IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            switch (handleType)
            {
                case uv_handle_type.UV_TIMER:
                    int result = uv_timer_stop(handle);
                    ThrowIfError(result);
                    break;
                case uv_handle_type.UV_PREPARE:
                    uv_prepare_stop(handle);
                    break;
                case uv_handle_type.UV_CHECK:
                    uv_check_stop(handle);
                    break;
                case uv_handle_type.UV_IDLE:
                    uv_idle_stop(handle);
                    break;
                case uv_handle_type.UV_POLL:
                    uv_poll_stop(handle);
                    break;
                case uv_handle_type.UV_SIGNAL:
                    uv_signal_stop(handle);
                    break;
                case uv_handle_type.UV_FS_EVENT:
                    uv_fs_event_stop(handle);
                    break;
                case uv_handle_type.UV_FS_POLL:
                    uv_fs_poll_stop(handle);
                    break;
                default:
                    throw ThrowHelper.GetNotSupportedException_Handle_type_to_stop_not_supported(handleType);
            }
        }

        private static readonly int[] HandleSizeTable;
        private static readonly int[] RequestSizeTable;

        static NativeMethods()
        {
            HandleSizeTable = new[]
            {
                uv_handle_size(uv_handle_type.UV_ASYNC).ToInt32(),
                uv_handle_size(uv_handle_type.UV_CHECK).ToInt32(),
                uv_handle_size(uv_handle_type.UV_FS_EVENT).ToInt32(),
                uv_handle_size(uv_handle_type.UV_FS_POLL).ToInt32(),
                uv_handle_size(uv_handle_type.UV_HANDLE).ToInt32(),
                uv_handle_size(uv_handle_type.UV_IDLE).ToInt32(),
                uv_handle_size(uv_handle_type.UV_NAMED_PIPE).ToInt32(),
                uv_handle_size(uv_handle_type.UV_POLL).ToInt32(),
                uv_handle_size(uv_handle_type.UV_PREPARE).ToInt32(),
                uv_handle_size(uv_handle_type.UV_PROCESS).ToInt32(),
                uv_handle_size(uv_handle_type.UV_STREAM).ToInt32(),
                uv_handle_size(uv_handle_type.UV_TCP).ToInt32(),
                uv_handle_size(uv_handle_type.UV_TIMER).ToInt32(),
                uv_handle_size(uv_handle_type.UV_TTY).ToInt32(),
                uv_handle_size(uv_handle_type.UV_UDP).ToInt32(),
                uv_handle_size(uv_handle_type.UV_SIGNAL).ToInt32(),
                uv_handle_size(uv_handle_type.UV_FILE).ToInt32(),
            };

            RequestSizeTable = new[]
            {
                uv_req_size(uv_req_type.UV_REQ).ToInt32(),
                uv_req_size(uv_req_type.UV_CONNECT).ToInt32(),
                uv_req_size(uv_req_type.UV_WRITE).ToInt32(),
                uv_req_size(uv_req_type.UV_SHUTDOWN).ToInt32(),
                uv_req_size(uv_req_type.UV_UDP_SEND).ToInt32(),
                uv_req_size(uv_req_type.UV_FS).ToInt32(),
                uv_req_size(uv_req_type.UV_WORK).ToInt32(),
                uv_req_size(uv_req_type.UV_GETADDRINFO).ToInt32(),
                uv_req_size(uv_req_type.UV_GETNAMEINFO).ToInt32()
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetSize(uv_handle_type handleType) =>
            HandleSizeTable[unchecked((int)handleType - 1)];

        #endregion Common

        #region Udp

        internal static void UdpReceiveStart(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_udp_recv_start(handle, Udp.AllocateCallback, Udp.ReceiveCallback);
            ThrowIfError(result);
        }

        internal static void UdpReceiveStop(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_udp_recv_stop(handle);
            ThrowIfError(result);
        }

        internal static unsafe void UdpSend(IntPtr requestHandle, IntPtr handle, IPEndPoint remoteEndPoint, uv_buf_t* bufs, ref int size)
        {
            Debug.Assert(requestHandle != IntPtr.Zero);
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(remoteEndPoint is object);

            GetSocketAddress(remoteEndPoint, out sockaddr addr);

            int result = uv_udp_send(
                requestHandle,
                handle,
                bufs,
                size,
                ref addr,
                WriteRequest.WriteCallback);
            ThrowIfError(result);
        }

        internal static void UdpTrySend(IntPtr handle, IPEndPoint remoteEndPoint, ref uv_buf_t buf)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(remoteEndPoint is object);

            GetSocketAddress(remoteEndPoint, out sockaddr addr);

            var bufs = new[] { buf };
            int result = uv_udp_try_send(handle, bufs, bufs.Length, ref addr);
            ThrowIfError(result);
        }

        internal static void UdpSetMembership(IntPtr handle, IPAddress multicastAddress, IPAddress interfaceAddress, uv_membership membership)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(multicastAddress is object);

            string multicast_addr = multicastAddress.ToString();
            string interface_addr = interfaceAddress?.ToString();

            int result = uv_udp_set_membership(handle, multicast_addr, interface_addr, membership);
            ThrowIfError(result);
        }

        internal static void UdpSetMulticastInterface(IntPtr handle, IPAddress interfaceAddress)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(interfaceAddress is object);

            string ip = interfaceAddress.ToString();
            int result = uv_udp_set_multicast_interface(handle, ip);
            ThrowIfError(result);
        }

        internal static void UdpBind(IntPtr handle, IPEndPoint endPoint, bool reuseAddress, bool dualStack)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(endPoint is object);

            GetSocketAddress(endPoint, out sockaddr addr);

            uint flag = 0;
            if (reuseAddress)
            {
                flag = (uint)uv_udp_flags.UV_UDP_REUSEADDR;
            }
            else
            {
                if (!dualStack
                    && endPoint.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    flag = (uint)uv_udp_flags.UV_UDP_IPV6ONLY;
                }
            }

            int result = uv_udp_bind(handle, ref addr, flag);
            ThrowIfError(result);
        }

        internal static IPEndPoint UdpGetSocketName(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int namelen = Marshal.SizeOf<sockaddr>();
            int result = uv_udp_getsockname(handle, out sockaddr sockaddr, ref namelen);
            ThrowIfError(result);

            return sockaddr.GetIPEndPoint();
        }

        internal static void UpdSetMulticastLoopback(IntPtr handle, bool value)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_udp_set_multicast_loop(handle, value ? 1 : 0);
            ThrowIfError(result);
        }

        internal static void UdpSetMulticastTtl(IntPtr handle, int value)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_udp_set_multicast_ttl(handle, value);
            ThrowIfError(result);
        }

        internal static void UdpSetTtl(IntPtr handle, int value)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_udp_set_ttl(handle, value);
            ThrowIfError(result);
        }

        internal static void UdpSetBroadcast(IntPtr handle, bool value)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_udp_set_broadcast(handle, value ? 1 : 0);
            ThrowIfError(result);
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_udp_init(IntPtr loopHandle, IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_udp_bind(IntPtr handle, ref sockaddr sockaddr, uint flags);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_udp_getsockname(IntPtr handle, out sockaddr sockaddr, ref int namelen);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_udp_set_multicast_loop(IntPtr handle, int on /* – 1 for on, 0 for off */);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_udp_set_multicast_ttl(IntPtr handle, int ttl /* – 1 through 255 */);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_udp_set_ttl(IntPtr handle, int ttl /* – 1 through 255 */);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_udp_set_broadcast(IntPtr handle, int on /* – 1 for on, 0 for off */);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_udp_set_multicast_interface(IntPtr handle, string interface_addr);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_udp_set_membership(IntPtr handle, string multicast_addr, string interface_addr, uv_membership membership);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_udp_try_send(IntPtr handle, uv_buf_t[] bufs, int nbufs, ref sockaddr addr);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_udp_recv_start(IntPtr handle, uv_alloc_cb alloc_cb, uv_udp_recv_cb recv_cb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_udp_recv_stop(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int uv_udp_send(IntPtr req, IntPtr handle, uv_buf_t* bufs, int nbufs, ref sockaddr addr, uv_watcher_cb cb);

        #endregion Udp

        #region Pipe

        internal static void PipeBind(IntPtr handle, string name)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(!string.IsNullOrEmpty(name));

            int result = uv_pipe_bind(handle, name);
            ThrowIfError(result);
        }

        internal static void PipeConnect(IntPtr requestHandle, IntPtr handle, string remoteName)
        {
            Debug.Assert(requestHandle != IntPtr.Zero);
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(!string.IsNullOrEmpty(remoteName));

            uv_pipe_connect(requestHandle, handle, remoteName, WatcherRequest.WatcherCallback);
        }

        internal static unsafe string PipeGetSocketName(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            var buf = stackalloc byte[NameBufferSize];
            var ptr = (IntPtr)buf;
            var length = (IntPtr)NameBufferSize;

            int result = uv_pipe_getsockname(handle, ptr, ref length);
            ThrowIfError(result);

            string socketName = Marshal.PtrToStringAnsi(ptr, length.ToInt32());

            return socketName;
        }

        internal static unsafe string PipeGetPeerName(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            var buf = stackalloc byte[NameBufferSize];
            var ptr = (IntPtr)buf;
            var length = (IntPtr)NameBufferSize;

            int result = uv_pipe_getpeername(handle, ptr, ref length);
            ThrowIfError(result);

            string peerName = Marshal.PtrToStringAnsi(ptr, length.ToInt32());

            return peerName;
        }

        internal static void PipePendingInstances(IntPtr handle, int count)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(count > 0);

            uv_pipe_pending_instances(handle, count);
        }

        internal static int PipePendingCount(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return uv_pipe_pending_count(handle);
        }

        internal static uv_handle_type PipePendingType(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return (uv_handle_type)uv_pipe_pending_type(handle);
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int uv_pipe_init(IntPtr loop, IntPtr handle, int ipc);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int uv_pipe_bind(IntPtr handle, string name);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void uv_pipe_connect(IntPtr req, IntPtr handle, string name, uv_watcher_cb connect_cb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_pipe_getsockname(IntPtr handle, IntPtr buffer, ref IntPtr size);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_pipe_getpeername(IntPtr handle, IntPtr buffer, ref IntPtr size);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void uv_pipe_pending_instances(IntPtr handle, int count);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_pipe_pending_count(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_pipe_pending_type(IntPtr handle);

        #endregion Pipe

        #region TCP

        internal static void TcpSetNoDelay(IntPtr handle, bool value)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_tcp_nodelay(handle, value ? 1 : 0);
            ThrowIfError(result);
        }

        internal static void TcpSetKeepAlive(IntPtr handle, bool value, int delay)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(delay >= 0);

            int result = uv_tcp_keepalive(handle, value ? 1 : 0, delay);
            ThrowIfError(result);
        }

        internal static void TcpSimultaneousAccepts(IntPtr handle, bool value)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_tcp_simultaneous_accepts(handle, value ? 1 : 0);
            ThrowIfError(result);
        }

        internal static void TcpBind(IntPtr handle, IPEndPoint endPoint, bool dualStack /* Both IPv4 & IPv6 */)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(endPoint is object);

            GetSocketAddress(endPoint, out sockaddr addr);

            int result = uv_tcp_bind(handle, ref addr, (uint)(dualStack ? 1 : 0));
            ThrowIfError(result);
        }

        internal static void TcpConnect(IntPtr requestHandle, IntPtr handle, IPEndPoint endPoint)
        {
            Debug.Assert(requestHandle != IntPtr.Zero);
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(endPoint is object);

            GetSocketAddress(endPoint, out sockaddr addr);

            int result = uv_tcp_connect(requestHandle, handle, ref addr, WatcherRequest.WatcherCallback);
            ThrowIfError(result);
        }

        internal static IPEndPoint TcpGetSocketName(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int namelen = Marshal.SizeOf<sockaddr>();
            uv_tcp_getsockname(handle, out sockaddr sockaddr, ref namelen);

            return sockaddr.GetIPEndPoint();
        }

        internal static IPEndPoint TcpGetPeerName(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int namelen = Marshal.SizeOf<sockaddr>();
            int result = uv_tcp_getpeername(handle, out sockaddr sockaddr, ref namelen);
            ThrowIfError(result);

            return sockaddr.GetIPEndPoint();
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_tcp_connect(IntPtr req, IntPtr handle, ref sockaddr sockaddr, uv_watcher_cb connect_cb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_tcp_init(IntPtr loopHandle, IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_tcp_init_ex(IntPtr loopHandle, IntPtr handle, uint flags);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_tcp_bind(IntPtr handle, ref sockaddr sockaddr, uint flags);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_tcp_getsockname(IntPtr handle, out sockaddr sockaddr, ref int namelen);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_tcp_getpeername(IntPtr handle, out sockaddr name, ref int namelen);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_tcp_nodelay(IntPtr handle, int enable);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_tcp_keepalive(IntPtr handle, int enable, int delay);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_tcp_simultaneous_accepts(IntPtr handle, int enable);

        #endregion TCP

        #region Tty

        internal static void TtySetMode(IntPtr handle, TtyMode ttyMode)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_tty_set_mode(handle, (uv_tty_mode_t)ttyMode);
            ThrowIfError(result);
        }

        internal static void TtyResetMode()
        {
            // To be called when the program exits. 
            // Resets TTY settings to default values for the next process to take over.
            int result = uv_tty_reset_mode();
            ThrowIfError(result);
        }

        internal static void TtyWindowSize(IntPtr handle, out int width, out int height)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_tty_get_winsize(handle, out width, out height);
            ThrowIfError(result);
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_tty_init(IntPtr loopHandle, IntPtr handle, int fd, int readable);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_tty_set_mode(IntPtr handle, uv_tty_mode_t mode);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_tty_reset_mode();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_tty_get_winsize(IntPtr handle, out int width, out int height);

        #endregion Tty

        #region Timer

        internal static void Start(IntPtr handle, long timeout, long repeat)
        {
            Debug.Assert(handle != IntPtr.Zero);
            Debug.Assert(timeout >= 0);
            Debug.Assert(repeat >= 0);

            int result = uv_timer_start(handle, WorkHandle.WorkCallback, timeout, repeat);
            ThrowIfError(result);
        }

        internal static void Again(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_timer_again(handle);
            ThrowIfError(result);
        }

        internal static void SetTimerRepeat(IntPtr handle, long repeat)
        {
            Debug.Assert(handle != IntPtr.Zero);

            uv_timer_set_repeat(handle, repeat);
        }

        internal static long GetTimerRepeat(IntPtr handle)
        {
            Debug.Assert(handle != IntPtr.Zero);

            return uv_timer_get_repeat(handle);
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_timer_init(IntPtr loopHandle, IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_timer_start(IntPtr handle, uv_work_cb work_cb, long timeout, long repeat);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_timer_stop(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_timer_again(IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void uv_timer_set_repeat(IntPtr handle, long repeat);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern long uv_timer_get_repeat(IntPtr handle);

        #endregion Timer

        #region Prepare

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_prepare_init(IntPtr loopHandle, IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_prepare_start(IntPtr handle, uv_work_cb prepare_cb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void uv_prepare_stop(IntPtr handle);

        #endregion Prepare

        #region Check

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_check_init(IntPtr loopHandle, IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_check_start(IntPtr handle, uv_work_cb check_cb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_check_stop(IntPtr handle);

        #endregion Check

        #region Idle

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_idle_init(IntPtr loopHandle, IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_idle_start(IntPtr handle, uv_work_cb check_cb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_idle_stop(IntPtr handle);

        #endregion Idle

        #region Async

        internal static void Send(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            int result = uv_async_send(handle);
            ThrowIfError(result);
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_async_init(IntPtr loopHandle, IntPtr handle, uv_work_cb async_cb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_async_send(IntPtr handle);

        #endregion Async

        #region Poll

        // Calling uv_poll_start() on a handle that is already active is fine. 
        // Doing so will update the events mask that is being watched for.
        internal static void PollStart(IntPtr handle, PollMask mask)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_poll_start(handle, (int)mask, Poll.PollCallback);
            ThrowIfError(result);
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_poll_init(IntPtr loop, IntPtr handle, int fd);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_poll_init_socket(IntPtr loop, IntPtr handle, IntPtr socket);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_poll_start(IntPtr handle, int events, uv_poll_cb cb);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_poll_stop(IntPtr handle);

        #endregion Poll

        #region Signal

        internal static void SignalStart(IntPtr handle, int signum)
        {
            Debug.Assert(handle != IntPtr.Zero);

            int result = uv_signal_start(handle, Signal.SignalCallback, signum);
            ThrowIfError(result);
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_signal_init(IntPtr loop, IntPtr handle);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_signal_start(IntPtr handle, uv_watcher_cb cb, int signum);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_signal_stop(IntPtr handle);

        #endregion Signal

        #region Common

        internal static void GetSocketAddress(IPEndPoint endPoint, out sockaddr addr)
        {
            Debug.Assert(endPoint is object);

            string ip = endPoint.Address.ToString();
            var result = endPoint.AddressFamily switch
            {
                AddressFamily.InterNetwork => uv_ip4_addr(ip, endPoint.Port, out addr),
                AddressFamily.InterNetworkV6 => uv_ip6_addr(ip, endPoint.Port, out addr),
                _ => throw ThrowHelper.GetNotSupportedException_expecting_InterNetworkkV6OrV4(endPoint),
            };
            ThrowIfError(result);
        }

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_ip4_addr(string ip, int port, out sockaddr address);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int uv_ip6_addr(string ip, int port, out sockaddr address);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr uv_handle_size(uv_handle_type handleType);

        #endregion Common
    }
#pragma warning restore IDE1006 // 命名样式
}
