// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
#if DESKTOPCLR
    using System.Threading;
#endif

    public sealed class SocketDatagramChannel : SocketDatagramChannel<SocketDatagramChannel>
    {
        public SocketDatagramChannel() : base() { }

        public SocketDatagramChannel(AddressFamily addressFamily) : base(addressFamily) { }

        public SocketDatagramChannel(Socket socket) : base(socket) { }
    }

    public partial class SocketDatagramChannel<TChannel> : AbstractSocketMessageChannel<TChannel, SocketDatagramChannel<TChannel>.DatagramChannelUnsafe>, IDatagramChannel
        where TChannel : SocketDatagramChannel<TChannel>
    {
        //static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<SocketDatagramChannel>();
        private static readonly Action<object, object> ReceiveFromCompletedSyncCallback = OnReceiveFromCompletedSync;
        private static readonly ChannelMetadata ChannelMetadata = new ChannelMetadata(true);

        private readonly DefaultDatagramChannelConfig _config;
        private readonly IPEndPoint _anyRemoteEndPoint;

        public SocketDatagramChannel()
            : this(new Socket(SocketType.Dgram, ProtocolType.Udp))
        {
        }

        public SocketDatagramChannel(AddressFamily addressFamily)
            : this(new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp))
        {
        }

        public SocketDatagramChannel(Socket socket)
            : base(null, socket)
        {
            _config = new DefaultDatagramChannelConfig(this, socket);
            _anyRemoteEndPoint = new IPEndPoint(
                socket.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any,
                IPEndPoint.MinPort);
        }

        public override IChannelConfiguration Configuration => _config;

        public override ChannelMetadata Metadata => ChannelMetadata;

        protected override EndPoint LocalAddressInternal => Socket.LocalEndPoint;

        protected override EndPoint RemoteAddressInternal => Socket.RemoteEndPoint;

        protected override void DoBind(EndPoint localAddress)
        {
            Socket.Bind(localAddress);
            _ = CacheLocalAddress();

            SetState(StateFlags.Active);
        }

        public override bool Active => Open && Socket.IsBound;

        protected override bool DoConnect(EndPoint remoteAddress, EndPoint localAddress)
        {
            if (localAddress is object)
            {
                DoBind(localAddress);
            }

            bool success = false;
            try
            {
                Socket.Connect(remoteAddress);
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                {
                    DoClose();
                }
            }
        }

        protected override void DoFinishConnect(SocketChannelAsyncOperation<TChannel, DatagramChannelUnsafe> operation)
        {
            throw new NotSupportedException();
        }

        protected override void DoDisconnect() => DoClose();

        protected override void DoClose()
        {
            if (TryResetState(StateFlags.Open | StateFlags.Active))
            {
                Socket.SafeClose(); //this.Socket.Dispose();
            }
        }

        protected override void ScheduleSocketRead()
        {
            var operation = ReadOperation;
            operation.RemoteEndPoint = _anyRemoteEndPoint;

            IRecvByteBufAllocatorHandle handle = Unsafe.RecvBufAllocHandle;
            IByteBuffer buffer = handle.Allocate(_config.Allocator);
            handle.AttemptedBytesRead = buffer.WritableBytes;
            operation.UserToken = buffer;

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
            operation.SetBuffer(buffer.FreeMemory);
#else
            ArraySegment<byte> bytes = buffer.GetIoBuffer(0, buffer.WritableBytes);
            operation.SetBuffer(bytes.Array, bytes.Offset, bytes.Count);
#endif

            bool pending;
#if NETCOREAPP || NETSTANDARD
            pending = Socket.ReceiveFromAsync(operation);
#else
            if (ExecutionContext.IsFlowSuppressed())
            {
                pending = Socket.ReceiveFromAsync(operation);
            }
            else
            {
                using (ExecutionContext.SuppressFlow())
                {
                    pending = Socket.ReceiveFromAsync(operation);
                }
            }
#endif
            if (!pending)
            {
                EventLoop.Execute(ReceiveFromCompletedSyncCallback, Unsafe, operation);
            }
        }

        protected override int DoReadMessages(List<object> buf)
        {
            if (buf is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buf); }

            var operation = ReadOperation;
            var data = (IByteBuffer)operation.UserToken;
            bool free = true;

            try
            {
                IRecvByteBufAllocatorHandle handle = Unsafe.RecvBufAllocHandle;

                int received = operation.BytesTransferred;
                if ((uint)(received - 1) > SharedConstants.TooBigOrNegative) // <= 0
                {
                    return 0;
                }

                handle.LastBytesRead = received;
                _ = data.SetWriterIndex(data.WriterIndex + received);
                EndPoint remoteAddress = operation.RemoteEndPoint;
                buf.Add(new DatagramPacket(data, remoteAddress, LocalAddress));
                free = false;

                return 1;
            }
            finally
            {
                if (free)
                {
                    _ = data.Release();
                }

                operation.UserToken = null;
            }
        }

        static void OnReceiveFromCompletedSync(object u, object p) => ((DatagramChannelUnsafe)u).FinishRead((SocketChannelAsyncOperation<TChannel, DatagramChannelUnsafe>)p);

        protected override void ScheduleMessageWrite(object message)
        {
            var envelope = message as IAddressedEnvelope<IByteBuffer>;
            if (envelope is null)
            {
                throw new InvalidOperationException(
                    $"Unexpected type: {message.GetType().FullName}, expecting DatagramPacket or IAddressedEnvelope.");
            }

            IByteBuffer data = envelope.Content;
            int length = data.ReadableBytes;
            if (0u >= (uint)length)
            {
                return;
            }

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
            var operation = PrepareWriteOperation(data.GetReadableMemory(data.ReaderIndex, length));
#else
            var operation = PrepareWriteOperation(data.GetIoBuffer(data.ReaderIndex, length));
#endif
            operation.RemoteEndPoint = envelope.Recipient;
            SetState(StateFlags.WriteScheduled);
            bool pending = Socket.SendToAsync(operation);
            if (!pending)
            {
                Unsafe.FinishWrite(operation);
            }
        }

        //protected override IChannelUnsafe NewUnsafe() => new DatagramChannelUnsafe(this); ## 苦竹 屏蔽 ##

        protected override bool DoWriteMessage(object msg, ChannelOutboundBuffer input)
        {
            EndPoint remoteAddress = null;
            IByteBuffer data = null;

            if (msg is IAddressedEnvelope<IByteBuffer> envelope)
            {
                remoteAddress = envelope.Recipient;
                data = envelope.Content;
            }
            else if (msg is IByteBuffer buffer)
            {
                data = buffer;//  (IByteBuffer)msg;
                remoteAddress = RemoteAddressInternal;
            }

            if (data is null || remoteAddress is null)
            {
                return false;
            }

            int length = data.ReadableBytes;
            if (0u >= (uint)length)
            {
                return true;
            }

            ArraySegment<byte> bytes = data.GetIoBuffer(data.ReaderIndex, length);
            int writtenBytes = Socket.SendTo(bytes.Array, bytes.Offset, bytes.Count, SocketFlags.None, remoteAddress);

            return writtenBytes > 0;
        }

        protected override object FilterOutboundMessage(object msg)
        {
            if (msg is DatagramPacket packet)
            {
                return packet.Content.IsSingleIoBuffer
                    ? packet
                    : new DatagramPacket(CreateNewDirectBuffer(packet, packet.Content), packet.Recipient);
            }

            if (msg is IByteBuffer buffer)
            {
                return buffer.IsSingleIoBuffer
                    ? buffer
                    : CreateNewDirectBuffer(buffer);
            }

            if (msg is IAddressedEnvelope<IByteBuffer> envolope)
            {
                if (envolope.Content.IsSingleIoBuffer)
                {
                    return envolope;
                }

                return new DefaultAddressedEnvelope<IByteBuffer>(
                    CreateNewDirectBuffer(envolope, envolope.Content), envolope.Recipient);
            }

            throw new NotSupportedException(
                $"Unsupported message type: {msg.GetType()}, expecting instances of DatagramPacket, IByteBuffer or IAddressedEnvelope.");
        }

        IByteBuffer CreateNewDirectBuffer(IByteBuffer buffer)
        {
            if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }

            int readableBytes = buffer.ReadableBytes;
            if (0u >= (uint)readableBytes)
            {
                buffer.SafeRelease();
                return Unpooled.Empty;
            }

            // Composite
            IByteBuffer data = Allocator.Buffer(readableBytes);
            _ = data.WriteBytes(buffer, buffer.ReaderIndex, readableBytes);
            buffer.SafeRelease();

            return data;
        }

        IByteBuffer CreateNewDirectBuffer(IReferenceCounted holder, IByteBuffer buffer)
        {
            if (holder is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.holder); }
            if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }

            int readableBytes = buffer.ReadableBytes;
            if (0u >= (uint)readableBytes)
            {
                holder.SafeRelease();
                return Unpooled.Empty;
            }

            // Composite
            IByteBuffer data = Allocator.Buffer(readableBytes);
            _ = data.WriteBytes(buffer, buffer.ReaderIndex, readableBytes);
            holder.SafeRelease();

            return data;
        }

        ////
        //// Checks if the specified buffer is a direct buffer and is composed of a single NIO buffer.
        //// (We check this because otherwise we need to make it a non-composite buffer.)
        ////
        //static bool IsSingleBuffer(IByteBuffer buffer)
        //{
        //    if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }
        //    return buffer.IsSingleIoBuffer;
        //}

        // Continue on write error as a SocketDatagramChannel can write to multiple remote peers
        // See https://github.com/netty/netty/issues/2665
        protected override bool ContinueOnWriteError => true;

        public bool IsConnected() => Socket.Connected;

        public Task JoinGroup(IPEndPoint multicastAddress) => JoinGroup(multicastAddress, null, null, NewPromise());

        public Task JoinGroup(IPEndPoint multicastAddress, IPromise promise) => JoinGroup(multicastAddress, null, null, promise);

        public Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface) => JoinGroup(multicastAddress, networkInterface, null, NewPromise());

        public Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPromise promise) => JoinGroup(multicastAddress, networkInterface, null, NewPromise());

        public Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source) => JoinGroup(multicastAddress, networkInterface, source, NewPromise());

        public Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source, IPromise promise)
        {
            if (EventLoop.InEventLoop)
            {
                DoJoinGroup(multicastAddress, networkInterface, source, promise);
            }
            else
            {
                try
                {
                    EventLoop.Execute(() => DoJoinGroup(multicastAddress, networkInterface, source, promise));
                }
                catch (Exception ex)
                {
                    Util.SafeSetFailure(promise, ex, Logger);
                }
            }

            return promise.Task;
        }

        void DoJoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source, IPromise promise)
        {
            try
            {
                Socket.SetSocketOption(
                    _config.AddressFamilyOptionLevel,
                    SocketOptionName.AddMembership,
                    CreateMulticastOption(multicastAddress, networkInterface, source));

                promise.Complete();
            }
            catch (Exception exception)
            {
                Util.SafeSetFailure(promise, exception, Logger);
            }
        }

        public Task LeaveGroup(IPEndPoint multicastAddress) => LeaveGroup(multicastAddress, null, null, NewPromise());

        public Task LeaveGroup(IPEndPoint multicastAddress, IPromise promise) => LeaveGroup(multicastAddress, null, null, promise);

        public Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface) => LeaveGroup(multicastAddress, networkInterface, null, NewPromise());

        public Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPromise promise) => LeaveGroup(multicastAddress, networkInterface, null, promise);

        public Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source) => LeaveGroup(multicastAddress, networkInterface, source, NewPromise());

        public Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source, IPromise promise)
        {
            if (EventLoop.InEventLoop)
            {
                DoLeaveGroup(multicastAddress, networkInterface, source, promise);
            }
            else
            {
                try
                {
                    EventLoop.Execute(() => DoLeaveGroup(multicastAddress, networkInterface, source, promise));
                }
                catch (Exception ex)
                {
                    Util.SafeSetFailure(promise, ex, Logger);
                }
            }

            return promise.Task;
        }

        void DoLeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source, IPromise promise)
        {
            try
            {
                Socket.SetSocketOption(
                    _config.AddressFamilyOptionLevel,
                    SocketOptionName.DropMembership,
                    CreateMulticastOption(multicastAddress, networkInterface, source));

                promise.Complete();
            }
            catch (Exception exception)
            {
                Util.SafeSetFailure(promise, exception, Logger);
            }
        }

        object CreateMulticastOption(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source)
        {
            int interfaceIndex = -1;
            if (networkInterface is object)
            {
                int index = _config.GetNetworkInterfaceIndex(networkInterface);
                if (index >= 0)
                {
                    interfaceIndex = index;
                }
            }

            if (Socket.AddressFamily == AddressFamily.InterNetwork)
            {
                var multicastOption = new MulticastOption(multicastAddress.Address);
                if (interfaceIndex >= 0)
                {
                    multicastOption.InterfaceIndex = interfaceIndex;
                }
                if (source is object)
                {
                    multicastOption.LocalAddress = source.Address;
                }

                return multicastOption;
            }

            if (Socket.AddressFamily == AddressFamily.InterNetworkV6)
            {
                var multicastOption = new IPv6MulticastOption(multicastAddress.Address);

                // Technically IPV6 multicast requires network interface index,
                // but if it is not specified, default 0 will be used.
                if (interfaceIndex >= 0)
                {
                    multicastOption.InterfaceIndex = interfaceIndex;
                }

                return multicastOption;
            }

            throw new NotSupportedException($"Socket address family {Socket.AddressFamily} not supported, expecting InterNetwork or InterNetworkV6");
        }
    }
}