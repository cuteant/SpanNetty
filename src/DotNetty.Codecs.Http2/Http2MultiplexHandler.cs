/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// An HTTP/2 handler that creates child channels for each stream. This handler must be used in combination
    /// with <see cref="Http2FrameCodec"/>.
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
    /// when it maps to an active HTTP/2 stream . A child channel does not know about the connection-level flow control
    /// window. <see cref="IChannelHandler"/>s are free to ignore the channel's writability, in which case the excessive writes will
    /// be buffered by the parent channel. It's important to note that only <see cref="IHttp2DataFrame"/>s are subject to
    /// HTTP/2 flow control.</para>
    /// </summary>
    public sealed class Http2MultiplexHandler : Http2ChannelDuplexHandler, IHasParentContext
    {
        internal static readonly Action<Task, object> RegisterDoneAction = (t, s) => RegisterDone(t, s);

        private readonly IChannelHandler _inboundStreamHandler;
        private readonly IChannelHandler _upgradeStreamHandler;
        private readonly MaxCapacityQueue<AbstractHttp2StreamChannel> _readCompletePendingQueue;

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

        /// <summary>Creates a new instance</summary>
        /// <param name="inboundStreamHandler">the <see cref="IChannelHandler"/> that will be added to the <see cref="IChannelPipeline"/> of
        /// the <see cref="IChannel"/>s created for new inbound streams.</param>
        public Http2MultiplexHandler(IChannelHandler inboundStreamHandler)
            : this(inboundStreamHandler, null)
        {
        }

        /// <summary>Creates a new instance</summary>
        /// <param name="inboundStreamHandler">the <see cref="IChannelHandler"/> that will be added to the <see cref="IChannelPipeline"/> of
        /// the <see cref="IChannel"/>s created for new inbound streams.</param>
        /// <param name="upgradeStreamHandler">the <see cref="IChannelHandler"/> that will be added to the <see cref="IChannelPipeline"/> of the
        /// upgraded <see cref="IChannel"/>.</param>
        public Http2MultiplexHandler(IChannelHandler inboundStreamHandler, IChannelHandler upgradeStreamHandler)
        {
            if (inboundStreamHandler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.inboundStreamHandler); }

            _readCompletePendingQueue =
                // Choose 100 which is what is used most of the times as default.
                new MaxCapacityQueue<AbstractHttp2StreamChannel>(Http2CodecUtil.SmallestMaxConcurrentStreams);

            _inboundStreamHandler = inboundStreamHandler;
            _upgradeStreamHandler = upgradeStreamHandler;
        }

        internal static void RegisterDone(Task future, object s)
        {
            // Handle any errors that occurred on the local thread while registering. Even though
            // failures can happen after this point, they will be handled by the channel by closing the
            // childChannel.
            if (future.IsFailure())
            {
                var childChannel = (IChannel)s;
                if (childChannel.IsRegistered)
                {
                    _ = childChannel.CloseAsync();
                }
                else
                {
                    childChannel.Unsafe.CloseForcibly();
                }
            }
        }

        /// <inheritdoc />
        protected override void HandlerAdded0(IChannelHandlerContext ctx)
        {
            if (ctx.Executor != ctx.Channel.EventLoop)
            {
                ThrowHelper.ThrowInvalidOperationException_EventExecutorMustBeEventLoopOfChannel();
            }
            InternalContext = ctx;
        }

        /// <inheritdoc />
        protected override void HandlerRemoved0(IChannelHandlerContext ctx)
        {
            _readCompletePendingQueue.Clear();
        }

        /// <inheritdoc />
        public override void ChannelRead(IChannelHandlerContext context, object msg)
        {
            _parentReadInProgress = true;
            switch (msg)
            {
                case IHttp2WindowUpdateFrame _:
                    // We dont want to propagate update frames to the user
                    return;

                case IHttp2StreamFrame streamFrame:
                    DefaultHttp2FrameStream s =
                            (DefaultHttp2FrameStream)streamFrame.Stream;

                    AbstractHttp2StreamChannel channel = (AbstractHttp2StreamChannel)s.Attachment;
                    if (msg is IHttp2ResetFrame)
                    {
                        // Reset frames needs to be propagated via user events as these are not flow-controlled and so
                        // must not be controlled by suppressing channel.read() on the child channel.
                        _ = channel.Pipeline.FireUserEventTriggered(msg);

                        // RST frames will also trigger closing of the streams which then will call
                        // AbstractHttp2StreamChannel.streamClosed()
                    }
                    else
                    {
                        channel.FireChildRead(streamFrame);
                    }
                    return;

                case IHttp2GoAwayFrame goAwayFrame:
                    // goaway frames will also trigger closing of the streams which then will call
                    // AbstractHttp2StreamChannel.streamClosed()
                    OnHttp2GoAwayFrame(context, goAwayFrame);
                    break;
            }

            // Send everything down the pipeline
            _ = context.FireChannelRead(msg);
        }

        /// <inheritdoc />
        public override void ChannelWritabilityChanged(IChannelHandlerContext context)
        {
            if (context.Channel.IsWritable)
            {
                // While the writability state may change during iterating of the streams we just set all of the streams
                // to writable to not affect fairness. These will be "limited" by their own watermarks in any case.
                ForEachActiveStream(AbstractHttp2StreamChannel.WritableVisitor);
            }

            _ = context.FireChannelWritabilityChanged();
        }

        /// <inheritdoc />
        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            if (!(evt is Http2FrameStreamEvent streamEvent))
            {
                _ = context.FireUserEventTriggered(evt);
                return;
            }

            var stream = (DefaultHttp2FrameStream)streamEvent.Stream;
            if (streamEvent.Type != Http2FrameStreamEvent.EventType.State) { return; }

            switch (stream.State)
            {
                case Http2StreamState.HalfClosedLocal:
                    if (stream.Id != Http2CodecUtil.HttpUpgradeStreamId)
                    {
                        // Ignore everything which was not caused by an upgrade
                        break;
                    }
                    goto case Http2StreamState.Open; // fall-through

                case Http2StreamState.HalfClosedRemote: // fall-through
                case Http2StreamState.Open:
                    if (stream.Attachment is object)
                    {
                        // ignore if child channel was already created.
                        break;
                    }
                    AbstractHttp2StreamChannel ch;
                    // We need to handle upgrades special when on the client side.
                    if (stream.Id == Http2CodecUtil.HttpUpgradeStreamId && !IsServer(context))
                    {
                        // We must have an upgrade handler or else we can't handle the stream
                        if (_upgradeStreamHandler is null)
                        {
                            ThrowHelper.ThrowConnectionError_ClientIsMisconfiguredForUpgradeRequests();
                        }
                        ch = new Http2MultiplexHandlerStreamChannel(this, stream, _upgradeStreamHandler)
                        {
                            OutboundClosed = true
                        };
                    }
                    else
                    {
                        ch = new Http2MultiplexHandlerStreamChannel(this, stream, _inboundStreamHandler);
                    }
                    var future = context.Channel.EventLoop.RegisterAsync(ch);
                    if (future.IsCompleted)
                    {
                        RegisterDone(future, ch);
                    }
                    else
                    {
                        _ = future.ContinueWith(RegisterDoneAction, ch, TaskContinuationOptions.ExecuteSynchronously);
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
            return new Http2MultiplexHandlerStreamChannel(this, (DefaultHttp2FrameStream)NewStream(), null);
        }

        /// <inheritdoc />
        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception exception)
        {
            if (exception is Http2FrameStreamException streamException)
            {
                var stream = streamException.Stream;
                var childChannel = (AbstractHttp2StreamChannel)
                        ((DefaultHttp2FrameStream)stream).Attachment;
                try
                {
                    _ = childChannel.Pipeline.FireExceptionCaught(exception.InnerException);
                }
                finally
                {
                    childChannel.Unsafe.CloseForcibly();
                }
                return;
            }
            _ = ctx.FireExceptionCaught(exception);
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        private static bool IsServer(IChannelHandlerContext ctx)
        {
            return ctx.Channel.Parent is IServerChannel;
        }

        private void OnHttp2GoAwayFrame(IChannelHandlerContext ctx, IHttp2GoAwayFrame goAwayFrame)
        {
            try
            {
                var server = IsServer(ctx);
                ForEachActiveStream(stream => InternalStreamVisitor(stream, goAwayFrame, server));
            }
            catch (Http2Exception exc)
            {
                _ = ctx.FireExceptionCaught(exc);
                _ = ctx.CloseAsync();
            }
        }

        private static bool InternalStreamVisitor(IHttp2FrameStream stream, IHttp2GoAwayFrame goAwayFrame, bool server)
        {
            int streamId = stream.Id;
            if (streamId > goAwayFrame.LastStreamId && Http2CodecUtil.IsStreamIdValid(streamId, server))
            {
                var childChannel = (AbstractHttp2StreamChannel)
                        ((DefaultHttp2FrameStream)stream).Attachment;
                _ = childChannel.Pipeline.FireUserEventTriggered(goAwayFrame.RetainedDuplicate());
            }
            return true;
        }

        /// <summary>
        /// Notifies any child streams of the read completion.
        /// </summary>
        /// <param name="context"></param>
        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            ProcessPendingReadCompleteQueue();
            _ = context.FireChannelReadComplete();
        }

        private void ProcessPendingReadCompleteQueue()
        {
            _parentReadInProgress = true;
            // If we have many child channel we can optimize for the case when multiple call flush() in
            // channelReadComplete(...) callbacks and only do it once as otherwise we will end-up with multiple
            // write calls on the socket which is expensive.
            if (_readCompletePendingQueue.TryDequeue(out var childChannel))
            {
                try
                {
                    do
                    {
                        childChannel.FireChildReadComplete();
                    } while (_readCompletePendingQueue.TryDequeue(out childChannel));
                }
                finally
                {
                    _parentReadInProgress = false;
                    _readCompletePendingQueue.Clear();
                    _ = InternalContext.Flush();
                }
            }
            else
            {
                _parentReadInProgress = false;
            }
        }

        sealed class Http2MultiplexHandlerStreamChannel : AbstractHttp2StreamChannel
        {
            private readonly Http2MultiplexHandler _owner;

            public Http2MultiplexHandlerStreamChannel(Http2MultiplexHandler owner, DefaultHttp2FrameStream stream, IChannelHandler inboundHandler)
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
        }
    }
}
