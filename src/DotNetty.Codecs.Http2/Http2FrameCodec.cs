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
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
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
    /// <para>An HTTP/2 handler that maps HTTP/2 frames to <see cref="IHttp2Frame"/> objects and vice versa. For every incoming HTTP/2
    /// frame, an <see cref="IHttp2Frame"/> object is created and propagated via <see cref="IChannelHandler.ChannelRead"/>. Outbound <see cref="IHttp2Frame"/>
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
    /// The first frame of an HTTP/2 stream must be a <see cref="IHttp2HeadersFrame"/>, which will have a <see cref="IHttp2FrameStream"/>
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

        protected internal readonly IHttp2ConnectionPropertyKey _streamKey;
        private readonly IHttp2ConnectionPropertyKey _upgradeKey;

        private readonly int? _initialFlowControlWindowSize;

        internal IChannelHandlerContext _ctx;

        /// <summary>
        /// Number of buffered streams if the <see cref="StreamBufferingEncoder"/> is used.
        /// </summary>
        private int v_numBufferedStreams;
        private readonly ConcurrentDictionary<int, DefaultHttp2FrameStream> _frameStreamToInitializeMap;

        public Http2FrameCodec(IHttp2ConnectionEncoder encoder, IHttp2ConnectionDecoder decoder, Http2Settings initialSettings, bool decoupleCloseAndGoAway)
            : base(decoder, encoder, initialSettings, decoupleCloseAndGoAway)
        {

            _frameStreamToInitializeMap = new ConcurrentDictionary<int, DefaultHttp2FrameStream>();

            decoder.FrameListener = new FrameListener(this);
            var connection = Connection;
            connection.AddListener(new ConnectionListener(this));
            connection.Remote.FlowController.Listener(new Http2RemoteFlowControllerListener(this));
            _streamKey = connection.NewKey();
            _upgradeKey = connection.NewKey();
            _initialFlowControlWindowSize = initialSettings.InitialWindowSize();
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
            Debug.Assert(_ctx.Executor.InEventLoop);
            var connection = Connection;
            if (connection.NumActiveStreams > 0)
            {
                _ = connection.ForEachActiveStream(stream => InternalVisit(stream, streamVisitor));
            }
        }

        /// <summary>
        /// Retrieve the number of streams currently in the process of being initialized.
        /// </summary>
        /// <remarks>This is package-private for testing only.</remarks>
        internal int NumInitializingStreams => _frameStreamToInitializeMap.Count;

        private bool InternalVisit(IHttp2Stream stream, IHttp2FrameStreamVisitor streamVisitor)
        {
            try
            {
                return streamVisitor.Visit(stream.GetProperty<IHttp2FrameStream>(_streamKey));
            }
            catch (Exception cause)
            {
                OnError(_ctx, false, cause);
                return false;
            }
        }

        public void ForEachActiveStream(Func<IHttp2FrameStream, bool> streamVisitor)
        {
            Debug.Assert(_ctx.Executor.InEventLoop);
            var connection = Connection;
            if (connection.NumActiveStreams > 0)
            {
                _ = connection.ForEachActiveStream(stream => InternalVisit(stream, streamVisitor));
            }
        }
        private bool InternalVisit(IHttp2Stream stream, Func<IHttp2FrameStream, bool> streamVisitor)
        {
            try
            {
                return streamVisitor(stream.GetProperty<IHttp2FrameStream>(_streamKey));
            }
            catch (Exception cause)
            {
                OnError(_ctx, false, cause);
                return false;
            }
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            _ctx = ctx;
            base.HandlerAdded(ctx);
            HandlerAdded0(ctx);
            // Must be after Http2ConnectionHandler does its initialization in handlerAdded above.
            // The server will not send a connection preface so we are good to send a window update.
            var connection = Connection;
            if (connection.IsServer)
            {
                TryExpandConnectionFlowControlWindow(connection);
            }
        }

        private void TryExpandConnectionFlowControlWindow(IHttp2Connection connection)
        {
            if (_initialFlowControlWindowSize.HasValue)
            {
                // The window size in the settings explicitly excludes the connection window. So we manually manipulate the
                // connection window to accommodate more concurrent data per connection.
                var connectionStream = connection.ConnectionStream;
                var localFlowController = connection.Local.FlowController;
                int delta = _initialFlowControlWindowSize.Value - localFlowController.GetInitialWindowSize(connectionStream);
                // Only increase the connection window, don't decrease it.
                if (delta > 0)
                {
                    // Double the delta just so a single stream can't exhaust the connection window.
                    localFlowController.IncrementWindowSize(connectionStream, Math.Max(delta << 1, delta));
                    Flush(_ctx);
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
                TryExpandConnectionFlowControlWindow(Connection);

                // We schedule this on the EventExecutor to allow to have any extra handlers added to the pipeline
                // before we pass the event to the next handler. This is needed as the event may be called from within
                // handlerAdded(...) which will be run before other handlers will be added to the pipeline.
                ctx.Executor.Execute(() => ctx.FireUserEventTriggered(evt));
            }
            else if (evt is HttpServerUpgradeHandler.UpgradeEvent upgrade)
            {
                try
                {
                    OnUpgradeEvent(ctx, (HttpServerUpgradeHandler.UpgradeEvent)upgrade.Retain());
                    var stream = Connection.Stream(Http2CodecUtil.HttpUpgradeStreamId);
                    if (stream.GetProperty<IHttp2FrameStream>(_streamKey) is null)
                    {
                        // TODO: improve handler/stream lifecycle so that stream isn't active before handler added.
                        // The stream was already made active, but ctx may have been null so it wasn't initialized.
                        // https://github.com/netty/netty/issues/4942
                        OnStreamActive0(stream);
                    }
                    _ = upgrade.UpgradeRequest.Headers.SetInt(
                            HttpConversionUtil.ExtensionHeaderNames.StreamId, Http2CodecUtil.HttpUpgradeStreamId);
                    _ = stream.SetProperty(_upgradeKey, true);
                    InboundHttpToHttp2Adapter.Handle(
                            ctx, Connection, Decoder.FrameListener, (IFullHttpMessage)upgrade.UpgradeRequest.Retain());
                }
                finally
                {
                    _ = upgrade.Release();
                }
            }
            else
            {
                _ = ctx.FireUserEventTriggered(evt);
            }
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
                    _ = Encoder.WriteDataAsync(ctx, dataFrame.Stream.Id, dataFrame.Content,
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
                        if (frameStream is null)
                        {
                            IncreaseInitialConnectionWindow(windowUpdateFrame.WindowSizeIncrement);
                        }
                        else
                        {
                            _ = ConsumeBytes(frameStream.Id, windowUpdateFrame.WindowSizeIncrement);
                        }
                        promise.Complete();
                    }
                    catch (Exception t)
                    {
                        promise.SetException(t);
                    }
                    break;

                case IHttp2ResetFrame rstFrame:
                    int id = rstFrame.Stream.Id;
                    // Only ever send a reset frame if stream may have existed before as otherwise we may send a RST on a
                    // stream in an invalid state and cause a connection error.
                    if (Connection.StreamMayHaveExisted(id))
                    {
                        _ = Encoder.WriteRstStreamAsync(ctx, id, rstFrame.ErrorCode, promise);
                    }
                    else
                    {
                        _ = ReferenceCountUtil.Release(rstFrame);
                        promise.SetException(GetStreamNeverExistedException(id));
                    }
                    break;

                case IHttp2PingFrame pingFrame:
                    _ = Encoder.WritePingAsync(ctx, pingFrame.Ack, pingFrame.Content, promise);
                    break;

                case IHttp2SettingsAckFrame _:
                    // In the event of manual SETTINGS ACK is is assumed the encoder will apply the earliest received but not
                    // yet ACKed settings.
                    _ = Encoder.WriteSettingsAckAsync(ctx, promise);
                    break;

                case IHttp2SettingsFrame settingsFrame:
                    _ = Encoder.WriteSettingsAsync(ctx, settingsFrame.Settings, promise);
                    break;

                case IHttp2GoAwayFrame goAwayFrame:
                    WriteGoAwayFrame(ctx, goAwayFrame, promise);
                    break;

                case IHttp2UnknownFrame unknownFrame:
                    _ = Encoder.WriteFrameAsync(ctx, unknownFrame.FrameType, unknownFrame.Stream.Id,
                            unknownFrame.Flags, unknownFrame.Content, promise);
                    break;

                default:
                    if (msg is IHttp2Frame)
                    {
                        _ = ReferenceCountUtil.Release(msg);
                        ThrowHelper.ThrowUnsupportedMessageTypeException();
                    }
                    _ = ctx.WriteAsync(msg, promise);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Http2Exception GetStreamNeverExistedException(int streamId)
        {
            return Http2Exception.StreamError(streamId, Http2Error.ProtocolError, "Stream never existed");
        }

        private void IncreaseInitialConnectionWindow(int deltaBytes)
        {
            // The LocalFlowController is responsible for detecting over/under flow.
            var connection = Connection;
            connection.Local.FlowController.IncrementWindowSize(connection.ConnectionStream, deltaBytes);
        }

        internal bool ConsumeBytes(int streamId, int bytes)
        {
            var connection = Connection;
            var stream = connection.Stream(streamId);
            // Upgraded requests are ineligible for stream control. We add the null check
            // in case the stream has been deregistered.
            if (stream is object && streamId == Http2CodecUtil.HttpUpgradeStreamId)
            {
                var upgraded = stream.GetProperty<bool>(_upgradeKey);
                if (upgraded) { return false; }
            }

            return connection.Local.FlowController.ConsumeBytes(stream, bytes);
        }

        private void WriteGoAwayFrame(IChannelHandlerContext ctx, IHttp2GoAwayFrame frame, IPromise promise)
        {
            if (SharedConstants.TooBigOrNegative >= (uint)frame.LastStreamId) // > -1
            {
                _ = frame.Release();
                ThrowHelper.ThrowArgumentException_LastStreamIdMustNotBeSetOnGoAwayFrame();
            }

            int lastStreamCreated = Connection.Remote.LastStreamCreated;
            long lastStreamId = lastStreamCreated + ((long)frame.ExtraStreamIds) * 2;
            // Check if the computation overflowed.
            if (lastStreamId > int.MaxValue)
            {
                lastStreamId = int.MaxValue;
            }
            _ = GoAwayAsync(ctx, (int)lastStreamId, frame.ErrorCode, frame.Content, promise);
        }

        private void WriteHeadersFrame(IChannelHandlerContext ctx, IHttp2HeadersFrame headersFrame, IPromise promise)
        {
            if (Http2CodecUtil.IsStreamIdValid(headersFrame.Stream.Id))
            {
                _ = Encoder.WriteHeadersAsync(ctx, headersFrame.Stream.Id, headersFrame.Headers, headersFrame.Padding,
                        headersFrame.IsEndStream, promise);
            }
            else
            {
                var stream = (DefaultHttp2FrameStream)headersFrame.Stream;
                var connection = Connection;
                var streamId = connection.Local.IncrementAndGetNextStreamId;
                if (streamId < 0)
                {
                    promise.SetException(new Http2NoMoreStreamIdsException());

                    // Simulate a GOAWAY being received due to stream exhaustion on this connection. We use the maximum
                    // valid stream ID for the current peer.
                    OnHttp2Frame(ctx, new DefaultHttp2GoAwayFrame(connection.IsServer ? int.MaxValue :
                            int.MaxValue - 1, Http2Error.NoError,
                            ByteBufferUtil.WriteAscii(ctx.Allocator, "Stream IDs exhausted on local stream creation")));
                    return;
                }
                stream.Id = streamId;

                // Use a Map to store all pending streams as we may have multiple. This is needed as if we would store the
                // stream in a field directly we may override the stored field before onStreamAdded(...) was called
                // and so not correctly set the property for the buffered stream.
                //
                // See https://github.com/netty/netty/issues/8692
                var result = _frameStreamToInitializeMap.TryAdd(streamId, stream);

                // We should not re-use ids.
                Debug.Assert(result);

                _ = Encoder.WriteHeadersAsync(ctx, streamId, headersFrame.Headers, headersFrame.Padding,
                        headersFrame.IsEndStream, promise);
                if (!promise.IsCompleted)
                {
                    _ = Interlocked.Increment(ref v_numBufferedStreams);
                    // Clean up the stream being initialized if writing the headers fails and also
                    // decrement the number of buffered streams.
                    _ = promise.Task.ContinueWith(ResetNufferedStreamsAction, (this, streamId), TaskContinuationOptions.ExecuteSynchronously);
                }
                else
                {
                    HandleHeaderFuture(promise.Task, streamId);
                }
            }
        }

        private static readonly Action<Task, object> ResetNufferedStreamsAction = (t, s) => ResetNufferedStreams(t, s);
        private static void ResetNufferedStreams(Task t, object s)
        {
            var (self, streamId) = ((Http2FrameCodec, int))s;
            _ = Interlocked.Decrement(ref self.v_numBufferedStreams);
            self.HandleHeaderFuture(t, streamId);
        }

        private void HandleHeaderFuture(Task channelFuture, int streamId)
        {
            if (channelFuture.IsFailure())
            {
                _ = _frameStreamToInitializeMap.TryRemove(streamId, out _);
            }
        }

        private void OnStreamActive0(IHttp2Stream stream)
        {
            if (stream.Id != Http2CodecUtil.HttpUpgradeStreamId && Connection.Local.IsValidStreamId(stream.Id)) { return; }

            DefaultHttp2FrameStream stream2 = NewStream().SetStreamAndProperty(_streamKey, stream);
            OnHttp2StreamStateChanged(_ctx, stream2);
        }

        sealed class ConnectionListener : Http2ConnectionAdapter
        {
            private readonly Http2FrameCodec _frameCodec;

            public ConnectionListener(Http2FrameCodec frameCodec) => _frameCodec = frameCodec;

            public override void OnStreamAdded(IHttp2Stream stream)
            {
                _ = _frameCodec._frameStreamToInitializeMap.TryRemove(stream.Id, out var frameStream);

                if (frameStream is object)
                {
                    _ = frameStream.SetStreamAndProperty(_frameCodec._streamKey, stream);
                }
            }

            public override void OnStreamActive(IHttp2Stream stream)
            {
                _frameCodec.OnStreamActive0(stream);
            }

            public override void OnStreamClosed(IHttp2Stream stream)
            {
                OnHttp2StreamStateChanged0(stream);
            }

            public override void OnStreamHalfClosed(IHttp2Stream stream)
            {
                OnHttp2StreamStateChanged0(stream);
            }

            [MethodImpl(InlineMethod.AggressiveOptimization)]
            private void OnHttp2StreamStateChanged0(IHttp2Stream stream)
            {
                var stream2 = stream.GetProperty<DefaultHttp2FrameStream>(_frameCodec._streamKey);
                if (stream2 is object)
                {
                    _frameCodec.OnHttp2StreamStateChanged(_frameCodec._ctx, stream2);
                }
            }
        }

        protected internal override void OnConnectionError(IChannelHandlerContext ctx, bool outbound, Exception cause, Http2Exception http2Ex)
        {
            if (!outbound)
            {
                // allow the user to handle it first in the pipeline, and then automatically clean up.
                // If this is not desired behavior the user can override this method.
                //
                // We only forward non outbound errors as outbound errors will already be reflected by failing the promise.
                _ = ctx.FireExceptionCaught(cause);
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
            var connectionStream = Connection.Stream(streamId);
            if (connectionStream is null)
            {
                OnHttp2UnknownStreamError(ctx, cause, streamException);
                // Write a RST_STREAM
                base.OnStreamError(ctx, outbound, cause, streamException);
                return;
            }

            var stream = connectionStream.GetProperty<IHttp2FrameStream>(_streamKey);
            if (stream is null)
            {
                if (Logger.WarnEnabled) { Logger.StreamExceptionThrownWithoutStreamObjectAttached(cause); }
                // Write a RST_STREAM
                base.OnStreamError(ctx, outbound, cause, streamException);
                return;
            }

            if (!outbound)
            {
                // We only forward non outbound errors as outbound errors will already be reflected by failing the promise.
                OnHttp2FrameStreamException(ctx, new Http2FrameStreamException(stream, streamException.Error, cause));
            }
        }

        protected virtual void OnHttp2UnknownStreamError(IChannelHandlerContext ctx, Exception cause, StreamException streamException)
        {
            // It is normal to hit a race condition where we still receive frames for a stream that this
            // peer has deemed closed, such as if this peer sends a RST(CANCEL) to discard the request.
            // Since this is likely to be normal we log at DEBUG level.
            if (streamException.Error == Http2Error.StreamClosed)
            {
#if DEBUG
                if (Logger.DebugEnabled) { Logger.StreamExceptionThrownForUnkownStreamD(streamException.StreamId, cause); }
#endif
            }
            else
            {
                if (Logger.WarnEnabled) { Logger.StreamExceptionThrownForUnkownStream(streamException.StreamId, cause); }
            }
        }

        protected override bool IsGracefulShutdownComplete =>
            base.IsGracefulShutdownComplete && 0u >= (uint)Volatile.Read(ref v_numBufferedStreams);

        sealed class FrameListener : IHttp2FrameListener
        {
            private readonly Http2FrameCodec _frameCodec;

            public FrameListener(Http2FrameCodec frameCodec) => _frameCodec = frameCodec;

            public int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
            {
                var frame = new DefaultHttp2DataFrame(data, endOfStream, padding)
                {
                    Stream = RequireStream(streamId)
                };
                _ = frame.Retain();
                _frameCodec.OnHttp2Frame(ctx, frame);
                // We return the bytes in consumeBytes() once the stream channel consumed the bytes.
                return 0;
            }

            public void OnGoAwayRead(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
            {
                var frame = new DefaultHttp2GoAwayFrame(lastStreamId, errorCode, debugData);
                _ = frame.Retain();
                _frameCodec.OnHttp2Frame(ctx, frame);
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
            {
                var frame = new DefaultHttp2HeadersFrame(headers, endOfStream, padding)
                {
                    Stream = RequireStream(streamId)
                };
                _frameCodec.OnHttp2Frame(ctx, frame);
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
            {
                OnHeadersRead(ctx, streamId, headers, padding, endOfStream);
            }

            public void OnPingAckRead(IChannelHandlerContext ctx, long data)
            {
                _frameCodec.OnHttp2Frame(ctx, new DefaultHttp2PingFrame(data, true));
            }

            public void OnPingRead(IChannelHandlerContext ctx, long data)
            {
                _frameCodec.OnHttp2Frame(ctx, new DefaultHttp2PingFrame(data, false));
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
                    Stream = RequireStream(streamId)
                };
                _frameCodec.OnHttp2Frame(ctx, frame);
            }

            public void OnSettingsAckRead(IChannelHandlerContext ctx)
            {
                _frameCodec.OnHttp2Frame(ctx, DefaultHttp2SettingsAckFrame.Instance);
            }

            public void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings)
            {
                _frameCodec.OnHttp2Frame(ctx, new DefaultHttp2SettingsFrame(settings));
            }

            public void OnUnknownFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload)
            {
                var frame = new DefaultHttp2UnknownFrame(frameType, flags, payload)
                {
                    Stream = RequireStream(streamId)
                };
                _ = frame.Retain();
                _frameCodec.OnHttp2Frame(ctx, frame);
            }

            public void OnWindowUpdateRead(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement)
            {
                if (0u >= (uint)streamId)
                {
                    // Ignore connection window updates.
                    return;
                }
                var frame = new DefaultHttp2WindowUpdateFrame(windowSizeIncrement)
                {
                    Stream = RequireStream(streamId)
                };
                _frameCodec.OnHttp2Frame(ctx, frame);
            }

            private IHttp2FrameStream RequireStream(int streamId)
            {
                var stream = _frameCodec.Connection.Stream(streamId).GetProperty<IHttp2FrameStream>(_frameCodec._streamKey);
                if (stream is null)
                {
                    ThrowHelper.ThrowInvalidOperationException_StreamObjectRequiredForIdentifier(streamId);
                }
                return stream;
            }
        }

        private void OnUpgradeEvent(IChannelHandlerContext ctx, HttpServerUpgradeHandler.UpgradeEvent evt)
        {
            _ = ctx.FireUserEventTriggered(evt);
        }

        private void OnHttp2StreamWritabilityChanged(IChannelHandlerContext ctx, DefaultHttp2FrameStream stream, bool writable)
        {
            _ = ctx.FireUserEventTriggered(stream.WritabilityChanged);
        }

        protected virtual void OnHttp2StreamStateChanged(IChannelHandlerContext ctx, DefaultHttp2FrameStream stream)
        {
            _ = ctx.FireUserEventTriggered(stream.StateChanged);
        }

        protected virtual void OnHttp2Frame(IChannelHandlerContext ctx, IHttp2Frame frame)
        {
            _ = ctx.FireChannelRead(frame);
        }

        protected virtual void OnHttp2FrameStreamException(IChannelHandlerContext ctx, Http2FrameStreamException cause)
        {
            _ = ctx.FireExceptionCaught(cause);
        }

        sealed class Http2RemoteFlowControllerListener : IHttp2RemoteFlowControllerListener
        {
            private readonly Http2FrameCodec _frameCodec;

            public Http2RemoteFlowControllerListener(Http2FrameCodec frameCodec) => _frameCodec = frameCodec;

            public void WritabilityChanged(IHttp2Stream stream)
            {
                var frameStream = stream.GetProperty<DefaultHttp2FrameStream>(_frameCodec._streamKey);
                if (frameStream is null) { return; }
                _frameCodec.OnHttp2StreamWritabilityChanged(
                    _frameCodec._ctx, frameStream, _frameCodec.Connection.Remote.FlowController.IsWritable(stream));
            }
        }
    }
}
