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
    using System.Diagnostics;
    using System.Net;
    using DotNetty.Buffers;
    using DotNetty.Transport.Libuv.Native;
    using DotNetty.Transport.Libuv.Requests;

    public sealed class Udp : ScheduleHandle<Udp>
    {
        private const int FixedBufferSize = 2048;

        internal static readonly uv_alloc_cb AllocateCallback = OnAllocateCallback;
        internal static readonly uv_udp_recv_cb ReceiveCallback = OnReceiveCallback;

        private readonly PooledByteBufferAllocator _allocator;
        private readonly PendingRead _pendingRead;
        private Action<Udp, IDatagramReadCompletion> _readAction;

        internal Udp(LoopContext loop)
            : this(loop, PooledByteBufferAllocator.Default)
        { }

        internal Udp(LoopContext loop, PooledByteBufferAllocator allocator)
            : base(loop, uv_handle_type.UV_UDP)
        {
            if (allocator is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.allocator); }

            _allocator = allocator;
            _pendingRead = new PendingRead();
        }

        public int GetSendBufferSize()
        {
            Validate();
            return NativeMethods.SendBufferSize(InternalHandle, 0);
        }

        public int SetSendBufferSize(int value)
        {
            if ((uint)(value - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(value, ExceptionArgument.value); }

            Validate();
            return NativeMethods.SendBufferSize(InternalHandle, value);
        }

        public int GetReceiveBufferSize()
        {
            Validate();
            return NativeMethods.ReceiveBufferSize(InternalHandle, 0);
        }

        public int SetReceiveBufferSize(int value)
        {
            if ((uint)(value - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(value, ExceptionArgument.value); }

            Validate();
            return NativeMethods.ReceiveBufferSize(InternalHandle, value);
        }

        public void GetFileDescriptor(ref IntPtr value)
        {
            Validate();
            NativeMethods.GetFileDescriptor(InternalHandle, ref value);
        }

        public WritableBuffer Allocate()
        {
            IByteBuffer buffer = _allocator.Buffer();
            return new WritableBuffer(buffer);
        }

        public void OnReceive(Action<Udp, IDatagramReadCompletion> action)
        {
            if (action is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.action); }

            if (_readAction is object)
            {
                ThrowHelper.ThrowInvalidOperationException_Udp_data_handler_has_already_been_registered();
            }

            _readAction = action;
        }

        public void ReceiveStart()
        {
            Validate();
            NativeMethods.UdpReceiveStart(InternalHandle);
#if DEBUG
            if (Log.TraceEnabled)
            {
                Log.Trace("{} {} receive started", HandleType, InternalHandle);
            }
#endif
        }

        public void ReceiveStop()
        {
            Validate();
            NativeMethods.UdpReceiveStop(InternalHandle);
#if DEBUG
            if (Log.TraceEnabled)
            {
                Log.Trace("{} {} receive stopped", HandleType, InternalHandle);
            }
#endif
        }

        public void QueueSend(byte[] array,
            IPEndPoint remoteEndPoint,
            Action<Udp, Exception> completion = null)
        {
            QueueSend(array, 0, array.Length, remoteEndPoint, completion);
        }

        public void QueueSend(byte[] array, int offset, int count,
            IPEndPoint remoteEndPoint,
            Action<Udp, Exception> completion = null)
        {
            if (array is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array); }

            if (0u >= (uint)count) { return; }

            IByteBuffer buffer = Unpooled.WrappedBuffer(array, offset, count);
            QueueSend(buffer, remoteEndPoint, completion);
        }

        public void QueueSend(WritableBuffer writableBuffer,
            IPEndPoint remoteEndPoint,
            Action<Udp, Exception> completion = null)
        {
            IByteBuffer buffer = writableBuffer.GetBuffer();
            if (buffer is null || !buffer.IsReadable()) { return; }

            QueueSend(buffer, remoteEndPoint, completion);
        }

        private unsafe void QueueSend(IByteBuffer buffer,
            IPEndPoint remoteEndPoint,
            Action<Udp, Exception> completion)
        {
            if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }
            if (remoteEndPoint is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.remoteEndPoint); }

            WriteRequest request = Loop.SendRequestPool.Take();
            try
            {
                request.Prepare(buffer,
                    (sendRequest, exception) => completion?.Invoke(this, exception));

                NativeMethods.UdpSend(
                    request.InternalHandle,
                    InternalHandle,
                    remoteEndPoint,
                    request.Bufs,
                    ref request.Size);
            }
            catch (Exception exception)
            {
                request.Release();
                Log.Handle_faulted(HandleType, exception);
                throw;
            }
        }

        public void TrySend(IPEndPoint remoteEndPoint, byte[] array)
        {
            if (array is null) { return; }
            TrySend(remoteEndPoint, array, 0, array.Length);
        }

        public unsafe void TrySend(IPEndPoint remoteEndPoint, byte[] array, int offset, int count)
        {
            if (remoteEndPoint is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.remoteEndPoint); }
            if (array is null || 0u >= (uint)array.Length) { return; }
            if ((uint)count > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count); }
            if ((uint)(offset + count) > (uint)array.Length) { ThrowHelper.ThrowArgumentException_InvalidOffLen(); }

            Validate();
            try
            {
                fixed (byte* memory = array)
                {
                    var buf = new uv_buf_t((IntPtr)memory + offset, count);
                    NativeMethods.UdpTrySend(InternalHandle, remoteEndPoint, ref buf);
                }
            }
#if DEBUG
            catch (Exception exception)
            {
                if (Log.DebugEnabled) { Log.Debug($"{HandleType} Trying to send data to {remoteEndPoint} failed.", exception); }
#else
            catch (Exception)
            {
#endif
                throw;
            }
        }

        public Udp JoinGroup(IPAddress multicastAddress, IPAddress interfaceAddress = null)
        {
            if (multicastAddress is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.multicastAddress); }

            SetMembership(multicastAddress, interfaceAddress, uv_membership.UV_JOIN_GROUP);
            return this;
        }

        public Udp LeaveGroup(IPAddress multicastAddress, IPAddress interfaceAddress = null)
        {
            if (multicastAddress is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.multicastAddress); }

            SetMembership(multicastAddress, interfaceAddress, uv_membership.UV_LEAVE_GROUP);
            return this;
        }

        private void SetMembership(IPAddress multicastAddress, IPAddress interfaceAddress, uv_membership membership)
        {
            Validate();
            NativeMethods.UdpSetMembership(InternalHandle,
                multicastAddress,
                interfaceAddress,
                membership);
        }

        public Udp Bind(IPEndPoint endPoint, bool reuseAddress = false, bool dualStack = false)
        {
            if (endPoint is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.endPoint); }

            Validate();
            NativeMethods.UdpBind(InternalHandle, endPoint, reuseAddress, dualStack);

            return this;
        }

        public IPEndPoint GetLocalEndPoint()
        {
            Validate();
            return NativeMethods.UdpGetSocketName(InternalHandle);
        }

        public Udp MulticastInterface(IPAddress interfaceAddress)
        {
            if (interfaceAddress is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.interfaceAddress); }

            Validate();
            NativeMethods.UdpSetMulticastInterface(InternalHandle, interfaceAddress);

            return this;
        }

        public Udp MulticastLoopback(bool value)
        {
            Validate();
            NativeMethods.UpdSetMulticastLoopback(InternalHandle, value);

            return this;
        }

        public Udp MulticastTtl(int value)
        {
            Validate();
            NativeMethods.UdpSetMulticastTtl(InternalHandle, value);

            return this;
        }

        public Udp Ttl(int value)
        {
            Validate();
            NativeMethods.UdpSetTtl(InternalHandle, value);

            return this;
        }

        public Udp Broadcast(bool value)
        {
            Validate();
            NativeMethods.UdpSetBroadcast(InternalHandle, value);

            return this;
        }

        private void OnReceivedCallback(IByteBuffer byteBuffer, int status, IPEndPoint remoteEndPoint)
        {
            Debug.Assert(byteBuffer is object);

            // status (nread) 
            //     Number of bytes that have been received. 
            //     0 if there is no more data to read. You may discard or repurpose the read buffer. 
            //     Note that 0 may also mean that an empty datagram was received (in this case addr is not NULL). 
            //     < 0 if a transmission error was detected.

            // For status = 0 (Nothing to read)
            if (status >= 0)
            {
#if DEBUG
                if (Log.DebugEnabled)
                {
                    Log.Debug("{} {} read, buffer length = {} status = {}.",
                        HandleType, InternalHandle, byteBuffer.Capacity, status);
                }
#endif

                InvokeRead(byteBuffer, status, remoteEndPoint);
            }
            else
            {
                Exception exception = NativeMethods.CreateError((uv_err_code)status);
                Log.Handle_read_error(HandleType, InternalHandle, status, exception);
                InvokeRead(byteBuffer, 0, remoteEndPoint, exception);
            }
        }

        private void InvokeRead(IByteBuffer byteBuffer, int size, IPEndPoint remoteEndPoint, Exception error = null)
        {
            if ((uint)(size - 1) > SharedConstants.TooBigOrNegative) // <= 0
            {
                byteBuffer.Release();

                if (error is null && 0u >= (uint)size)
                {
                    // Filter out empty data received if not an error
                    //
                    // On windows the udp receive actually been call with empty data 
                    // for broadcast, on Linux, the receive is not called at all.
                    //
                    return;
                }
            }

            ReadableBuffer buffer = size > 0 ? new ReadableBuffer(byteBuffer, size) : ReadableBuffer.Empty;
            var completion = new DatagramReadCompletion(ref buffer, error, remoteEndPoint);
            try
            {
                _readAction?.Invoke(this, completion);
            }
            catch (Exception exception)
            {
                Log.Udp_Exception_whilst_invoking_read_callback(exception);
            }
            finally
            {
                completion.Dispose();
            }
        }

        // addr: 
        //     struct sockaddr ontaining the address of the sender. 
        //     Can be NULL. Valid for the duration of the callback only.
        //
        // flags: 
        //     One or more or’ed UV_UDP_* constants. 
        //     Right now only UV_UDP_PARTIAL is used
        private static void OnReceiveCallback(IntPtr handle, IntPtr nread, ref uv_buf_t buf, ref sockaddr addr, int flags)
        {
            var udp = HandleContext.GetTarget<Udp>(handle);
            IByteBuffer byteBuffer = udp.GetBuffer();

            int count = (int)nread.ToInt64();
            IPEndPoint remoteEndPoint = count > 0 ? addr.GetIPEndPoint() : null;

            //
            // Indicates message was truncated because read buffer was too small. 
            // The remainder was discarded by the OS. Used in uv_udp_recv_cb.
            // 
            if (flags == (int)uv_udp_flags.UV_UDP_PARTIAL)
            {
                Log.Handle_receive_result_truncated(handle, byteBuffer);
            }

            udp.OnReceivedCallback(byteBuffer, count, remoteEndPoint);
        }

        private void OnAllocateCallback(out uv_buf_t buf)
        {
            IByteBuffer buffer = _allocator.Buffer(FixedBufferSize);
#if DEBUG
            if (Log.TraceEnabled)
            {
                Log.Trace("{} {} receive buffer allocated size = {}", HandleType, InternalHandle, buffer.Capacity);
            }
#endif

            buf = _pendingRead.GetBuffer(buffer);
        }

        private IByteBuffer GetBuffer()
        {
            IByteBuffer byteBuffer = _pendingRead.Buffer;
            _pendingRead.Reset();
            return byteBuffer;
        }

        private static void OnAllocateCallback(IntPtr handle, IntPtr suggestedSize, out uv_buf_t buf)
        {
            var udp = HandleContext.GetTarget<Udp>(handle);
            udp.OnAllocateCallback(out buf);
        }

        protected override void Close()
        {
            _readAction = null;
            _pendingRead.Dispose();
        }

        private sealed class DatagramReadCompletion : ReadCompletion, IDatagramReadCompletion
        {
            internal DatagramReadCompletion(ref ReadableBuffer data, Exception error, IPEndPoint remoteEndPoint)
                : base(ref data, error)
            {
                RemoteEndPoint = remoteEndPoint;
            }

            public IPEndPoint RemoteEndPoint { get; }
        }
    }
}
