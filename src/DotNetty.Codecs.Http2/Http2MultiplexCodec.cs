// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// An HTTP/2 handler that creates child channels for each stream.
    ///
    /// <para>When a new stream is created, a new <see cref="IChannel"/> is created for it. Applications send and
    /// receive <see cref="IHttp2StreamFrame"/>s on the created channel. <see cref="IByteBuffer"/>s cannot be processed by the channel;
    /// all writes that reach the head of the pipeline must be an instance of <see cref="IHttp2StreamFrame"/>. Writes that reach
    /// the head of the pipeline are processed directly by this handler and cannot be intercepted.</para>
    ///
    /// <para>The child channel will be notified of user events that impact the stream, such as <see
    /// cref="IHttp2GoAwayFrame"/> and <see cref="IHttp2ResetFrame"/>, as soon as they occur. Although <see
    /// cref="IHttp2GoAwayFrame"/> and <see cref="IHttp2ResetFrame"/> signify that the remote is ignoring further
    /// communication, closing of the channel is delayed until any inbound queue is drained with <see
    /// cref="IChannel.Read()"/>, which follows the default behavior of channels in Netty. Applications are
    /// free to close the channel in response to such events if they don't have use for any queued
    /// messages. Any connection level events like <see cref="IHttp2SettingsFrame"/> and <see cref="IHttp2GoAwayFrame"/>
    /// will be processed internally and also propagated down the pipeline for other handlers to act on.</para>
    ///
    /// <para>Outbound streams are supported via the <see cref="Http2StreamChannelBootstrap"/>.</para>
    ///
    /// <para><see cref="IChannelConfiguration.MaxMessagesPerRead"/> and <see cref="IChannelConfiguration.IsAutoRead"/> are supported.</para>
    ///
    /// <h3>Reference Counting</h3>
    ///
    /// <para>Some <see cref="IHttp2StreamFrame"/>s implement the <see cref="IReferenceCounted"/> interface, as they carry
    /// reference counted objects (e.g. <see cref="IByteBuffer"/>s). The multiplex codec will call <see cref="IReferenceCounted.Retain()"/>
    /// before propagating a reference counted object through the pipeline, and thus an application handler needs to release
    /// such an object after having consumed it. For more information on reference counting take a look at
    /// https://netty.io/wiki/reference-counted-objects.html </para>
    ///
    /// <h3>Channel Events</h3>
    ///
    /// <para>A child channel becomes active as soon as it is registered to an <see cref="IEventLoop"/>. Therefore, an active channel
    /// does not map to an active HTTP/2 stream immediately. Only once a <see cref="IHttp2HeadersFrame"/> has been successfully sent
    /// or received, does the channel map to an active HTTP/2 stream. In case it is not possible to open a new HTTP/2 stream
    /// (i.e. due to the maximum number of active streams being exceeded), the child channel receives an exception
    /// indicating the cause and is closed immediately thereafter.</para>
    ///
    /// <h3>Writability and Flow Control</h3>
    ///
    /// <para>A child channel observes outbound/remote flow control via the channel's writability. A channel only becomes writable
    /// when it maps to an active HTTP/2 stream and the stream's flow control window is greater than zero. A child channel
    /// does not know about the connection-level flow control window. <see cref="IChannelHandler"/>s are free to ignore the
    /// channel's writability, in which case the excessive writes will be buffered by the parent channel. It's important to
    /// note that only <see cref="IHttp2DataFrame"/>s are subject to HTTP/2 flow control.</para>
    ///
    /// @deprecated use <see cref="Http2FrameCodecBuilder"/> together with <see cref="Http2MultiplexHandler"/>.
    /// </summary>
    public class Http2MultiplexCodec : Http2FrameCodec, IHasParentContext
    {
        private readonly IChannelHandler _inboundStreamHandler;
        private readonly IChannelHandler _upgradeStreamHandler;
        private readonly MaxCapacityQueue<AbstractHttp2StreamChannel> _readCompletePendingQueue =
            // Choose 100 which is what is used most of the times as default.
            new MaxCapacityQueue<AbstractHttp2StreamChannel>(Http2CodecUtil.SmallestMaxConcurrentStreams);

        private bool _parentReadInProgress;

        private int v_idCount;
        private int NextId => Interlocked.Increment(ref v_idCount);

        // Need to be volatile as accessed from within the Http2MultiplexHandlerStreamChannel in a multi-threaded fashion.
        private IChannelHandlerContext v_ctx;
        private IChannelHandlerContext InternalContext
        {
            get => Volatile.Read(ref v_ctx);
            set => Interlocked.Exchange(ref v_ctx, value);
        }
        IChannelHandlerContext IHasParentContext.Context => Volatile.Read(ref v_ctx);

        public Http2MultiplexCodec(IHttp2ConnectionEncoder encoder, IHttp2ConnectionDecoder decoder, Http2Settings initialSettings,
            IChannelHandler inboundStreamHandler, IChannelHandler upgradeStreamHandler, bool decoupleCloseAndGoAway)
            : base(encoder, decoder, initialSettings, decoupleCloseAndGoAway)
        {
            _inboundStreamHandler = inboundStreamHandler;
            _upgradeStreamHandler = upgradeStreamHandler;
        }

        /// <inheritdoc />
        public override void OnHttpClientUpgrade()
        {
            // We must have an upgrade handler or else we can't handle the stream
            if (_upgradeStreamHandler is null)
            {
                ThrowHelper.ThrowConnectionError_ClientIsMisconfiguredForUpgradeRequests();
            }
            // Creates the Http2Stream in the Connection.
            base.OnHttpClientUpgrade();
        }

        /// <inheritdoc />
        protected sealed override void HandlerAdded0(IChannelHandlerContext ctx)
        {
            if (ctx.Executor != ctx.Channel.EventLoop)
            {
                ThrowHelper.ThrowInvalidOperationException_EventExecutorMustBeEventLoopOfChannel();
            }
            InternalContext = ctx;
        }

        /// <inheritdoc />
        protected sealed override void HandlerRemovedInternal(IChannelHandlerContext ctx)
        {
            _readCompletePendingQueue.Clear();
        }

        /// <inheritdoc />
        protected sealed override void OnHttp2Frame(IChannelHandlerContext ctx, IHttp2Frame frame)
        {
            if (frame is IHttp2StreamFrame streamFrame)
            {
                var channel = (AbstractHttp2StreamChannel)
                        ((DefaultHttp2FrameStream)streamFrame.Stream).Attachment;
                channel.FireChildRead(streamFrame);
                return;
            }
            if (frame is IHttp2GoAwayFrame goAwayFrame)
            {
                OnHttp2GoAwayFrame(ctx, goAwayFrame);
            }
            // Send frames down the pipeline
            _ = ctx.FireChannelRead(frame);
        }

        /// <inheritdoc />
        protected sealed override void OnHttp2StreamStateChanged(IChannelHandlerContext ctx, DefaultHttp2FrameStream stream)
        {
            switch (stream.State)
            {
                case Http2StreamState.HalfClosedLocal:
                    if (stream.Id != Http2CodecUtil.HttpUpgradeStreamId)
                    {
                        // Ignore everything which was not caused by an upgrade
                        break;
                    }
                    goto case Http2StreamState.Open;

                case Http2StreamState.HalfClosedRemote:
                case Http2StreamState.Open:
                    if (stream.Attachment is object)
                    {
                        // ignore if child channel was already created.
                        break;
                    }
                    Http2MultiplexCodecStreamChannel streamChannel;
                    // We need to handle upgrades special when on the client side.
                    if (stream.Id == Http2CodecUtil.HttpUpgradeStreamId && !Connection.IsServer)
                    {
                        // Add our upgrade handler to the channel and then register the channel.
                        // The register call fires the channelActive, etc.
                        Debug.Assert(_upgradeStreamHandler is object);
                        streamChannel = new Http2MultiplexCodecStreamChannel(this, stream, _upgradeStreamHandler)
                        {
                            OutboundClosed = true
                        };
                    }
                    else
                    {
                        streamChannel = new Http2MultiplexCodecStreamChannel(this, stream, _inboundStreamHandler);
                    }
                    var future = ctx.Channel.EventLoop.RegisterAsync(streamChannel);
                    if (future.IsCompleted)
                    {
                        Http2MultiplexHandler.RegisterDone(future, streamChannel);
                    }
                    else
                    {
                        _ = future.ContinueWith(Http2MultiplexHandler.RegisterDoneAction, streamChannel, TaskContinuationOptions.ExecuteSynchronously);
                    }
                    break;

                case Http2StreamState.Closed:
                    if (stream.Attachment is AbstractHttp2StreamChannel channel)
                    {
                        channel.StreamClosed();
                    }
                    break;

                default:
                    // ignore for now
                    break;
            }
        }

        // TODO: This is most likely not the best way to expose this, need to think more about it.
        internal IHttp2StreamChannel NewOutboundStream()
        {
            return new Http2MultiplexCodecStreamChannel(this, NewStream(), null);
        }

        /// <inheritdoc />
        protected sealed override void OnHttp2FrameStreamException(IChannelHandlerContext ctx, Http2FrameStreamException cause)
        {
            IHttp2FrameStream stream = cause.Stream;
            var channel = (AbstractHttp2StreamChannel)((DefaultHttp2FrameStream)stream).Attachment;

            try
            {
                _ = channel.Pipeline.FireExceptionCaught(cause.InnerException);
            }
            finally
            {
                channel.Unsafe.CloseForcibly();
            }
        }

        private void OnHttp2GoAwayFrame(IChannelHandlerContext ctx, IHttp2GoAwayFrame goAwayFrame)
        {
            try
            {
                ForEachActiveStream(stream => InternalStreamVisitor(stream, goAwayFrame, Connection));
            }
            catch (Http2Exception exc)
            {
                _ = ctx.FireExceptionCaught(exc);
                _ = ctx.CloseAsync();
            }
        }

        private static bool InternalStreamVisitor(IHttp2FrameStream stream, IHttp2GoAwayFrame goAwayFrame, IHttp2Connection connection)
        {
            int streamId = stream.Id;
            if (streamId > goAwayFrame.LastStreamId && connection.Local.IsValidStreamId(streamId))
            {
                var channel = (AbstractHttp2StreamChannel)
                        ((DefaultHttp2FrameStream)stream).Attachment;
                _ = channel.Pipeline.FireUserEventTriggered(goAwayFrame.RetainedDuplicate());
            }
            return true;
        }

        /// <summary>
        /// Notifies any child streams of the read completion.
        /// </summary>
        /// <param name="ctx"></param>
        public sealed override void ChannelReadComplete(IChannelHandlerContext ctx)
        {
            ProcessPendingReadCompleteQueue();
            ChannelReadComplete0(ctx);
        }

        private void ProcessPendingReadCompleteQueue()
        {
            _parentReadInProgress = true;
            try
            {
                // If we have many child channel we can optimize for the case when multiple call flush() in
                // channelReadComplete(...) callbacks and only do it once as otherwise we will end-up with multiple
                // write calls on the socket which is expensive.
                while (_readCompletePendingQueue.TryDequeue(out var childChannel))
                {
                    childChannel.FireChildReadComplete();
                }
            }
            finally
            {
                _parentReadInProgress = false;
                _readCompletePendingQueue.Clear();
                // We always flush as this is what Http2ConnectionHandler does for now.
                Flush0(InternalContext);
            }
        }

        /// <inheritdoc />
        public sealed override void ChannelRead(IChannelHandlerContext context, object message)
        {
            _parentReadInProgress = true;
            base.ChannelRead(context, message);
        }

        /// <inheritdoc />
        public override void ChannelWritabilityChanged(IChannelHandlerContext ctx)
        {
            if (ctx.Channel.IsWritable)
            {
                // While the writability state may change during iterating of the streams we just set all of the streams
                // to writable to not affect fairness. These will be "limited" by their own watermarks in any case.
                ForEachActiveStream(AbstractHttp2StreamChannel.WritableVisitor);
            }

            base.ChannelWritabilityChanged(ctx);
        }

        private void Flush0(IChannelHandlerContext ctx)
        {
            Flush(ctx);
        }

        sealed class Http2MultiplexCodecStreamChannel : AbstractHttp2StreamChannel
        {
            private readonly Http2MultiplexCodec _owner;

            public Http2MultiplexCodecStreamChannel(Http2MultiplexCodec owner, DefaultHttp2FrameStream stream, IChannelHandler inboundHandler)
                : base(stream, owner.NextId, inboundHandler, owner)
            {
                _owner = owner;
            }

            protected override bool IsParentReadInProgress => _owner._parentReadInProgress;

            protected override void AddChannelToReadCompletePendingQueue()
            {
                var readCompletePendingQueue = _owner._readCompletePendingQueue;
                // If there is no space left in the queue, just keep on processing everything that is already
                // stored there and try again.
                while (!readCompletePendingQueue.TryEnqueue(this))
                {
                    _owner.ProcessPendingReadCompleteQueue();
                }
            }

            protected override IChannelHandlerContext ParentContext => _owner.InternalContext;

            protected override Task InternalWriteAsync(IChannelHandlerContext ctx, object msg)
            {
                var promise = ctx.NewPromise();
                _owner.Write(ctx, msg, promise);
                return promise.Task;
            }

            protected override void Flush0(IChannelHandlerContext ctx)
            {
                _owner.Flush0(ctx);
            }
        }
    }
}
