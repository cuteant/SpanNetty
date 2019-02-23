// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using CuteAnt.Collections;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public partial class Http2MultiplexCodec : Http2FrameCodec
    {
        private static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<Http2MultiplexCodec>();

        private static readonly Action<Task, object> RegisterDoneAction = RegisterDone;

        private static readonly ChannelMetadata CodecMetadata = new ChannelMetadata(false, 16);
        internal static readonly ClosedChannelException ClosedChannelException = new ClosedChannelException();

        /// <summary>
        /// Number of bytes to consider non-payload messages. 9 is arbitrary, but also the minimum size of an HTTP/2 frame.
        /// Primarily is non-zero.
        /// </summary>
        private static readonly int MinHttp2FrameSize = 9;

        /// <summary>
        /// Returns the flow-control size for DATA frames, and 0 for all other frames.
        /// </summary>
        private sealed class FlowControlledFrameSizeEstimator : IMessageSizeEstimator
        {
            public static readonly IMessageSizeEstimator Instance = new FlowControlledFrameSizeEstimator();

            private FlowControlledFrameSizeEstimator() { }

            public IMessageSizeEstimatorHandle NewHandle() => FlowControlledFrameSizeEstimatorHandle.Instance;
        }
        private sealed class FlowControlledFrameSizeEstimatorHandle : IMessageSizeEstimatorHandle
        {
            public static readonly IMessageSizeEstimatorHandle Instance = new FlowControlledFrameSizeEstimatorHandle();

            private FlowControlledFrameSizeEstimatorHandle() { }

            public int Size(object msg)
            {
                return msg is IHttp2DataFrame frame
                    ? (int)Math.Min(int.MaxValue, frame.InitialFlowControlledBytes + (long)MinHttp2FrameSize)
                    : MinHttp2FrameSize;
            }
        }

        private readonly IChannelHandler inboundStreamHandler;
        private readonly IChannelHandler upgradeStreamHandler;

        private int initialOutboundStreamWindow = Http2CodecUtil.DefaultWindowSize;
        private bool parentReadInProgress;
        private int idCount;

        // Linked-List for DefaultHttp2StreamChannel instances that need to be processed by channelReadComplete(...)
        private DefaultHttp2StreamChannel head;
        private DefaultHttp2StreamChannel tail;

        // Need to be volatile as accessed from within the DefaultHttp2StreamChannel in a multi-threaded fashion.
        private IChannelHandlerContext _ctx;
        protected IChannelHandlerContext InternalContext
        {
            get => Volatile.Read(ref _ctx);
            set => Interlocked.Exchange(ref _ctx, value);
        }


        public Http2MultiplexCodec(IHttp2ConnectionEncoder encoder, IHttp2ConnectionDecoder decoder,
            Http2Settings initialSettings, IChannelHandler inboundStreamHandler, IChannelHandler upgradeStreamHandler)
            : base(encoder, decoder, initialSettings)
        {
            this.inboundStreamHandler = inboundStreamHandler;
            this.upgradeStreamHandler = upgradeStreamHandler;
        }

        private int NewId() => Interlocked.Increment(ref this.idCount);

        public override void OnHttpClientUpgrade()
        {
            // We must have an upgrade handler or else we can't handle the stream
            if (this.upgradeStreamHandler == null)
            {
                ThrowHelper.ThrowConnectionError_ClientIsMisconfiguredForUpgradeRequests();
            }
            // Creates the Http2Stream in the Connection.
            base.OnHttpClientUpgrade();
            // Now make a new FrameStream, set it's underlying Http2Stream, and initialize it.
            var codecStream = (Http2MultiplexCodecStream)this.NewStream();
            codecStream.SetStreamAndProperty(streamKey, this.Connection.Stream(Http2CodecUtil.HttpUpgradeStreamId));
            this.OnHttp2UpgradeStreamInitialized(this.InternalContext, codecStream);
        }

        private static void RegisterDone(Task future, object state) => RegisterDone(future, (IChannel)state);
        private static void RegisterDone(Task future, IChannel childChannel)
        {
            // Handle any errors that occurred on the local thread while registering. Even though
            // failures can happen after this point, they will be handled by the channel by closing the
            // childChannel.
            if (!future.IsSuccess())
            {
                if (childChannel.Registered)
                {
                    childChannel.CloseAsync();
                }
                else
                {
                    childChannel.Unsafe.CloseForcibly();
                }
            }
        }

        protected sealed override void HandlerAdded0(IChannelHandlerContext ctx)
        {
            if (ctx.Executor != ctx.Channel.EventLoop)
            {
                ThrowHelper.ThrowInvalidOperationException_EventExecutorMustBeEventLoopOfChannel();
            }
            this.InternalContext = ctx;
        }

        protected sealed override void HandlerRemovedInternal(IChannelHandlerContext ctx)
        {
            base.HandlerRemovedInternal(ctx);

            // Unlink the linked list to guard against GC nepotism.
            DefaultHttp2StreamChannel ch = this.head;
            while (ch != null)
            {
                DefaultHttp2StreamChannel curr = ch;
                ch = curr.next;
                curr.next = curr.previous = null;
            }
            this.head = this.tail = null;
        }

        internal override DefaultHttp2FrameStream NewStream()
        {
            return new Http2MultiplexCodecStream();
        }

        protected sealed override void OnHttp2Frame(IChannelHandlerContext ctx, IHttp2Frame frame)
        {
            switch (frame)
            {
                case IHttp2StreamFrame streamFrame:
                    ((Http2MultiplexCodecStream)streamFrame.Stream).channel.FireChildRead(streamFrame);
                    break;

                case IHttp2GoAwayFrame goAwayFrame:
                    this.OnHttp2GoAwayFrame(ctx, goAwayFrame);
                    // Allow other handlers to act on GOAWAY frame
                    ctx.FireChannelRead(frame);
                    break;

                case IHttp2SettingsFrame settingsFrame:
                    Http2Settings settings = settingsFrame.Settings;
                    var initialWindowSize = settings.InitialWindowSize();
                    if (initialWindowSize.HasValue)
                    {
                        this.initialOutboundStreamWindow = initialWindowSize.Value;
                    }
                    // Allow other handlers to act on SETTINGS frame
                    ctx.FireChannelRead(frame);
                    break;

                default:
                    // Send any other frames down the pipeline
                    ctx.FireChannelRead(frame);
                    break;
            }
        }

        private void OnHttp2UpgradeStreamInitialized(IChannelHandlerContext ctx, Http2MultiplexCodecStream stream)
        {
            Debug.Assert(stream.State == Http2StreamState.HalfClosedLocal);
            DefaultHttp2StreamChannel ch = new DefaultHttp2StreamChannel(this, stream, true)
            {
                OutboundClosed = true
            };

            // Add our upgrade handler to the channel and then register the channel.
            // The register call fires the channelActive, etc.
            ch.Pipeline.AddLast(upgradeStreamHandler);
            var future = ctx.Channel.EventLoop.RegisterAsync(ch);
            if (future.IsCompleted)
            {
                RegisterDone(future, ch);
            }
            else
            {
#if NET40
                future.ContinueWith(t => RegisterDone(t, ch), TaskContinuationOptions.ExecuteSynchronously);
#else
                future.ContinueWith(RegisterDoneAction, ch, TaskContinuationOptions.ExecuteSynchronously);
#endif
            }
        }

        protected sealed override void OnHttp2StreamStateChanged(IChannelHandlerContext ctx, IHttp2FrameStream stream)
        {
            Http2MultiplexCodecStream s = (Http2MultiplexCodecStream)stream;
            var streamState = stream.State;
            if (Http2StreamState.HalfClosedRemote == streamState || Http2StreamState.Open == streamState)
            {
                if (s.channel != null)
                {
                    // ignore if child channel was already created.
                    return;
                }
                // fall-trough
                var channel = ctx.Channel;
                var future = channel.EventLoop.RegisterAsync(new DefaultHttp2StreamChannel(this, s, false));
                if (future.IsCompleted)
                {
                    RegisterDone(future, channel);
                }
                else
                {
#if NET40
                    future.ContinueWith(t => RegisterDone(t, channel), TaskContinuationOptions.ExecuteSynchronously);
#else
                    future.ContinueWith(RegisterDoneAction, channel, TaskContinuationOptions.ExecuteSynchronously);
#endif
                }
            }
            else if (Http2StreamState.Closed == streamState)
            {
                DefaultHttp2StreamChannel channel = s.channel;
                if (channel != null)
                {
                    channel.StreamClosed();
                }
            }
            else
            {
                // ignore for now
            }
        }

        protected sealed override void OnHttp2StreamWritabilityChanged(IChannelHandlerContext ctx, IHttp2FrameStream stream, bool writable)
        {
            (((Http2MultiplexCodecStream)stream).channel).WritabilityChanged(writable);
        }

        // TODO: This is most likely not the best way to expose this, need to think more about it.
        internal IHttp2StreamChannel NewOutboundStream()
        {
            return new DefaultHttp2StreamChannel(this, this.NewStream(), true);
        }

        protected sealed override void OnHttp2FrameStreamException(IChannelHandlerContext ctx, Http2FrameStreamException cause)
        {
            var stream = cause.Stream;
            DefaultHttp2StreamChannel childChannel = ((Http2MultiplexCodecStream)stream).channel;

            try
            {
                childChannel.Pipeline.FireExceptionCaught(cause.InnerException);
            }
            finally
            {
                childChannel.Unsafe.CloseForcibly();
            }
        }

        private bool IsChildChannelInReadPendingQueue(DefaultHttp2StreamChannel childChannel)
        {
            return childChannel.previous != null || childChannel.next != null || head == childChannel;
        }


        internal void TryAddChildChannelToReadPendingQueue(DefaultHttp2StreamChannel childChannel)
        {
            if (!this.IsChildChannelInReadPendingQueue(childChannel))
            {
                this.AddChildChannelToReadPendingQueue(childChannel);
            }
        }

        internal void AddChildChannelToReadPendingQueue(DefaultHttp2StreamChannel childChannel)
        {
            if (tail == null)
            {
                Debug.Assert(this.head == null);
                this.tail = this.head = childChannel;
            }
            else
            {
                childChannel.previous = tail;
                this.tail.next = childChannel;
                this.tail = childChannel;
            }
        }

        private void TryRemoveChildChannelFromReadPendingQueue(DefaultHttp2StreamChannel childChannel)
        {
            if (this.IsChildChannelInReadPendingQueue(childChannel))
            {
                this.RemoveChildChannelFromReadPendingQueue(childChannel);
            }
        }

        private void RemoveChildChannelFromReadPendingQueue(DefaultHttp2StreamChannel childChannel)
        {
            DefaultHttp2StreamChannel previous = childChannel.previous;
            if (childChannel.next != null)
            {
                childChannel.next.previous = previous;
            }
            else
            {
                tail = tail.previous; // If there is no next, this childChannel is the tail, so move the tail back.
            }
            if (previous != null)
            {
                previous.next = childChannel.next;
            }
            else
            {
                head = head.next; // If there is no previous, this childChannel is the head, so move the tail forward.
            }
            childChannel.next = childChannel.previous = null;
        }

        private void OnHttp2GoAwayFrame(IChannelHandlerContext ctx, IHttp2GoAwayFrame goAwayFrame)
        {
            try
            {
                bool LocalVisit(IHttp2FrameStream stream)
                {
                    int streamId = stream.Id;
                    DefaultHttp2StreamChannel childChannel = ((Http2MultiplexCodecStream)stream).channel;
                    if (streamId > goAwayFrame.LastStreamId && this.Connection.Local.IsValidStreamId(streamId))
                    {
                        childChannel.Pipeline.FireUserEventTriggered(goAwayFrame.RetainedDuplicate());
                    }
                    return true;
                }
                this.ForEachActiveStream(LocalVisit);
            }
            catch (Http2Exception e)
            {
                ctx.FireExceptionCaught(e);
                ctx.CloseAsync();
            }
        }

        /// <summary>
        /// Notifies any child streams of the read completion.
        /// </summary>
        /// <param name="ctx"></param>
        public sealed override void ChannelReadComplete(IChannelHandlerContext ctx)
        {
            try
            {
                this.OnChannelReadComplete(ctx);
            }
            finally
            {
                this.parentReadInProgress = false;
                tail = head = null;
                // We always flush as this is what Http2ConnectionHandler does for now.
                this.Flush0(ctx);
            }
            this.ChannelReadComplete0(ctx);
        }

        public sealed override void ChannelRead(IChannelHandlerContext context, object message)
        {
            this.parentReadInProgress = true;
            base.ChannelRead(context, message);
        }

        internal void OnChannelReadComplete(IChannelHandlerContext ctx)
        {
            // If we have many child channel we can optimize for the case when multiple call flush() in
            // channelReadComplete(...) callbacks and only do it once as otherwise we will end-up with multiple
            // write calls on the socket which is expensive.
            DefaultHttp2StreamChannel current = head;
            while (current != null)
            {
                DefaultHttp2StreamChannel childChannel = current;
                // Clear early in case fireChildReadComplete() causes it to need to be re-processed
                current = current.next;
                childChannel.next = childChannel.previous = null;
                childChannel.FireChildReadComplete();
            }
        }

        // Allow to override for testing
        internal virtual void Flush0(IChannelHandlerContext ctx)
        {
            this.Flush(ctx);
        }

        /// <summary>
        /// Return bytes to flow control.
        /// <para>Package private to allow to override for testing</para>
        /// </summary>
        /// <param name="ctx">The <see cref="IChannelHandlerContext"/> associated with the parent channel.</param>
        /// <param name="stream">The object representing the HTTP/2 stream.</param>
        /// <param name="bytes">The number of bytes to return to flow control.</param>
        /// <returns><c>true</c> if a frame has been written as a result of this method call.</returns>
        /// <exception cref="Http2Exception">If this operation violates the flow control limits.</exception>
        internal virtual bool OnBytesConsumed(IChannelHandlerContext ctx, IHttp2FrameStream stream, int bytes)
        {
            return this.ConsumeBytes(stream.Id, bytes);
        }

        // Allow to extend for testing
        internal class Http2MultiplexCodecStream : DefaultHttp2FrameStream
        {
            internal DefaultHttp2StreamChannel channel;
        }

        private bool InitialWritability(DefaultHttp2FrameStream stream)
        {
            // If the stream id is not valid yet we will just mark the channel as writable as we will be notified
            // about non-writability state as soon as the first Http2HeaderFrame is written (if needed).
            // This should be good enough and simplify things a lot.
            return !Http2CodecUtil.IsStreamIdValid(stream.Id) || this.IsWritable(stream);
        }

        /// <summary>
        /// The current status of the read-processing for a <see cref="IHttp2StreamChannel"/>.
        /// </summary>
        enum ReadStatus
        {
            /// <summary>
            /// No read in progress and no read was requested (yet)
            /// </summary>
            Idle,

            /// <summary>
            /// Reading in progress
            /// </summary>
            InProgress,

            /// <summary>
            /// A read operation was requested.
            /// </summary>
            Requested
        }

        // TODO: Handle writability changes due writing from outside the eventloop.
        internal sealed partial class DefaultHttp2StreamChannel : DefaultAttributeMap, IHttp2StreamChannel
        {
            private readonly Http2MultiplexCodec multiCodec;
            private readonly Http2StreamChannelConfiguration config;
            private readonly Http2ChannelUnsafe channelUnsafe;
            private readonly IChannelId channelId;
            private readonly IChannelPipeline pipeline;
            private readonly DefaultHttp2FrameStream stream;
            private readonly IPromise closePromise;
            private readonly bool outbound;

            private int _registered = Constants.False;
            private bool InternalRegistered
            {
                get => Constants.True == Volatile.Read(ref _registered);
                set => Interlocked.Exchange(ref _registered, value ? Constants.True : Constants.False);
            }
            // We start with the writability of the channel when creating the StreamChannel.
            private int _writable = Constants.False;
            private bool InternalWritable
            {
                get => Constants.True == Volatile.Read(ref _writable);
                set => Interlocked.Exchange(ref _writable, value ? Constants.True : Constants.False);
            }

            private int _outboundClosed = Constants.False;
            internal bool OutboundClosed
            {
                get => Constants.True == Volatile.Read(ref _outboundClosed);
                set => Interlocked.Exchange(ref _outboundClosed, value ? Constants.True : Constants.False);
            }

            /// <summary>
            /// This variable represents if a read is in progress for the current channel or was requested.
            /// Note that depending upon the <see cref="IRecvByteBufAllocator"/> behavior a read may extend beyond the
            /// <see cref="Http2ChannelUnsafe.BeginRead"/> method scope. The <see cref="Http2ChannelUnsafe.BeginRead"/> loop may
            /// drain all pending data, and then if the parent channel is reading this channel may still accept frames.
            /// </summary>
            private ReadStatus readStatus = ReadStatus.Idle;

            private Deque<object> inboundBuffer;

            /// <summary>
            /// <c>true</c> after the first HEADERS frame has been written
            /// </summary>
            private bool firstFrameWritten;

            // Currently the child channel and parent channel are always on the same EventLoop thread. This allows us to
            // extend the read loop of a child channel if the child channel drains its queued data during read, and the
            // parent channel is still in its read loop. The next/previous links build a doubly linked list that the parent
            // channel will iterate in its channelReadComplete to end the read cycle for each child channel in the list.
            internal DefaultHttp2StreamChannel next;
            internal DefaultHttp2StreamChannel previous;

            public DefaultHttp2StreamChannel(Http2MultiplexCodec multiCodec, DefaultHttp2FrameStream stream, bool outbound)
            {
                this.multiCodec = multiCodec;
                this.config = new Http2StreamChannelConfiguration(this);
                this.channelUnsafe = new Http2ChannelUnsafe(this);

                this.stream = stream;
                this.outbound = outbound;
                this.InternalWritable = multiCodec.InitialWritability(stream);
                ((Http2MultiplexCodecStream)stream).channel = this;
                this.pipeline = new Http2ChannelPipeline(this);
                this.closePromise = this.pipeline.NewPromise();
                this.channelId = new Http2StreamChannelId(Parent.Id, multiCodec.NewId());
            }

            public IHttp2FrameStream Stream => this.stream;

            internal void StreamClosed()
            {
                this.channelUnsafe.ReadEOS = true;
                // Attempt to drain any queued data from the queue and deliver it to the application before closing this
                // channel.
                this.channelUnsafe.DoBeginRead();
            }

            public ChannelMetadata Metadata => CodecMetadata;

            public IChannelConfiguration Configuration => this.config;

            public bool Open => !this.closePromise.IsCompleted;

            public bool Active => this.Open;

            public bool IsWritable => this.InternalWritable;

            public IChannelId Id => this.channelId;

            public IEventLoop EventLoop => this.Parent.EventLoop;

            public IChannel Parent => this.multiCodec.InternalContext.Channel;

            public bool Registered => this.InternalRegistered;

            public EndPoint LocalAddress => this.Parent.LocalAddress;

            public EndPoint RemoteAddress => this.Parent.RemoteAddress;

            public Task CloseCompletion => this.closePromise.Task;

            // TODO: Do a proper impl
            public long BytesBeforeUnwritable => this.config.WriteBufferHighWaterMark;

            // TODO: Do a proper impl
            public long BytesBeforeWritable => 0;

            public IChannelUnsafe Unsafe => this.channelUnsafe;

            public IChannelPipeline Pipeline => this.pipeline;

            public IByteBufferAllocator Allocator => this.config.Allocator;

            public IChannel Read()
            {
                this.pipeline.Read();
                return this;
            }

            public IChannel Flush()
            {
                this.pipeline.Flush();
                return this;
            }

            public Task BindAsync(EndPoint localAddress)
            {
                return this.pipeline.BindAsync(localAddress);
            }

            public Task ConnectAsync(EndPoint remoteAddress)
            {
                return this.pipeline.ConnectAsync(remoteAddress);
            }

            public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
            {
                return this.pipeline.ConnectAsync(remoteAddress, localAddress);
            }

            public Task DisconnectAsync()
            {
                return this.pipeline.DisconnectAsync();
            }

            public Task DisconnectAsync(IPromise promise)
            {
                return this.pipeline.DisconnectAsync(promise);
            }

            public Task CloseAsync()
            {
                return this.pipeline.CloseAsync();
            }

            public Task CloseAsync(IPromise promise)
            {
                return this.pipeline.CloseAsync(promise);
            }

            public Task DeregisterAsync()
            {
                return this.pipeline.DeregisterAsync();
            }

            public Task DeregisterAsync(IPromise promise)
            {
                return this.pipeline.DeregisterAsync(promise);
            }

            public Task WriteAndFlushAsync(object message)
            {
                return this.pipeline.WriteAndFlushAsync(message);
            }

            public Task WriteAndFlushAsync(object message, IPromise promise)
            {
                return this.pipeline.WriteAndFlushAsync(message, promise);
            }

            public Task WriteAsync(object message)
            {
                return this.pipeline.WriteAsync(message);
            }

            public Task WriteAsync(object message, IPromise promise)
            {
                return this.pipeline.WriteAsync(message, promise);
            }

            public IPromise NewPromise()
            {
                return this.pipeline.NewPromise();
            }

            public IPromise NewPromise(object state)
            {
                return this.pipeline.NewPromise(state);
            }

            public IPromise VoidPromise()
            {
                return this.pipeline.VoidPromise();
            }

            public override int GetHashCode()
            {
                return this.channelId.GetHashCode();
            }

            public int CompareTo(IChannel other)
            {
                if (ReferenceEquals(this, other)) { return 0; }
                return this.channelId.CompareTo(other.Id);
            }

            public override bool Equals(object obj)
            {
                return ReferenceEquals(this, obj);
            }

            public bool Equals(IChannel other)
            {
                return ReferenceEquals(this, other);
            }

            public override string ToString()
            {
                return base.ToString();
            }

            internal void WritabilityChanged(bool writable)
            {
                Debug.Assert(this.EventLoop.InEventLoop);
                if (writable != this.InternalWritable && this.Active)
                {
                    // Only notify if we received a state change.
                    this.InternalWritable = writable;
                    this.pipeline.FireChannelWritabilityChanged();
                }
            }

            /// <summary>
            /// Receive a read message. This does not notify handlers unless a read is in progress on the channel.
            /// </summary>
            /// <param name="frame"></param>
            internal void FireChildRead(IHttp2Frame frame)
            {
                Debug.Assert(this.EventLoop.InEventLoop);
                if (!this.Active)
                {
                    ReferenceCountUtil.Release(frame);
                }
                else if (this.readStatus != ReadStatus.Idle)
                {
                    // If a read is in progress or has been requested, there cannot be anything in the queue,
                    // otherwise we would have drained it from the queue and processed it during the read cycle.
                    Debug.Assert(this.inboundBuffer == null || this.inboundBuffer.IsEmpty);
                    var allocHandle = this.channelUnsafe.RecvBufAllocHandle;
                    this.channelUnsafe.DoRead0(frame, allocHandle);
                    // We currently don't need to check for readEOS because the parent channel and child channel are limited
                    // to the same EventLoop thread. There are a limited number of frame types that may come after EOS is
                    // read (unknown, reset) and the trade off is less conditionals for the hot path (headers/data) at the
                    // cost of additional readComplete notifications on the rare path.
                    if (allocHandle.ContinueReading())
                    {
                        this.multiCodec.TryAddChildChannelToReadPendingQueue(this);
                    }
                    else
                    {
                        this.multiCodec.TryRemoveChildChannelFromReadPendingQueue(this);
                        this.channelUnsafe.NotifyReadComplete(allocHandle);
                    }
                }
                else
                {
                    if (this.inboundBuffer == null)
                    {
                        this.inboundBuffer = new Deque<object>(4);
                    }
                    this.inboundBuffer.AddToBack(frame);
                }
            }

            internal void FireChildReadComplete()
            {
                Debug.Assert(this.EventLoop.InEventLoop);
                Debug.Assert(this.readStatus != ReadStatus.Idle);
                this.channelUnsafe.NotifyReadComplete(this.channelUnsafe.RecvBufAllocHandle);
            }

            sealed class Http2ChannelPipeline : DefaultChannelPipeline
            {
                public Http2ChannelPipeline(DefaultHttp2StreamChannel channel) : base(channel) { }

                protected override void IncrementPendingOutboundBytes(long size)
                {
                    // Do thing for now
                }

                protected override void DecrementPendingOutboundBytes(long size)
                {
                    // Do thing for now
                }
            }

            sealed class Http2ChannelUnsafe : IChannelUnsafe
            {
                private readonly DefaultHttp2StreamChannel channel;
                private readonly IPromise unsafeVoidPromise;
                private IRecvByteBufAllocatorHandle recvHandle;
                private bool writeDoneAndNoFlush;
                private int _closeInitiated = Constants.False;
                private bool CloseInitiated
                {
                    get => Constants.True == Volatile.Read(ref _closeInitiated);
                    set => Interlocked.Exchange(ref _closeInitiated, value ? Constants.True : Constants.False);
                }

                private int _readEOS = Constants.False;
                internal bool ReadEOS
                {
                    get => Constants.True == Volatile.Read(ref _readEOS);
                    set => Interlocked.Exchange(ref _readEOS, value ? Constants.True : Constants.False);
                }

                public Http2ChannelUnsafe(DefaultHttp2StreamChannel channel)
                {
                    this.channel = channel;
                    this.unsafeVoidPromise = new VoidChannelPromise(channel, false);
                }

                public void Initialize(IChannel channel)
                {
                }

                public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
                {
                    return TaskUtil.FromException(new NotSupportedException());
                }

                public IRecvByteBufAllocatorHandle RecvBufAllocHandle
                {
                    get
                    {
                        if (null == this.recvHandle)
                        {
                            var config = this.channel.Configuration;
                            this.recvHandle = config.RecvByteBufAllocator.NewHandle();
                            this.recvHandle.Reset(config);

                        }
                        return this.recvHandle;
                    }
                }

                public Task RegisterAsync(IEventLoop eventLoop)
                {
                    var ch = this.channel;
                    if (ch.InternalRegistered)
                    {
                        throw new NotSupportedException("Re-register is not supported");
                    }

                    ch.InternalRegistered = true;

                    var pipeline = ch.Pipeline;
                    if (!ch.outbound)
                    {
                        // Add the handler to the pipeline now that we are registered.
                        pipeline.AddLast(ch.multiCodec.inboundStreamHandler);
                    }

                    pipeline.FireChannelRegistered();
                    if (ch.Active)
                    {
                        pipeline.FireChannelActive();
                    }

                    return TaskUtil.Completed;
                }

                public Task BindAsync(EndPoint localAddress)
                {
                    return TaskUtil.FromException(new NotSupportedException());
                }

                public void Disconnect(IPromise promise) => this.Close(promise);

                public void CloseForcibly() => this.Close(this.VoidPromise());

                public void Close(IPromise promise)
                {
                    if (!promise.SetUncancellable()) { return; }

                    var ch = this.channel;
                    if (this.CloseInitiated)
                    {
                        var closeCompletion = ch.CloseCompletion;
                        if (closeCompletion.IsCompleted)
                        {
                            // Closed already.
                            promise.Complete();
                        }
                        else if (!promise.IsVoid) // Only needed if no VoidChannelPromise.
                        {
                            // This means close() was called before so we just register a listener and return
                            closeCompletion.LinkOutcome(promise);
                        }
                        return;
                    }
                    this.CloseInitiated = true;

                    ch.multiCodec.TryRemoveChildChannelFromReadPendingQueue(ch);

                    bool wasActive = ch.Active;

                    // Only ever send a reset frame if the connection is still alive and if the stream may have existed
                    // as otherwise we may send a RST on a stream in an invalid state and cause a connection error.
                    if (ch.Parent.Active && !this.ReadEOS && ch.multiCodec.Connection.StreamMayHaveExisted(ch.stream.Id))
                    {
                        IHttp2StreamFrame resetFrame = new DefaultHttp2ResetFrame(Http2Error.Cancel) { Stream = ch.stream };
                        this.Write(resetFrame, this.VoidPromise());
                        this.Flush();
                    }

                    var inboundBuffer = ch.inboundBuffer;
                    if (inboundBuffer != null)
                    {
                        while (inboundBuffer.TryRemoveFromFront(out var msg))
                        {
                            ReferenceCountUtil.Release(msg);
                        }
                    }

                    // The promise should be notified before we call fireChannelInactive().
                    ch.OutboundClosed = true;
                    ch.closePromise.Complete();
                    promise.Complete();

                    this.FireChannelInactiveAndDeregister(this.VoidPromise(), wasActive);
                }

                public void Deregister(IPromise promise)
                {
                    this.FireChannelInactiveAndDeregister(promise, false);
                }

                private void FireChannelInactiveAndDeregister(IPromise promise, bool fireChannelInactive)
                {
                    if (!promise.SetUncancellable()) { return; }

                    var ch = this.channel;
                    if (!ch.InternalRegistered)
                    {
                        promise.Complete();
                        return;
                    }

                    // As a user may call deregister() from within any method while doing processing in the ChannelPipeline,
                    // we need to ensure we do the actual deregister operation later. This is necessary to preserve the
                    // behavior of the AbstractChannel, which always invokes channelUnregistered and channelInactive
                    // events 'later' to ensure the current events in the handler are completed before these events.
                    //
                    // See:
                    // https://github.com/netty/netty/issues/4435
                    this.InvokeLater(() =>
                    {
                        if (fireChannelInactive)
                        {
                            ch.pipeline.FireChannelInactive();
                        }
                        // The user can fire `deregister` events multiple times but we only want to fire the pipeline
                        // event if the channel was actually registered.
                        if (ch.InternalRegistered)
                        {
                            ch.InternalRegistered = false;
                            ch.pipeline.FireChannelUnregistered();
                        }
                        Util.SafeSetSuccess(promise, Logger);
                    });
                }

                private void InvokeLater(Action task)
                {
                    try
                    {
                        // This method is used by outbound operation implementations to trigger an inbound event later.
                        // They do not trigger an inbound event immediately because an outbound operation might have been
                        // triggered by another inbound event handler method.  If fired immediately, the call stack
                        // will look like this for example:
                        //
                        //   handlerA.inboundBufferUpdated() - (1) an inbound handler method closes a connection.
                        //   -> handlerA.ctx.close()
                        //     -> channel.unsafe.close()
                        //       -> handlerA.channelInactive() - (2) another inbound handler method called while in (1) yet
                        //
                        // which means the execution of two inbound handler methods of the same handler overlap undesirably.
                        this.channel.EventLoop.Execute(task);
                    }
                    catch (RejectedExecutionException e)
                    {
                        if (Logger.WarnEnabled) { Logger.CanotInvokeTaskLaterAsEventLoopRejectedIt(e); }
                    }
                }

                public void BeginRead()
                {
                    var ch = this.channel;
                    if (!ch.Active)
                    {
                        return;
                    }
                    switch (ch.readStatus)
                    {
                        case ReadStatus.Idle:
                            ch.readStatus = ReadStatus.InProgress;
                            this.DoBeginRead();
                            break;
                        case ReadStatus.InProgress:
                            ch.readStatus = ReadStatus.Requested;
                            break;
                        default:
                            break;
                    }
                }

                internal void DoBeginRead()
                {
                    var ch = this.channel;
                    var inboundBuffer = ch.inboundBuffer;
                    if (inboundBuffer == null || (!inboundBuffer.TryRemoveFromFront(out var message)))
                    {
                        if (this.ReadEOS)
                        {
                            ch.Unsafe.CloseForcibly();
                        }
                    }
                    else
                    {
                        var allocHandle = this.RecvBufAllocHandle;
                        allocHandle.Reset(ch.config);
                        var continueReading = false;
                        do
                        {
                            this.DoRead0((IHttp2Frame)message, allocHandle);
                        } while ((this.ReadEOS || (continueReading = allocHandle.ContinueReading())) &&
                                 inboundBuffer.TryRemoveFromFront(out message));

                        if (continueReading && ch.multiCodec.parentReadInProgress && !this.ReadEOS)
                        {
                            // Currently the parent and child channel are on the same EventLoop thread. If the parent is
                            // currently reading it is possile that more frames will be delivered to this child channel. In
                            // the case that this child channel still wants to read we delay the channelReadComplete on this
                            // child channel until the parent is done reading.
                            Debug.Assert(!ch.multiCodec.IsChildChannelInReadPendingQueue(ch));
                            ch.multiCodec.AddChildChannelToReadPendingQueue(ch);
                        }
                        else
                        {
                            this.NotifyReadComplete(allocHandle);
                        }
                    }
                }

                //internal void ReadEOS()
                //{
                //    this.ReadEOS = true;
                //}

                internal void NotifyReadComplete(IRecvByteBufAllocatorHandle allocHandle)
                {
                    var ch = this.channel;
                    Debug.Assert(ch.next == null && ch.previous == null);
                    if (ch.readStatus == ReadStatus.Requested)
                    {
                        ch.readStatus = ReadStatus.InProgress;
                    }
                    else
                    {
                        ch.readStatus = ReadStatus.Idle;
                    }
                    allocHandle.ReadComplete();
                    ch.pipeline.FireChannelReadComplete();
                    // Reading data may result in frames being written (e.g. WINDOW_UPDATE, RST, etc..). If the parent
                    // channel is not currently reading we need to force a flush at the child channel, because we cannot
                    // rely upon flush occurring in channelReadComplete on the parent channel.
                    this.Flush();
                    if (this.ReadEOS)
                    {
                        ch.Unsafe.CloseForcibly();
                    }
                }

                internal void DoRead0(IHttp2Frame frame, IRecvByteBufAllocatorHandle allocHandle)
                {
                    var ch = this.channel;
                    var pipeline = ch.pipeline;
                    pipeline.FireChannelRead(frame);
                    allocHandle.IncMessagesRead(1);

                    if (frame is IHttp2DataFrame dataFrame)
                    {
                        int numBytesToBeConsumed = dataFrame.InitialFlowControlledBytes;
                        allocHandle.AttemptedBytesRead = numBytesToBeConsumed;
                        allocHandle.LastBytesRead = numBytesToBeConsumed;
                        if (numBytesToBeConsumed != 0)
                        {
                            try
                            {
                                var multiCodec = ch.multiCodec;
                                this.writeDoneAndNoFlush |= multiCodec.OnBytesConsumed(multiCodec.InternalContext, ch.stream, numBytesToBeConsumed);
                            }
                            catch (Http2Exception e)
                            {
                                pipeline.FireExceptionCaught(e);
                            }
                        }
                    }
                    else
                    {
                        allocHandle.AttemptedBytesRead = MinHttp2FrameSize;
                        allocHandle.LastBytesRead = MinHttp2FrameSize;
                    }
                }

                public void Write(object msg, IPromise promise)
                {
                    // After this point its not possible to cancel a write anymore.
                    if (!promise.SetUncancellable())
                    {
                        ReferenceCountUtil.Release(msg);
                        return;
                    }

                    var ch = this.channel;

                    if (!ch.Active ||
                            // Once the outbound side was closed we should not allow header / data frames
                            ch.OutboundClosed && (msg is IHttp2HeadersFrame || msg is IHttp2DataFrame))
                    {
                        ReferenceCountUtil.Release(msg);
                        promise.SetException(ClosedChannelException);
                        return;
                    }

                    try
                    {
                        if (msg is IHttp2StreamFrame streamFrame)
                        {
                            var frame = this.ValidateStreamFrame(streamFrame);
                            frame.Stream = ch.stream;
                            if (!ch.firstFrameWritten && !Http2CodecUtil.IsStreamIdValid(ch.stream.Id))
                            {
                                if (!(frame is IHttp2HeadersFrame))
                                {
                                    ReferenceCountUtil.Release(frame);
                                    promise.SetException(ThrowHelper.GetArgumentException_FirstFrameMustBeHeadersFrame(frame));
                                    return;
                                }
                                ch.firstFrameWritten = true;
                                var future = this.Write0(frame);
                                if (future.IsCompleted)
                                {
                                    this.FirstWriteComplete(future, promise);
                                }
                                else
                                {
#if NET40
                                    future.ContinueWith(t => FirstWriteCompleteAfterWrite(t, Tuple.Create(this, promise)),
                                        TaskContinuationOptions.ExecuteSynchronously);
#else
                                    future.ContinueWith(FirstWriteCompleteAfterWriteAction,
                                        Tuple.Create(this, promise), TaskContinuationOptions.ExecuteSynchronously);
#endif
                                }
                                return;
                            }
                        }
                        else
                        {
                            ReferenceCountUtil.Release(msg);
                            promise.SetException(ThrowHelper.GetArgumentException_MsgMustBeStreamFrame(msg));
                            return;
                        }

                        var writeTask = this.Write0(msg);
                        if (writeTask.IsCompleted)
                        {
                            this.WriteComplete(writeTask, promise);
                        }
                        else
                        {
#if NET40
                            writeTask.ContinueWith(t => WriteCompleteContinute(t, Tuple.Create(this, promise)),
                                TaskContinuationOptions.ExecuteSynchronously);
#else
                            writeTask.ContinueWith(WriteCompleteContinuteAction,
                                Tuple.Create(this, promise), TaskContinuationOptions.ExecuteSynchronously);
#endif
                        }
                    }
                    catch (Exception t)
                    {
                        promise.SetException(t);
                    }
                    finally
                    {
                        this.writeDoneAndNoFlush = true;
                    }
                }

                private static readonly Action<Task, object> FirstWriteCompleteAfterWriteAction = FirstWriteCompleteAfterWrite;
                private static void FirstWriteCompleteAfterWrite(Task t, object s)
                {
                    var wrapped = (Tuple<Http2ChannelUnsafe, IPromise>)s;
                    wrapped.Item1.FirstWriteComplete(t, wrapped.Item2);
                }

                private void FirstWriteComplete(Task future, IPromise promise)
                {
                    if (future.IsSuccess())
                    {
                        // As we just finished our first write which made the stream-id valid we need to re-evaluate
                        // the writability of the channel.
                        var ch = this.channel;
                        var multiCodec = this.channel.multiCodec;
                        ch.WritabilityChanged(multiCodec.IsWritable(ch.stream));
                        promise.Complete();
                    }
                    else
                    {
                        // If the first write fails there is not much we can do, just close
                        this.CloseForcibly();
                        promise.SetException(this.WrapStreamClosedError(future.Exception.InnerException));
                    }
                }

                private static readonly Action<Task, object> WriteCompleteContinuteAction = WriteCompleteContinute;
                private static void WriteCompleteContinute(Task t, object s)
                {
                    var wrapped = (Tuple<Http2ChannelUnsafe, IPromise>)s;
                    wrapped.Item1.WriteComplete(t, wrapped.Item2);
                }

                private void WriteComplete(Task future, IPromise promise)
                {
                    if (future.IsSuccess())
                    {
                        promise.Complete();
                    }
                    else
                    {
                        var cause = future.Exception.InnerException;
                        var error = this.WrapStreamClosedError(cause);
                        if (error is ClosedChannelException)
                        {
                            if (this.channel.config.AutoClose)
                            {
                                // Close channel if needed.
                                this.CloseForcibly();
                            }
                            else
                            {
                                this.channel.OutboundClosed = true;
                            }
                        }
                        promise.SetException(error);
                    }
                }

                private Exception WrapStreamClosedError(Exception cause)
                {
                    // If the error was caused by STREAM_CLOSED we should use a ClosedChannelException to better
                    // mimic other transports and make it easier to reason about what exceptions to expect.
                    if (cause is Http2Exception http2Exception && http2Exception.Error == Http2Error.StreamClosed)
                    {
                        return new ClosedChannelException(cause.Message, cause);
                    }
                    return cause;
                }

                private IHttp2StreamFrame ValidateStreamFrame(IHttp2StreamFrame frame)
                {
                    var frameStream = frame.Stream;
                    if (frameStream != null && frameStream != this.channel.stream)
                    {
                        ReferenceCountUtil.Release(frame);
                        ThrowHelper.ThrowArgumentException_StreamMustNotBeSetOnTheFrame(frame);
                    }
                    return frame;
                }

                private Task Write0(object msg)
                {
                    var multiCodec = this.channel.multiCodec;
                    var ctx = multiCodec.InternalContext;
                    var promise = ctx.NewPromise();
                    multiCodec.Write(ctx, msg, promise);
                    return promise.Task;
                }

                public void Flush()
                {
                    // If we are currently in the parent channel's read loop we should just ignore the flush.
                    // We will ensure we trigger ctx.flush() after we processed all Channels later on and
                    // so aggregate the flushes. This is done as ctx.flush() is expensive when as it may trigger an
                    // write(...) or writev(...) operation on the socket.
                    var multiCodec = this.channel.multiCodec;
                    if (!this.writeDoneAndNoFlush || multiCodec.parentReadInProgress)
                    {
                        // There is nothing to flush so this is a NOOP.
                        return;
                    }
                    try
                    {
                        multiCodec.Flush0(multiCodec.InternalContext);
                    }
                    finally
                    {
                        this.writeDoneAndNoFlush = false;
                    }
                }

                public IPromise VoidPromise() => this.unsafeVoidPromise;

                // Always return null as we not use the ChannelOutboundBuffer and not even support it.
                public ChannelOutboundBuffer OutboundBuffer => null;
            }

            sealed class Http2StreamChannelConfiguration : DefaultChannelConfiguration
            {
                readonly DefaultHttp2StreamChannel http2Channel;
                public Http2StreamChannelConfiguration(DefaultHttp2StreamChannel channel)
                    : base(channel)
                {
                    this.http2Channel = channel;
                }

                public override int WriteBufferHighWaterMark
                {
                    get => Math.Min(this.http2Channel.Parent.Configuration.WriteBufferHighWaterMark, this.http2Channel.multiCodec.initialOutboundStreamWindow);
                    set => throw new NotSupportedException();
                }

                public override int WriteBufferLowWaterMark
                {
                    get => Math.Min(this.http2Channel.Parent.Configuration.WriteBufferLowWaterMark, this.http2Channel.multiCodec.initialOutboundStreamWindow);
                    set => throw new NotSupportedException();
                }

                public override IMessageSizeEstimator MessageSizeEstimator
                {
                    get => FlowControlledFrameSizeEstimator.Instance;
                    set => throw new NotSupportedException();
                }
            }
        }
    }
}
