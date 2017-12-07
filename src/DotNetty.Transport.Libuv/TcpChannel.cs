// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv.Native;

    public sealed class TcpChannel : TcpChannel<TcpChannel>
    {
        public TcpChannel() : base() { }

        internal TcpChannel(IChannel parent, Tcp tcp) : base(parent, tcp) { }
    }

    public class TcpChannel<TChannel> : NativeChannel<TChannel, TcpChannel<TChannel>.TcpChannelUnsafe>, ISocketChannel
        where TChannel : TcpChannel<TChannel>
    {
        const int DefaultWriteRequestPoolSize = 1024;

        static readonly ChannelMetadata TcpMetadata = new ChannelMetadata(false, 16);
        static readonly ThreadLocalPool<WriteRequest> Recycler = new ThreadLocalPool<WriteRequest>(handle => new WriteRequest(handle), DefaultWriteRequestPoolSize);

        readonly TcpChannelConfig config;
        Tcp tcp;

        public TcpChannel() : this(null, null)
        {
        }

        protected TcpChannel(IChannel parent, Tcp tcp) : base(parent)
        {
            this.config = new TcpChannelConfig(this);
            this.SetState(StateFlags.Open);

            this.tcp = tcp;
            if (this.tcp != null)
            {
                if (this.config.TcpNoDelay)
                {
                    tcp.NoDelay(true);
                }

                this.OnConnected();
            }
        }

        public sealed override IChannelConfiguration Configuration => this.config;

        public sealed override ChannelMetadata Metadata => TcpMetadata;

        protected sealed override EndPoint LocalAddressInternal => this.tcp?.GetLocalEndPoint();

        protected sealed override EndPoint RemoteAddressInternal => this.tcp?.GetPeerEndPoint();

        // ## 苦竹 屏蔽 ##
        //protected override IChannelUnsafe NewUnsafe() => new TcpChannelUnsafe(this);

        protected sealed override void DoRegister()
        {
            if (this.tcp != null)
            {
                this.Unsafe.ScheduleRead();
            }
            else
            {
                var loopExecutor = (ILoopExecutor)this.EventLoop;
                Loop loop = loopExecutor.UnsafeLoop;
                this.CreateHandle(loop);
            }
        }

        internal void CreateHandle(Loop loop)
        {
            Debug.Assert(this.tcp == null);

            this.tcp = new Tcp(loop);
            this.config.SetOptions(this.tcp);
        }

        internal override unsafe IntPtr GetLoopHandle()
        {
            if (this.tcp == null)
            {
                throw new InvalidOperationException("Tcp handle not intialized");
            }

            return ((uv_stream_t*)this.tcp.Handle)->loop;
        }

        protected sealed override void DoBind(EndPoint localAddress)
        {
            this.tcp.Bind((IPEndPoint)localAddress);
            this.CacheLocalAddress();
        }

        protected sealed override void DoDisconnect() => this.DoClose();

        protected sealed override void DoClose()
        {
            if (this.TryResetState(StateFlags.Open | StateFlags.Active))
            {
                this.tcp?.ReadStop();
                this.tcp?.CloseHandle();
                this.tcp = null;
            }
        }

        protected sealed override void DoBeginRead()
        {
            if (!this.Open || this.IsInState(StateFlags.ReadScheduled))
            {
                return;
            }

            this.Unsafe.ScheduleRead();
        }

        protected sealed override void DoScheduleRead()
        {
            if (!this.Open)
            {
                return;
            }

            if (!this.IsInState(StateFlags.ReadScheduled))
            {
                this.SetState(StateFlags.ReadScheduled);
                this.tcp.ReadStart(this.Unsafe);
            }
        }

        protected sealed override void DoWrite(ChannelOutboundBuffer input)
        {
            if (this.EventLoop.InEventLoop)
            {
                this.Write(input);
            }
            else
            {
                this.EventLoop.Execute(WriteAction, this, input);
            }
        }

        static readonly Action<object, object> WriteAction = (u, e) => ((TChannel)u).Write((ChannelOutboundBuffer)e);

        void Write(ChannelOutboundBuffer input)
        {
            while (true)
            {
                int size = input.Count;
                if (size == 0)
                {
                    break;
                }

                List<ArraySegment<byte>> nioBuffers = input.GetSharedBufferList();
                int nioBufferCnt = nioBuffers.Count;
                long expectedWrittenBytes = input.NioBufferSize;
                if (nioBufferCnt == 0)
                {
                    this.WriteByteBuffers(input);
                    return;
                }
                else
                {
                    WriteRequest writeRequest = Recycler.Take();
                    writeRequest.Prepare(this.Unsafe, nioBuffers);
                    this.tcp.Write(writeRequest);
                    input.RemoveBytes(expectedWrittenBytes);
                }
            }
        }

        void WriteByteBuffers(ChannelOutboundBuffer input)
        {
            while (true)
            {
                object msg = input.Current;
                if (msg == null)
                {
                    // Wrote all messages.
                    break;
                }

                if (msg is IByteBuffer buf)
                {
                    int readableBytes = buf.ReadableBytes;
                    if (readableBytes == 0)
                    {
                        input.Remove();
                        continue;
                    }

                    var nioBuffers = new List<ArraySegment<byte>>();
                    ArraySegment<byte> nioBuffer = buf.GetIoBuffer();
                    nioBuffers.Add(nioBuffer);
                    WriteRequest writeRequest = Recycler.Take();
                    writeRequest.Prepare(this.Unsafe, nioBuffers);
                    this.tcp.Write(writeRequest);

                    input.Remove();
                }
                else
                {
                    // Should not reach here.
                    throw new InvalidOperationException();
                }
            }
        }

        public sealed class TcpChannelUnsafe : NativeChannelUnsafe
        {
            public TcpChannelUnsafe() { }
            //public TcpChannelUnsafe(TcpChannel channel) : base(channel)
            //{
            //}

            public override IntPtr UnsafeHandle => this.channel.tcp.Handle;
        }
    }
}
