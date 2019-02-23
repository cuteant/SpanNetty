// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// <c>This API is very immature.</c> The Http2Connection-based API is currently preferred over this API.
    /// This API is targeted to eventually replace or reduce the need for the <see cref="Http2ConnectionHandler"/> API.
    ///
    /// <para>A HTTP/2 handler that maps HTTP/2 frames to <see cref="IHttp2Frame"/> objects and vice versa. For every incoming HTTP/2
    /// frame, a <see cref="IHttp2Frame"/> object is created and propagated via <see cref="IChannelHandler.ChannelRead"/>. Outbound <see cref="IHttp2Frame"/>
    /// objects received via <see cref="Write"/> are converted to the HTTP/2 wire format. HTTP/2 frames specific to a stream
    /// implement the <see cref="IHttp2StreamFrame"/> interface. The <see cref="Http2FrameCodec"/> is instantiated using the
    /// <see cref="Http2FrameCodecBuilder"/>. It's recommended for channel handlers to inherit from the
    /// <see cref="Http2ChannelDuplexHandler"/>, as it provides additional functionality like iterating over all active streams or
    /// creating outbound streams.</para>
    ///
    /// <para><c>Stream Lifecycle</c></para>
    ///
    /// The frame codec delivers and writes frames for active streams. An active stream is closed when either side sends a
    /// <c>RST_STREAM</c> frame or both sides send a frame with the <c>END_STREAM</c> flag set. Each
    /// <see cref="IHttp2StreamFrame"/> has a <see cref="IHttp2FrameStream"/> object attached that uniquely identifies a particular stream.
    ///
    /// <para><see cref="IHttp2StreamFrame"/>s read from the channel always a <see cref="IHttp2FrameStream"/> object set, while when writing a
    /// <see cref="IHttp2StreamFrame"/> the application code needs to set a <see cref="IHttp2FrameStream"/> object using
    /// <see cref="IHttp2StreamFrame.Stream"/>.</para>
    ///
    /// <para><c>Flow control</c></para>
    ///
    /// The frame codec automatically increments stream and connection flow control windows.
    ///
    /// <para>Incoming flow controlled frames need to be consumed by writing a <see cref="IHttp2WindowUpdateFrame"/> with the consumed
    /// number of bytes and the corresponding stream identifier set to the frame codec.</para>
    ///
    /// <para>The local stream-level flow control window can be changed by writing a <see cref="IHttp2SettingsFrame"/> with the
    /// <see cref="Http2Settings.InitialWindowSize()"/> set to the targeted value.</para>
    ///
    /// <para>The connection-level flow control window can be changed by writing a <see cref="IHttp2WindowUpdateFrame"/> with the
    /// desired window size <c>increment</c> in bytes and the stream identifier set to <c>0</c>. By default the initial
    /// connection-level flow control window is the same as initial stream-level flow control window.</para>
    ///
    /// <para><c>New inbound Streams</c></para>
    ///
    /// The first frame of a HTTP/2 stream must be a <see cref="IHttp2HeadersFrame"/>, which will have a <see cref="IHttp2FrameStream"/>
    /// object attached.
    ///
    /// <para><c>New outbound Streams</c></para>
    ///
    /// A outbound HTTP/2 stream can be created by first instantiating a new <see cref="IHttp2FrameStream"/> object via
    /// <see cref="Http2ChannelDuplexHandler.NewStream()"/>, and then writing a <see cref="IHttp2HeadersFrame"/> object with the stream
    /// attached.
    ///
    /// <code>
    ///     final Http2Stream2 stream = handler.newStream();
    ///     ctx.write(headersFrame.stream(stream)).addListener(new ChannelFutureListener() {
    ///
    ///         @Override
    ///         public void operationComplete(ChannelFuture f) {
    ///             if (f.isSuccess()) {
    ///                 // Stream is active and stream.id() returns a valid stream identifier.
    ///                 System.out.println("New stream with id " + stream.id() + " created.");
    ///             } else {
    ///                 // Stream failed to become active. Handle error.
    ///                 if (f.cause() instanceof Http2NoMoreStreamIdsException) {
    ///
    ///                 } else if (f.cause() instanceof Http2GoAwayException) {
    ///
    ///                 } else {
    ///
    ///                 }
    ///             }
    ///         }
    ///     }
    /// </code>
    ///
    /// <para>If a new stream cannot be created due to stream id exhaustion of the endpoint, the <see cref="IPromise"/> of the
    /// HEADERS frame will fail with a <see cref="Http2NoMoreStreamIdsException"/>.</para>
    ///
    /// <para>The HTTP/2 standard allows for an endpoint to limit the maximum number of concurrently active streams via the
    /// <c>SETTINGS_MAX_CONCURRENT_STREAMS</c> setting. When this limit is reached, no new streams can be created. However,
    /// the <see cref="Http2FrameCodec"/> can be build with
    /// <see cref="AbstractHttp2ConnectionHandlerBuilder{THandler, TBuilder}.EncoderEnforceMaxConcurrentStreams"/> enabled, in which case a new stream and
    /// its associated frames will be buffered until either the limit is increased or an active stream is closed. It's,
    /// however, possible that a buffered stream will never become active. That is, the channel might
    /// get closed or a GO_AWAY frame might be received. In the first case, all writes of buffered streams will fail with a
    /// <see cref="Http2ChannelClosedException"/>. In the second case, all writes of buffered streams with an identifier less than
    /// the last stream identifier of the GO_AWAY frame will fail with a <see cref="Http2GoAwayException"/>.</para>
    ///
    /// <para><c>Error Handling</c></para>
    ///
    /// Exceptions and errors are propagated via <see cref="IChannelHandler.ExceptionCaught"/>. Exceptions that apply to
    /// a specific HTTP/2 stream are wrapped in a <see cref="Http2FrameStreamException"/> and have the corresponding
    /// <see cref="IHttp2FrameStream"/> object attached.
    ///
    /// <para><c>Reference Counting</c></para>
    ///
    /// Some <see cref="IHttp2StreamFrame"/>s implement the <see cref="IReferenceCounted"/> interface, as they carry
    /// reference counted objects (e.g. <see cref="IByteBuffer"/>s). The frame codec will call <see cref="IReferenceCounted.Retain()"/> before
    /// propagating a reference counted object through the pipeline, and thus an application handler needs to release such
    /// an object after having consumed it. For more information on reference counting take a look at
    /// http://netty.io/wiki/reference-counted-objects.html
    ///
    /// <para><c>HTTP Upgrade</c></para>
    ///
    /// Server-side HTTP to HTTP/2 upgrade is supported in conjunction with <see cref="Http2ServerUpgradeCodec"/>; the necessary
    /// HTTP-to-HTTP/2 conversion is performed automatically.
    /// </summary>
    public class Http2FrameCodec : Http2ConnectionHandler
    {
        private static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<Http2FrameCodec>();

        protected readonly IHttp2ConnectionPropertyKey streamKey;
        private readonly IHttp2ConnectionPropertyKey upgradeKey;

        private readonly int? initialFlowControlWindowSize;

        private IChannelHandlerContext ctx;

        /// <summary>
        /// Number of buffered streams if the <see cref="StreamBufferingEncoder"/> is used.
        /// </summary>
        private int numBufferedStreams;
        private DefaultHttp2FrameStream frameStreamToInitialize;

        public Http2FrameCodec(IHttp2ConnectionEncoder encoder, IHttp2ConnectionDecoder decoder, Http2Settings initialSettings)
            : base(decoder, encoder, initialSettings)
        {

            decoder.FrameListener = new FrameListener(this);
            var connection = this.Connection;
            connection.AddListener(new ConnectionListener(this));
            connection.Remote.FlowController.Listener(new Http2RemoteFlowControllerListener(this));
            this.streamKey = connection.NewKey();
            this.upgradeKey = connection.NewKey();
            this.initialFlowControlWindowSize = initialSettings.InitialWindowSize();
        }

        /// <summary>
        /// Creates a new outbound/local stream.
        /// </summary>
        /// <returns></returns>
        internal virtual DefaultHttp2FrameStream NewStream()
        {
            return new DefaultHttp2FrameStream();
        }

        /// <summary>
        /// Iterates over all active HTTP/2 streams.
        /// <para>This method must not be called outside of the event loop.</para>
        /// </summary>
        /// <param name="streamVisitor"></param>
        public void ForEachActiveStream(IHttp2FrameStreamVisitor streamVisitor)
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);
            bool LocalVisit(IHttp2Stream stream)
            {
                try
                {
                    return streamVisitor.Visit(stream.GetProperty<IHttp2FrameStream>(this.streamKey));
                }
                catch (Exception cause)
                {
                    this.OnError(ctx, false, cause);
                    return false;
                }
            }
            this.Connection.ForEachActiveStream(LocalVisit);
        }
        public void ForEachActiveStream(Func<IHttp2FrameStream, bool> streamVisitor)
        {
            Debug.Assert(this.ctx.Executor.InEventLoop);
            bool LocalVisit(IHttp2Stream stream)
            {
                try
                {
                    return streamVisitor(stream.GetProperty<IHttp2FrameStream>(this.streamKey));
                }
                catch (Exception cause)
                {
                    this.OnError(ctx, false, cause);
                    return false;
                }
            }
            this.Connection.ForEachActiveStream(LocalVisit);
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            this.ctx = ctx;
            base.HandlerAdded(ctx);
            this.HandlerAdded0(ctx);
            // Must be after Http2ConnectionHandler does its initialization in handlerAdded above.
            // The server will not send a connection preface so we are good to send a window update.
            var connection = this.Connection;
            if (connection.IsServer)
            {
                TryExpandConnectionFlowControlWindow(connection);
            }
        }

        private void TryExpandConnectionFlowControlWindow(IHttp2Connection connection)
        {
            if (this.initialFlowControlWindowSize.HasValue)
            {
                // The window size in the settings explicitly excludes the connection window. So we manually manipulate the
                // connection window to accommodate more concurrent data per connection.
                var connectionStream = connection.ConnectionStream;
                var localFlowController = connection.Local.FlowController;
                int delta = initialFlowControlWindowSize.Value - localFlowController.GetInitialWindowSize(connectionStream);
                // Only increase the connection window, don't decrease it.
                if (delta > 0)
                {
                    // Double the delta just so a single stream can't exhaust the connection window.
                    localFlowController.IncrementWindowSize(connectionStream, Math.Max(delta << 1, delta));
                    this.Flush(ctx);
                }
            }
        }

        protected virtual void HandlerAdded0(IChannelHandlerContext ctx)
        {
            // sub-class can override this for extra steps that needs to be done when the handler is added.
        }

        /// <summary>
        /// Handles the cleartext HTTP upgrade event. If an upgrade occurred, sends a simple response via
        /// HTTP/2 on stream 1 (the stream specifically reserved for cleartext HTTP upgrade).
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="evt"></param>
        public sealed override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
        {
            if (evt == Http2ConnectionPrefaceAndSettingsFrameWrittenEvent.Instance)
            {
                // The user event implies that we are on the client.
                TryExpandConnectionFlowControlWindow(this.Connection);
            }
            else if (evt is HttpServerUpgradeHandler.UpgradeEvent upgrade)
            {
                try
                {
                    OnUpgradeEvent(ctx, (HttpServerUpgradeHandler.UpgradeEvent)upgrade.Retain());
                    var stream = this.Connection.Stream(Http2CodecUtil.HttpUpgradeStreamId);
                    if (stream.GetProperty<IHttp2FrameStream>(this.streamKey) == null)
                    {
                        // TODO: improve handler/stream lifecycle so that stream isn't active before handler added.
                        // The stream was already made active, but ctx may have been null so it wasn't initialized.
                        // https://github.com/netty/netty/issues/4942
                        this.OnStreamActive0(stream);
                    }
                    upgrade.UpgradeRequest.Headers.SetInt(
                            HttpConversionUtil.ExtensionHeaderNames.StreamId, Http2CodecUtil.HttpUpgradeStreamId);
                    stream.SetProperty(this.upgradeKey, true);
                    InboundHttpToHttp2Adapter.Handle(
                            ctx, this.Connection, this.Decoder.FrameListener, (IFullHttpMessage)upgrade.UpgradeRequest.Retain());
                }
                finally
                {
                    upgrade.Release();
                }
                return;
            }
            base.UserEventTriggered(ctx, evt);
        }

        /// <summary>
        /// Processes all <see cref="IHttp2Frame"/>s. <see cref="IHttp2StreamFrame"/>s may only originate in child streams.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="msg"></param>
        /// <param name="promise"></param>
        public override void Write(IChannelHandlerContext ctx, object msg, IPromise promise)
        {
            switch (msg)
            {
                case IHttp2DataFrame dataFrame:
                    this.Encoder.WriteDataAsync(ctx, dataFrame.Stream.Id, dataFrame.Content,
                        dataFrame.Padding, dataFrame.IsEndStream, promise);
                    break;

                case IHttp2HeadersFrame headersFrame:
                    WriteHeadersFrame(ctx, headersFrame, promise);
                    break;

                case IHttp2WindowUpdateFrame windowUpdateFrame:
                    var frameStream = windowUpdateFrame.Stream;
                    // It is legit to send a WINDOW_UPDATE frame for the connection stream. The parent channel doesn't attempt
                    // to set the Http2FrameStream so we assume if it is null the WINDOW_UPDATE is for the connection stream.
                    try
                    {
                        if (frameStream == null)
                        {
                            this.IncreaseInitialConnectionWindow(windowUpdateFrame.WindowSizeIncrement);
                        }
                        else
                        {
                            this.ConsumeBytes(frameStream.Id, windowUpdateFrame.WindowSizeIncrement);
                        }
                        promise.Complete();
                    }
                    catch (Exception t)
                    {
                        promise.SetException(t);
                    }
                    break;

                case IHttp2ResetFrame rstFrame:
                    this.Encoder.WriteRstStreamAsync(ctx, rstFrame.Stream.Id, rstFrame.ErrorCode, promise);
                    break;

                case IHttp2PingFrame pingFrame:
                    this.Encoder.WritePingAsync(ctx, pingFrame.Ack, pingFrame.Content, promise);
                    break;

                case IHttp2SettingsFrame settingsFrame:
                    this.Encoder.WriteSettingsAsync(ctx, settingsFrame.Settings, promise);
                    break;

                case IHttp2GoAwayFrame goAwayFrame:
                    this.WriteGoAwayFrame(ctx, goAwayFrame, promise);
                    break;

                case IHttp2UnknownFrame unknownFrame:
                    this.Encoder.WriteFrameAsync(ctx, unknownFrame.FrameType, unknownFrame.Stream.Id,
                            unknownFrame.Flags, unknownFrame.Content, promise);
                    break;

                default:
                    if (msg is IHttp2Frame frame)
                    {
                        ReferenceCountUtil.Release(msg);
                        ThrowHelper.ThrowUnsupportedMessageTypeException();
                    }
                    ctx.WriteAsync(msg, promise);
                    break;
            }
        }

        private void IncreaseInitialConnectionWindow(int deltaBytes)
        {
            // The LocalFlowController is responsible for detecting over/under flow.
            var connection = this.Connection;
            connection.Local.FlowController.IncrementWindowSize(connection.ConnectionStream, deltaBytes);
        }

        internal bool ConsumeBytes(int streamId, int bytes)
        {
            var connection = this.Connection;
            var stream = connection.Stream(streamId);
            // Upgraded requests are ineligible for stream control. We add the null check
            // in case the stream has been deregistered.
            if (stream != null && streamId == Http2CodecUtil.HttpUpgradeStreamId)
            {
                var upgraded = stream.GetProperty<bool>(upgradeKey);
                if (upgraded) { return false; }
            }

            return connection.Local.FlowController.ConsumeBytes(stream, bytes);
        }

        private void WriteGoAwayFrame(IChannelHandlerContext ctx, IHttp2GoAwayFrame frame, IPromise promise)
        {
            if (frame.LastStreamId > -1)
            {
                frame.Release();
                ThrowHelper.ThrowArgumentException_LastStreamIdMustNotBeSetOnGoAwayFrame();
            }

            int lastStreamCreated = this.Connection.Remote.LastStreamCreated;
            long lastStreamId = lastStreamCreated + ((long)frame.ExtraStreamIds) * 2;
            // Check if the computation overflowed.
            if (lastStreamId > int.MaxValue)
            {
                lastStreamId = int.MaxValue;
            }
            this.GoAwayAsync(ctx, (int)lastStreamId, frame.ErrorCode, frame.Content, promise);
        }

        private void WriteHeadersFrame(IChannelHandlerContext ctx, IHttp2HeadersFrame headersFrame, IPromise promise)
        {
            if (Http2CodecUtil.IsStreamIdValid(headersFrame.Stream.Id))
            {
                this.Encoder.WriteHeadersAsync(ctx, headersFrame.Stream.Id, headersFrame.Headers, headersFrame.Padding,
                        headersFrame.IsEndStream, promise);
            }
            else
            {
                var stream = (DefaultHttp2FrameStream)headersFrame.Stream;
                var connection = this.Connection;
                var streamId = connection.Local.IncrementAndGetNextStreamId;
                if (streamId < 0)
                {
                    promise.SetException(new Http2NoMoreStreamIdsException());
                    return;
                }
                stream.Id = streamId;

                // TODO: This depends on the fact that the connection based API will create Http2Stream objects
                // synchronously. We should investigate how to refactor this later on when we consolidate some layers.
                Debug.Assert(frameStreamToInitialize == null);
                frameStreamToInitialize = stream;

                // TODO(buchgr): Once Http2Stream2 and Http2Stream are merged this is no longer necessary.
                var writePromise = ctx.NewPromise();

                this.Encoder.WriteHeadersAsync(ctx, streamId, headersFrame.Headers, headersFrame.Padding,
                        headersFrame.IsEndStream, writePromise);
                var writeCompltion = writePromise.Task;
                if (writeCompltion.IsCompleted)
                {
                    NotifyHeaderWritePromise(writeCompltion, promise);
                }
                else
                {
                    Interlocked.Increment(ref numBufferedStreams);
#if NET40
                    writeCompltion.ContinueWith(t => NotifyHeaderWritePromise0(t, Tuple.Create(this, promise)),
                        TaskContinuationOptions.ExecuteSynchronously);
#else
                    writeCompltion.ContinueWith(NotifyHeaderWritePromiseAction,
                        Tuple.Create(this, promise), TaskContinuationOptions.ExecuteSynchronously);
#endif
                }
            }
        }

        private static readonly Action<Task, object> NotifyHeaderWritePromiseAction = NotifyHeaderWritePromise0;
        private static void NotifyHeaderWritePromise0(Task t, object s)
        {
            var wrapped = (Tuple<Http2FrameCodec, IPromise>)s;
            Interlocked.Decrement(ref wrapped.Item1.numBufferedStreams);
            NotifyHeaderWritePromise(t, wrapped.Item2);
        }

        private static void NotifyHeaderWritePromise(Task future, IPromise promise)
        {
            if (future.IsCanceled)
            {
                promise.TrySetCanceled();
            }
            else if (future.IsFaulted)
            {
                promise.TrySetException(future.Exception);
            }
            else //if (future.IsCompleted)
            {
                promise.TryComplete();
            }
        }

        private void OnStreamActive0(IHttp2Stream stream)
        {
            if (this.Connection.Local.IsValidStreamId(stream.Id)) { return; }

            DefaultHttp2FrameStream stream2 = this.NewStream().SetStreamAndProperty(streamKey, stream);
            this.OnHttp2StreamStateChanged(ctx, stream2);
        }

        sealed class ConnectionListener : Http2ConnectionAdapter
        {
            readonly Http2FrameCodec frameCodec;

            public ConnectionListener(Http2FrameCodec frameCodec) => this.frameCodec = frameCodec;

            public override void OnStreamAdded(IHttp2Stream stream)
            {
                var frameStreamToInitialize = this.frameCodec.frameStreamToInitialize;
                if (frameStreamToInitialize != null && stream.Id == frameStreamToInitialize.Id)
                {
                    frameStreamToInitialize.SetStreamAndProperty(this.frameCodec.streamKey, stream);
                    this.frameCodec.frameStreamToInitialize = null;
                }
            }

            public override void OnStreamActive(IHttp2Stream stream)
            {
                this.frameCodec.OnStreamActive0(stream);
            }

            public override void OnStreamClosed(IHttp2Stream stream)
            {
                var stream2 = stream.GetProperty<DefaultHttp2FrameStream>(this.frameCodec.streamKey);
                if (stream2 != null)
                {
                    this.frameCodec.OnHttp2StreamStateChanged(this.frameCodec.ctx, stream2);
                }
            }

            public override void OnStreamHalfClosed(IHttp2Stream stream)
            {
                var stream2 = stream.GetProperty<DefaultHttp2FrameStream>(this.frameCodec.streamKey);
                if (stream2 != null)
                {
                    this.frameCodec.OnHttp2StreamStateChanged(this.frameCodec.ctx, stream2);
                }
            }
        }

        protected override void OnConnectionError(IChannelHandlerContext ctx, bool outbound, Exception cause, Http2Exception http2Ex)
        {
            if (!outbound)
            {
                // allow the user to handle it first in the pipeline, and then automatically clean up.
                // If this is not desired behavior the user can override this method.
                //
                // We only forward non outbound errors as outbound errors will already be reflected by failing the promise.
                ctx.FireExceptionCaught(cause);
            }
            base.OnConnectionError(ctx, outbound, cause, http2Ex);
        }

        /// <summary>
        /// Exceptions for unknown streams, that is streams that have no <see cref="IHttp2FrameStream"/> object attached
        /// are simply logged and replied to by sending a RST_STREAM frame.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="outbound"></param>
        /// <param name="cause"></param>
        /// <param name="streamException"></param>
        protected override void OnStreamError(IChannelHandlerContext ctx, bool outbound, Exception cause, StreamException streamException)
        {
            int streamId = streamException.StreamId;
            var connectionStream = this.Connection.Stream(streamId);
            if (connectionStream == null)
            {
                this.OnHttp2UnknownStreamError(ctx, cause, streamException);
                // Write a RST_STREAM
                base.OnStreamError(ctx, outbound, cause, streamException);
                return;
            }

            var stream = connectionStream.GetProperty<IHttp2FrameStream>(streamKey);
            if (stream == null)
            {
                if (Logger.WarnEnabled) { Logger.StreamExceptionThrownWithoutStreamObjectAttached(cause); }
                // Write a RST_STREAM
                base.OnStreamError(ctx, outbound, cause, streamException);
                return;
            }

            if (!outbound)
            {
                // We only forward non outbound errors as outbound errors will already be reflected by failing the promise.
                this.OnHttp2FrameStreamException(ctx, new Http2FrameStreamException(stream, streamException.Error, cause));
            }
        }

        protected virtual void OnHttp2UnknownStreamError(IChannelHandlerContext ctx, Exception cause, StreamException streamException)
        {
            // Just log....
            if (Logger.WarnEnabled) { Logger.StreamExceptionThrownForUnkownStream(streamException.StreamId, cause); }
        }

        protected override bool IsGracefulShutdownComplete =>
            base.IsGracefulShutdownComplete && Volatile.Read(ref this.numBufferedStreams) == 0;

        sealed class FrameListener : IHttp2FrameListener
        {
            readonly Http2FrameCodec frameCodec;

            public FrameListener(Http2FrameCodec frameCodec) => this.frameCodec = frameCodec;

            public int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
            {
                var frame = new DefaultHttp2DataFrame(data, endOfStream, padding)
                {
                    Stream = this.RequireStream(streamId)
                };
                frame.Retain();
                this.frameCodec.OnHttp2Frame(ctx, frame);
                // We return the bytes in consumeBytes() once the stream channel consumed the bytes.
                return 0;
            }

            public void OnGoAwayRead(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
            {
                var frame = new DefaultHttp2GoAwayFrame(lastStreamId, errorCode, debugData);
                frame.Retain();
                this.frameCodec.OnHttp2Frame(ctx, frame);
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
            {
                var frame = new DefaultHttp2HeadersFrame(headers, endOfStream, padding)
                {
                    Stream = this.RequireStream(streamId)
                };
                this.frameCodec.OnHttp2Frame(ctx, frame);
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
            {
                this.OnHeadersRead(ctx, streamId, headers, padding, endOfStream);
            }

            public void OnPingAckRead(IChannelHandlerContext ctx, long data)
            {
                this.frameCodec.OnHttp2Frame(ctx, new DefaultHttp2PingFrame(data, true));
            }

            public void OnPingRead(IChannelHandlerContext ctx, long data)
            {
                this.frameCodec.OnHttp2Frame(ctx, new DefaultHttp2PingFrame(data, false));
            }

            public void OnPriorityRead(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive)
            {
                // TODO: Maybe handle me
            }

            public void OnPushPromiseRead(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding)
            {
                // TODO: Maybe handle me
            }

            public void OnRstStreamRead(IChannelHandlerContext ctx, int streamId, Http2Error errorCode)
            {
                var frame = new DefaultHttp2ResetFrame(errorCode)
                {
                    Stream = this.RequireStream(streamId)
                };
                this.frameCodec.OnHttp2Frame(ctx, frame);
            }

            public void OnSettingsAckRead(IChannelHandlerContext ctx)
            {
                // TODO: Maybe handle me
            }

            public void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings)
            {
                this.frameCodec.OnHttp2Frame(ctx, new DefaultHttp2SettingsFrame(settings));
            }

            public void OnUnknownFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload)
            {
                var frame = new DefaultHttp2UnknownFrame(frameType, flags, payload)
                {
                    Stream = this.RequireStream(streamId)
                };
                frame.Retain();
                this.frameCodec.OnHttp2Frame(ctx, frame);
            }

            public void OnWindowUpdateRead(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement)
            {
                if (streamId == 0)
                {
                    // Ignore connection window updates.
                    return;
                }
                var frame = new DefaultHttp2WindowUpdateFrame(windowSizeIncrement)
                {
                    Stream = this.RequireStream(streamId)
                };
                this.frameCodec.OnHttp2Frame(ctx, frame);
            }

            private IHttp2FrameStream RequireStream(int streamId)
            {
                var stream = this.frameCodec.Connection.Stream(streamId).GetProperty<IHttp2FrameStream>(this.frameCodec.streamKey);
                if (stream == null)
                {
                    ThrowHelper.ThrowInvalidOperationException_StreamObjectRequiredForIdentifier(streamId);
                }
                return stream;
            }
        }

        protected virtual void OnUpgradeEvent(IChannelHandlerContext ctx, HttpServerUpgradeHandler.UpgradeEvent evt)
        {
            ctx.FireUserEventTriggered(evt);
        }

        protected virtual void OnHttp2StreamWritabilityChanged(IChannelHandlerContext ctx, IHttp2FrameStream stream, bool writable)
        {
            ctx.FireUserEventTriggered(Http2FrameStreamEvent.WritabilityChanged(stream));
        }

        protected virtual void OnHttp2StreamStateChanged(IChannelHandlerContext ctx, IHttp2FrameStream stream)
        {
            ctx.FireUserEventTriggered(Http2FrameStreamEvent.StateChanged(stream));
        }

        protected virtual void OnHttp2Frame(IChannelHandlerContext ctx, IHttp2Frame frame)
        {
            ctx.FireChannelRead(frame);
        }

        protected virtual void OnHttp2FrameStreamException(IChannelHandlerContext ctx, Http2FrameStreamException cause)
        {
            ctx.FireExceptionCaught(cause);
        }

        internal bool IsWritable(DefaultHttp2FrameStream stream)
        {
            var s = stream.InternalStream;
            return s != null && this.Connection.Remote.FlowController.IsWritable(s);
        }

        sealed class Http2RemoteFlowControllerListener : IHttp2RemoteFlowControllerListener
        {
            readonly Http2FrameCodec frameCodec;

            public Http2RemoteFlowControllerListener(Http2FrameCodec frameCodec) => this.frameCodec = frameCodec;

            public void WritabilityChanged(IHttp2Stream stream)
            {
                var frameStream = stream.GetProperty<IHttp2FrameStream>(this.frameCodec.streamKey);
                if (frameStream == null) { return; }
                this.frameCodec.OnHttp2StreamWritabilityChanged(
                    this.frameCodec.ctx, frameStream, this.frameCodec.Connection.Remote.FlowController.IsWritable(stream));
            }
        }
    }
}
