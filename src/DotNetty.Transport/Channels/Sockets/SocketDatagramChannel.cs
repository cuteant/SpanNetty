// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;

    public partial class SocketDatagramChannel<TChannel> : AbstractSocketMessageChannel<TChannel, SocketDatagramChannel<TChannel>.DatagramChannelUnsafe>, IDatagramChannel
    {
        //static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<SocketDatagramChannel>();
        static readonly Action<object, object> ReceiveFromCompletedSyncCallback = OnReceiveFromCompletedSync;
        static readonly ChannelMetadata ChannelMetadata = new ChannelMetadata(true);

        readonly DefaultDatagramChannelConfig config;
        readonly IPEndPoint anyRemoteEndPoint;

        //public SocketDatagramChannel()
        //    : this(new Socket(SocketType.Dgram, ProtocolType.Udp))
        //{
        //}

        public SocketDatagramChannel(AddressFamily addressFamily)
            : this(new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp))
        {
        }

        public SocketDatagramChannel(Socket socket)
            : base(null, socket)
        {
            this.config = new DefaultDatagramChannelConfig(this, socket);
            this.anyRemoteEndPoint = new IPEndPoint(
                socket.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any,
                IPEndPoint.MinPort);
        }

        public override IChannelConfiguration Configuration => this.config;

        public override ChannelMetadata Metadata => ChannelMetadata;

        protected override EndPoint LocalAddressInternal => this.Socket.LocalEndPoint;

        protected override EndPoint RemoteAddressInternal => this.Socket.RemoteEndPoint;

        protected override void DoBind(EndPoint localAddress)
        {
            this.Socket.Bind(localAddress);
            this.CacheLocalAddress();

            this.SetState(StateFlags.Active);
        }

        public override bool Active => this.Open && this.Socket.IsBound;

        protected override bool DoConnect(EndPoint remoteAddress, EndPoint localAddress)
        {
            if (localAddress != null)
            {
                this.DoBind(localAddress);
            }

            bool success = false;
            try
            {
                this.Socket.Connect(remoteAddress);
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                {
                    this.DoClose();
                }
            }
        }

        protected override void DoFinishConnect(SocketChannelAsyncOperation<TChannel, DatagramChannelUnsafe> operation)
        {
            throw new NotSupportedException();
        }

        protected override void DoDisconnect() => this.DoClose();

        protected override void DoClose()
        {
            if (this.TryResetState(StateFlags.Open | StateFlags.Active))
            {
                this.Socket.SafeClose(); //this.Socket.Dispose();
            }
        }

        protected override void ScheduleSocketRead()
        {
            var operation = this.ReadOperation;
            operation.RemoteEndPoint = this.anyRemoteEndPoint;

            IRecvByteBufAllocatorHandle handle = this.Unsafe.RecvBufAllocHandle;
            IByteBuffer buffer = handle.Allocate(this.config.Allocator);
            handle.AttemptedBytesRead = buffer.WritableBytes;
            operation.UserToken = buffer;

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
            operation.SetBuffer(buffer.FreeMemory);
#else
            ArraySegment<byte> bytes = buffer.GetIoBuffer(0, buffer.WritableBytes);
            operation.SetBuffer(bytes.Array, bytes.Offset, bytes.Count);
#endif

            bool pending;
#if NETSTANDARD || NETCOREAPP
            pending = this.Socket.ReceiveFromAsync(operation);
#else
            if (ExecutionContext.IsFlowSuppressed())
            {
                pending = this.Socket.ReceiveFromAsync(operation);
            }
            else
            {
                using (ExecutionContext.SuppressFlow())
                {
                    pending = this.Socket.ReceiveFromAsync(operation);
                }
            }
#endif
            if (!pending)
            {
                this.EventLoop.Execute(ReceiveFromCompletedSyncCallback, this.Unsafe, operation);
            }
        }

        protected override int DoReadMessages(List<object> buf)
        {
            if (null == buf) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buf); }

            var operation = this.ReadOperation;
            var data = (IByteBuffer)operation.UserToken;
            bool free = true;

            try
            {
                IRecvByteBufAllocatorHandle handle = this.Unsafe.RecvBufAllocHandle;

                int received = operation.BytesTransferred;
                if (received <= 0)
                {
                    return 0;
                }

                handle.LastBytesRead = received;
                data.SetWriterIndex(data.WriterIndex + received);
                EndPoint remoteAddress = operation.RemoteEndPoint;
                buf.Add(new DatagramPacket(data, remoteAddress, this.LocalAddress));
                free = false;

                return 1;
            }
            finally
            {
                if (free)
                {
                    data.Release();
                }

                operation.UserToken = null;
            }
        }

        static void OnReceiveFromCompletedSync(object u, object p) => ((DatagramChannelUnsafe)u).FinishRead((SocketChannelAsyncOperation<TChannel, DatagramChannelUnsafe>)p);

        protected override void ScheduleMessageWrite(object message)
        {
            var envelope = message as IAddressedEnvelope<IByteBuffer>;
            if (envelope == null)
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
            var operation = this.PrepareWriteOperation(data.GetReadableMemory(data.ReaderIndex, length));
#else
            var operation = this.PrepareWriteOperation(data.GetIoBuffer(data.ReaderIndex, length));
#endif
            operation.RemoteEndPoint = envelope.Recipient;
            this.SetState(StateFlags.WriteScheduled);
            bool pending = this.Socket.SendToAsync(operation);
            if (!pending)
            {
                this.Unsafe.FinishWrite(operation);
            }
        }

        //protected override IChannelUnsafe NewUnsafe() => new DatagramChannelUnsafe(this); ## 苦竹 屏蔽 ##

        public sealed class DatagramChannelUnsafe : SocketMessageUnsafe
        {
            public DatagramChannelUnsafe() //SocketDatagramChannel channel)
                : base() //channel)
            {
            }

            protected override bool CanWrite => this.channel.Open && this.channel.Registered;
        }

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
                remoteAddress = this.RemoteAddressInternal;
            }

            if (data == null || remoteAddress == null)
            {
                return false;
            }

            int length = data.ReadableBytes;
            if (0u >= (uint)length)
            {
                return true;
            }

            ArraySegment<byte> bytes = data.GetIoBuffer(data.ReaderIndex, length);
            int writtenBytes = this.Socket.SendTo(bytes.Array, bytes.Offset, bytes.Count, SocketFlags.None, remoteAddress);

            return writtenBytes > 0;
        }

        protected override object FilterOutboundMessage(object msg)
        {
            if (msg is DatagramPacket packet)
            {
                return packet.Content.IsSingleIoBuffer
                    ? packet
                    : new DatagramPacket(this.CreateNewDirectBuffer(packet, packet.Content), packet.Recipient);
            }

            if (msg is IByteBuffer buffer)
            {
                return buffer.IsSingleIoBuffer
                    ? buffer
                    : this.CreateNewDirectBuffer(buffer);
            }

            if (msg is IAddressedEnvelope<IByteBuffer> envolope)
            {
                if (envolope.Content.IsSingleIoBuffer)
                {
                    return envolope;
                }

                return new DefaultAddressedEnvelope<IByteBuffer>(
                    this.CreateNewDirectBuffer(envolope, envolope.Content), envolope.Recipient);
            }

            throw new NotSupportedException(
                $"Unsupported message type: {msg.GetType()}, expecting instances of DatagramPacket, IByteBuffer or IAddressedEnvelope.");
        }

        IByteBuffer CreateNewDirectBuffer(IByteBuffer buffer)
        {
            if (null == buffer) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }

            int readableBytes = buffer.ReadableBytes;
            if (0u >= (uint)readableBytes)
            {
                buffer.SafeRelease();
                return Unpooled.Empty;
            }

            // Composite
            IByteBuffer data = this.Allocator.Buffer(readableBytes);
            data.WriteBytes(buffer, buffer.ReaderIndex, readableBytes);
            buffer.SafeRelease();

            return data;
        }

        IByteBuffer CreateNewDirectBuffer(IReferenceCounted holder, IByteBuffer buffer)
        {
            if (null == holder) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.holder); }
            if (null == buffer) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }

            int readableBytes = buffer.ReadableBytes;
            if (0u >= (uint)readableBytes)
            {
                holder.SafeRelease();
                return Unpooled.Empty;
            }

            // Composite
            IByteBuffer data = this.Allocator.Buffer(readableBytes);
            data.WriteBytes(buffer, buffer.ReaderIndex, readableBytes);
            holder.SafeRelease();

            return data;
        }

        ////
        //// Checks if the specified buffer is a direct buffer and is composed of a single NIO buffer.
        //// (We check this because otherwise we need to make it a non-composite buffer.)
        ////
        //static bool IsSingleBuffer(IByteBuffer buffer)
        //{
        //    if (null == buffer) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }
        //    return buffer.IsSingleIoBuffer;
        //}

        // Continue on write error as a SocketDatagramChannel can write to multiple remote peers
        // See https://github.com/netty/netty/issues/2665
        protected override bool ContinueOnWriteError => true;

        public bool IsConnected() => this.Socket.Connected;

        public Task JoinGroup(IPEndPoint multicastAddress) => this.JoinGroup(multicastAddress, null, null, this.NewPromise());

        public Task JoinGroup(IPEndPoint multicastAddress, IPromise promise) => this.JoinGroup(multicastAddress, null, null, promise);

        public Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface) => this.JoinGroup(multicastAddress, networkInterface, null, this.NewPromise());

        public Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPromise promise) => this.JoinGroup(multicastAddress, networkInterface, null, this.NewPromise());

        public Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source) => this.JoinGroup(multicastAddress, networkInterface, source, this.NewPromise());

        public Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source, IPromise promise)
        {
            if (this.EventLoop.InEventLoop)
            {
                this.DoJoinGroup(multicastAddress, networkInterface, source, promise);
            }
            else
            {
                try
                {
                    this.EventLoop.Execute(() => this.DoJoinGroup(multicastAddress, networkInterface, source, promise));
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
                this.Socket.SetSocketOption(
                    this.config.AddressFamilyOptionLevel,
                    SocketOptionName.AddMembership,
                    this.CreateMulticastOption(multicastAddress, networkInterface, source));

                promise.Complete();
            }
            catch (Exception exception)
            {
                Util.SafeSetFailure(promise, exception, Logger);
            }
        }

        public Task LeaveGroup(IPEndPoint multicastAddress) => this.LeaveGroup(multicastAddress, null, null, this.NewPromise());

        public Task LeaveGroup(IPEndPoint multicastAddress, IPromise promise) => this.LeaveGroup(multicastAddress, null, null, promise);

        public Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface) => this.LeaveGroup(multicastAddress, networkInterface, null, this.NewPromise());

        public Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPromise promise) => this.LeaveGroup(multicastAddress, networkInterface, null, promise);

        public Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source) => this.LeaveGroup(multicastAddress, networkInterface, source, this.NewPromise());

        public Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source, IPromise promise)
        {
            if (this.EventLoop.InEventLoop)
            {
                this.DoLeaveGroup(multicastAddress, networkInterface, source, promise);
            }
            else
            {
                try
                {
                    this.EventLoop.Execute(() => this.DoLeaveGroup(multicastAddress, networkInterface, source, promise));
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
                this.Socket.SetSocketOption(
                    this.config.AddressFamilyOptionLevel,
                    SocketOptionName.DropMembership,
                    this.CreateMulticastOption(multicastAddress, networkInterface, source));

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
            if (networkInterface != null)
            {
                int index = this.config.GetNetworkInterfaceIndex(networkInterface);
                if (index >= 0)
                {
                    interfaceIndex = index;
                }
            }

            if (this.Socket.AddressFamily == AddressFamily.InterNetwork)
            {
                var multicastOption = new MulticastOption(multicastAddress.Address);
                if (interfaceIndex >= 0)
                {
                    multicastOption.InterfaceIndex = interfaceIndex;
                }
                if (source != null)
                {
                    multicastOption.LocalAddress = source.Address;
                }

                return multicastOption;
            }

            if (this.Socket.AddressFamily == AddressFamily.InterNetworkV6)
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

            throw new NotSupportedException($"Socket address family {this.Socket.AddressFamily} not supported, expecting InterNetwork or InterNetworkV6");
        }
    }
}