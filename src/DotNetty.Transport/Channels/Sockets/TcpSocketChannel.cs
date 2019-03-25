// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;

    /// <summary>
    ///     <see cref="ISocketChannel" /> which uses Socket-based implementation.
    /// </summary>
    public partial class TcpSocketChannel<TChannel> : AbstractSocketByteChannel<TChannel, TcpSocketChannel<TChannel>.TcpSocketChannelUnsafe>, ISocketChannel
    {
        static readonly ChannelMetadata METADATA = new ChannelMetadata(false, 16);

        readonly ISocketChannelConfiguration config;

        /// <summary>Create a new instance</summary>
        public TcpSocketChannel()
            : this(null, SocketEx.CreateSocket(), false) //new Socket(SocketType.Stream, ProtocolType.Tcp))
        {
        }

        /// <summary>Create a new instance</summary>
        public TcpSocketChannel(AddressFamily addressFamily)
            : this(null, SocketEx.CreateSocket(addressFamily), false) //new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp))
        {
        }

        /// <summary>Create a new instance using the given <see cref="ISocketChannel" />.</summary>
        public TcpSocketChannel(Socket socket)
            : this(null, socket, false)
        {
        }

        /// <summary>Create a new instance</summary>
        /// <param name="parent">
        ///     the <see cref="IChannel" /> which created this instance or <c>null</c> if it was created by the
        ///     user
        /// </param>
        /// <param name="socket">the <see cref="ISocketChannel" /> which will be used</param>
        public TcpSocketChannel(IChannel parent, Socket socket)
            : this(parent, socket, false)
        {
        }

        protected TcpSocketChannel(IChannel parent, Socket socket, bool connected)
            : base(parent, socket)
        {
            this.config = new TcpSocketChannelConfig((TChannel)this, socket);
            if (connected)
            {
                this.OnConnected();
            }
        }

        public override ChannelMetadata Metadata => METADATA;

        public override IChannelConfiguration Configuration => this.config;

        protected override EndPoint LocalAddressInternal => this.Socket.LocalEndPoint;

        protected override EndPoint RemoteAddressInternal => this.Socket.RemoteEndPoint;

        public bool IsOutputShutdown
        {
            get { throw new NotImplementedException(); } // todo: impl with stateflags
        }

        public Task ShutdownOutputAsync()
        {
            var tcs = this.NewPromise();
            // todo: use closeExecutor if available
            //Executor closeExecutor = ((TcpSocketChannelUnsafe) unsafe()).closeExecutor();
            //if (closeExecutor != null) {
            //    closeExecutor.execute(new OneTimeTask() {

            //        public void run() {
            //            shutdownOutput0(promise);
            //        }
            //    });
            //} else {
            IEventLoop loop = this.EventLoop;
            if (loop.InEventLoop)
            {
                this.ShutdownOutput0(tcs);
            }
            else
            {
                loop.Execute(ShutdownOutputAction, this, tcs);
            }
            //}
            return tcs.Task;
        }

        void ShutdownOutput0(IPromise promise)
        {
            try
            {
                this.Socket.Shutdown(SocketShutdown.Send);
                promise.Complete();
            }
            catch (Exception ex)
            {
                promise.SetException(ex);
            }
        }

        protected override void DoBind(EndPoint localAddress) => this.Socket.Bind(localAddress);

        protected override bool DoConnect(EndPoint remoteAddress, EndPoint localAddress)
        {
            if (localAddress != null)
            {
                this.Socket.Bind(localAddress);
            }

            bool success = false;
            try
            {
                var eventPayload = new SocketChannelAsyncOperation<TChannel, TcpSocketChannelUnsafe>((TChannel)this, false)
                {
                    RemoteEndPoint = remoteAddress
                };
                bool connected = !this.Socket.ConnectAsync(eventPayload);
                if (connected)
                {
                    this.DoFinishConnect(eventPayload);
                }
                success = true;
                return connected;
            }
            finally
            {
                if (!success)
                {
                    this.DoClose();
                }
            }
        }

        protected override void DoFinishConnect(SocketChannelAsyncOperation<TChannel, TcpSocketChannelUnsafe> operation)
        {
            try
            {
                operation.Validate();
            }
            finally
            {
                operation.Dispose();
            }
            this.OnConnected();
        }

        void OnConnected()
        {
            this.SetState(StateFlags.Active);

            // preserve local and remote addresses for later availability even if Socket fails
            this.CacheLocalAddress();
            this.CacheRemoteAddress();
        }

        protected override void DoDisconnect() => this.DoClose();

        protected override void DoClose()
        {
            try
            {
                if (this.TryResetState(StateFlags.Open | StateFlags.Active))
                {
                    //this.Socket.Shutdown(SocketShutdown.Both);
                    this.Socket.SafeClose(); //this.Socket.Dispose();
                }
            }
            finally
            {
                base.DoClose();
            }
        }

        protected override int DoReadBytes(IByteBuffer byteBuf)
        {
            if (!byteBuf.HasArray)
            {
                ThrowHelper.ThrowNotImplementedException_OnlyIByteBufferImpl();
            }

            if (!this.Socket.Connected)
            {
                return -1; // prevents ObjectDisposedException from being thrown in case connection has been lost in the meantime
            }

#if NETCOREAPP
            int received = this.Socket.Receive(byteBuf.FreeSpan, SocketFlags.None, out SocketError errorCode);
#else
            int received = this.Socket.Receive(byteBuf.Array, byteBuf.ArrayOffset + byteBuf.WriterIndex, byteBuf.WritableBytes, SocketFlags.None, out SocketError errorCode);
#endif

            switch (errorCode)
            {
                case SocketError.Success:
                    if (received == 0)
                    {
                        return -1; // indicate that socket was closed
                    }
                    break;
                case SocketError.WouldBlock:
                    if (received == 0)
                    {
                        return 0;
                    }
                    break;
                default:
                    ThrowHelper.ThrowSocketException(errorCode); break;
            }

            byteBuf.SetWriterIndex(byteBuf.WriterIndex + received);

            return received;
        }

        protected override int DoWriteBytes(IByteBuffer buf)
        {
            if (!buf.HasArray)
            {
                ThrowHelper.ThrowNotImplementedException_OnlyIByteBufferImpl();
            }

            int sent = this.Socket.Send(buf.Array, buf.ArrayOffset + buf.ReaderIndex, buf.ReadableBytes, SocketFlags.None, out SocketError errorCode);

            if (errorCode != SocketError.Success && errorCode != SocketError.WouldBlock)
            {
                ThrowHelper.ThrowSocketException(errorCode);
            }

            if (sent > 0)
            {
                buf.SetReaderIndex(buf.ReaderIndex + sent);
            }

            return sent;
        }

        //protected long doWriteFileRegion(FileRegion region)
        //{
        //    long position = region.transfered();
        //    return region.transferTo(javaChannel(), position);
        //}

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            List<ArraySegment<byte>> sharedBufferList = null;
            var socketConfig = (TcpSocketChannelConfig)this.config;
            Socket socket = this.Socket;
            var writeSpinCount = socketConfig.WriteSpinCount;
            try
            {
                while (true)
                {
                    int size = input.Size;
                    if (size == 0)
                    {
                        // All written
                        break;
                    }
                    long writtenBytes = 0;
                    bool done = false;

                    // Ensure the pending writes are made of ByteBufs only.
                    int maxBytesPerGatheringWrite = socketConfig.GetMaxBytesPerGatheringWrite();
                    sharedBufferList = input.GetSharedBufferList(1024, maxBytesPerGatheringWrite);
                    int nioBufferCnt = sharedBufferList.Count;
                    long expectedWrittenBytes = input.NioBufferSize;

                    List<ArraySegment<byte>> bufferList = sharedBufferList;
                    // Always us nioBuffers() to workaround data-corruption.
                    // See https://github.com/netty/netty/issues/2761
                    switch (nioBufferCnt)
                    {
                        case 0:
                            // We have something else beside ByteBuffers to write so fallback to normal writes.
                            base.DoWrite(input);
                            return;
                        default:
                            for (int i = writeSpinCount - 1; i >= 0; i--)
                            {
                                long localWrittenBytes = socket.Send(bufferList, SocketFlags.None, out SocketError errorCode);
                                if (errorCode != SocketError.Success && errorCode != SocketError.WouldBlock)
                                {
                                    ThrowHelper.ThrowSocketException(errorCode);
                                }

                                if (localWrittenBytes == 0)
                                {
                                    break;
                                }

                                expectedWrittenBytes -= localWrittenBytes;
                                writtenBytes += localWrittenBytes;
                                if (expectedWrittenBytes == 0)
                                {
                                    done = true;
                                    break;
                                }
                                else
                                {
                                    bufferList = this.AdjustBufferList(localWrittenBytes, bufferList);
                                }
                            }
                            break;
                    }

                    if (writtenBytes > 0)
                    {
                        // Release the fully written buffers, and update the indexes of the partially written buffer
                        input.RemoveBytes(writtenBytes);
                    }

                    if (!done)
                    {
                        IList<ArraySegment<byte>> asyncBufferList = bufferList;
                        if (object.ReferenceEquals(sharedBufferList, asyncBufferList))
                        {
                            asyncBufferList = sharedBufferList.ToArray(); // move out of shared list that will be reused which could corrupt buffers still pending update
                        }
                        var asyncOperation = this.PrepareWriteOperation(asyncBufferList);

                        // Not all buffers were written out completely
                        if (this.IncompleteWrite(true, asyncOperation))
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                // Prepare the list for reuse
                sharedBufferList?.Clear();
            }
        }

        List<ArraySegment<byte>> AdjustBufferList(long localWrittenBytes, List<ArraySegment<byte>> bufferList)
        {
            var adjusted = new List<ArraySegment<byte>>(bufferList.Count);
            foreach (ArraySegment<byte> buffer in bufferList)
            {
                if (localWrittenBytes > 0)
                {
                    long leftBytes = localWrittenBytes - buffer.Count;
                    if (leftBytes < 0)
                    {
                        int offset = buffer.Offset + (int)localWrittenBytes;
                        int count = -(int)leftBytes;
                        adjusted.Add(new ArraySegment<byte>(buffer.Array, offset, count));
                        localWrittenBytes = 0;
                    }
                    else
                    {
                        localWrittenBytes = leftBytes;
                    }
                }
                else
                {
                    adjusted.Add(buffer);
                }
            }
            return adjusted;
        }

        //protected override IChannelUnsafe NewUnsafe() => new TcpSocketChannelUnsafe(this); ## 苦竹 屏蔽 ##

        public sealed class TcpSocketChannelUnsafe : SocketByteChannelUnsafe
        {
            public TcpSocketChannelUnsafe() //TcpSocketChannel channel)
                : base() //channel)
            {
            }

            // todo: review
            //protected Executor closeExecutor()
            //{
            //    if (javaChannel().isOpen() && config().getSoLinger() > 0)
            //    {
            //        return GlobalEventExecutor.INSTANCE;
            //    }
            //    return null;
            //}
        }

        sealed class TcpSocketChannelConfig : DefaultSocketChannelConfiguration
        {
            int maxBytesPerGatheringWrite = int.MaxValue;

            public TcpSocketChannelConfig(TChannel channel, Socket javaSocket)
                : base(channel, javaSocket)
            {
                this.CalculateMaxBytesPerGatheringWrite();
            }

            public int GetMaxBytesPerGatheringWrite() => Volatile.Read(ref this.maxBytesPerGatheringWrite);

            public override int SendBufferSize
            {
                get => base.SendBufferSize;
                set
                {
                    base.SendBufferSize = value;
                    this.CalculateMaxBytesPerGatheringWrite();
                }
            }

            void CalculateMaxBytesPerGatheringWrite()
            {
                // Multiply by 2 to give some extra space in case the OS can process write data faster than we can provide.
                int newSendBufferSize = this.SendBufferSize << 1;
                if (newSendBufferSize > 0)
                {
                    Interlocked.Exchange(ref this.maxBytesPerGatheringWrite, newSendBufferSize);
                }
            }

            protected override void AutoReadCleared() => ((TChannel)this.Channel).ClearReadPending();
        }
    }
}