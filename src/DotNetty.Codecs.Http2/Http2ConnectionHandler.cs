// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Provides the default implementation for processing inbound frame events and delegates to a
    /// <see cref="IHttp2FrameListener"/>
    /// <para>
    /// This class will read HTTP/2 frames and delegate the events to a <see cref="IHttp2FrameListener"/>
    /// </para>
    /// This interface enforces inbound flow control functionality through
    /// <see cref="IHttp2LocalFlowController"/>
    /// </summary>
    public class Http2ConnectionHandler : ByteToMessageDecoder, IHttp2LifecycleManager
    {
        private static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<Http2ConnectionHandler>();

        private static readonly IHttp2Headers HEADERS_TOO_LARGE_HEADERS = ReadOnlyHttp2Headers.ServerHeaders(false,
            HttpResponseStatus.RequestHeaderFieldsTooLarge.CodeAsText);
        private static readonly IByteBuffer HTTP_1_X_BUF = Unpooled.UnreleasableBuffer(
            Unpooled.WrappedBuffer(new byte[] { (byte)'H', (byte)'T', (byte)'T', (byte)'P', (byte)'/', (byte)'1', (byte)'.' }).AsReadOnly());

        private readonly IHttp2ConnectionDecoder decoder;
        private readonly IHttp2ConnectionEncoder encoder;
        private readonly Http2Settings initialSettings;
        private ClosingChannelFutureListener closeListener;
        private BaseDecoder byteDecoder;
        private TimeSpan gracefulShutdownTimeout;

        public Http2ConnectionHandler(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings)
        {
            if (initialSettings is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.initialSettings); }
            if (decoder is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.decoder); }
            if (encoder is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.encoder); }
            this.initialSettings = initialSettings;
            this.decoder = decoder;
            this.encoder = encoder;
            if (encoder.Connection != decoder.Connection)
            {
                ThrowHelper.ThrowArgumentException_EncoderAndDecoderDonotShareTheSameConnObject();
            }
        }

        public Http2ConnectionHandler(bool server, IHttp2FrameWriter frameWriter, IHttp2FrameLogger frameLogger, Http2Settings initialSettings)
        {
            if (initialSettings is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.initialSettings); }
            this.initialSettings = initialSettings;

            var connection = new DefaultHttp2Connection(server);

            var maxHeaderListSize = initialSettings.MaxHeaderListSize();
            IHttp2FrameReader frameReader = new DefaultHttp2FrameReader(!maxHeaderListSize.HasValue ?
                    new DefaultHttp2HeadersDecoder(true) :
                    new DefaultHttp2HeadersDecoder(true, maxHeaderListSize.Value));

            if (frameLogger is object)
            {
                frameWriter = new Http2OutboundFrameLogger(frameWriter, frameLogger);
                frameReader = new Http2InboundFrameLogger(frameReader, frameLogger);
            }
            this.encoder = new DefaultHttp2ConnectionEncoder(connection, frameWriter);
            this.decoder = new DefaultHttp2ConnectionDecoder(connection, this.encoder, frameReader);
        }

        public TimeSpan GracefulShutdownTimeout
        {
            get => this.gracefulShutdownTimeout;
            set
            {
#if NET40
                if (value < TimeSpan.FromMilliseconds(-1))
#else
                if (value < Timeout.InfiniteTimeSpan)
#endif
                {
                    ThrowHelper.ThrowArgumentException_GracefulShutdownTimeout(value);
                }
                this.gracefulShutdownTimeout = value;
            }
        }

        public IHttp2Connection Connection => this.encoder.Connection;

        public IHttp2ConnectionDecoder Decoder => this.decoder;

        public IHttp2ConnectionEncoder Encoder => this.encoder;

        private bool PrefaceSent => this.byteDecoder is object && this.byteDecoder.PrefaceSent;

        /// <summary>
        /// Handles the client-side (cleartext) upgrade from HTTP to HTTP/2.
        /// Reserves local stream 1 for the HTTP/2 response.
        /// </summary>
        public virtual void OnHttpClientUpgrade()
        {
            if (this.Connection.IsServer)
            {
                ThrowHelper.ThrowConnectionError_HttpUpgradeRequested(true);
            }
            if (!PrefaceSent)
            {
                // If the preface was not sent yet it most likely means the handler was not added to the pipeline before
                // calling this method.
                ThrowHelper.ThrowConnectionError_HttpUpgradeMustOccurAfterPrefaceWasSent();
            }
            if (this.decoder.PrefaceReceived)
            {
                ThrowHelper.ThrowConnectionError_HttpUpgradeMustOccurBeforeHttp2PrefaceIsReceived();
            }

            // Create a local stream used for the HTTP cleartext upgrade.
            this.Connection.Local.CreateStream(Http2CodecUtil.HttpUpgradeStreamId, true);
        }

        /// <summary>
        /// Handles the server-side (cleartext) upgrade from HTTP to HTTP/2.
        /// </summary>
        /// <param name="settings">the settings for the remote endpoint.</param>
        public virtual void OnHttpServerUpgrade(Http2Settings settings)
        {
            if (!this.Connection.IsServer)
            {
                ThrowHelper.ThrowConnectionError_HttpUpgradeRequested(false);
            }
            if (!this.PrefaceSent)
            {
                // If the preface was not sent yet it most likely means the handler was not added to the pipeline before
                // calling this method.
                ThrowHelper.ThrowConnectionError_HttpUpgradeMustOccurAfterPrefaceWasSent();
            }
            if (this.decoder.PrefaceReceived)
            {
                ThrowHelper.ThrowConnectionError_HttpUpgradeMustOccurBeforeHttp2PrefaceIsReceived();
            }

            // Apply the settings but no ACK is necessary.
            this.encoder.RemoteSettings(settings);

            // Create a stream in the half-closed state.
            this.Connection.Remote.CreateStream(Http2CodecUtil.HttpUpgradeStreamId, true);
        }

        public override void Flush(IChannelHandlerContext ctx)
        {
            try
            {
                // Trigger pending writes in the remote flow controller.
                this.encoder.FlowController.WritePendingBytes();
                ctx.Flush();
            }
            catch (Http2Exception e)
            {
                this.OnError(ctx, true, e);
            }
            catch (Exception cause)
            {
                this.OnError(ctx, true, ThrowHelper.GetConnectionError_ErrorFlushing(cause));
            }
        }

        private abstract class BaseDecoder
        {
            protected readonly Http2ConnectionHandler connHandler;

            public BaseDecoder(Http2ConnectionHandler connHandler) => this.connHandler = connHandler;

            public abstract void Decode(IChannelHandlerContext ctx, IByteBuffer input, List<object> output);

            public virtual void HandlerRemoved(IChannelHandlerContext ctx) { }

            public virtual void ChannelActive(IChannelHandlerContext ctx) { }

            public virtual void ChannelInactive(IChannelHandlerContext ctx)
            {
                // Connection has terminated, close the encoder and decoder.
                this.connHandler.encoder.Close();
                this.connHandler.decoder.Close();

                // We need to remove all streams (not just the active ones).
                // See https://github.com/netty/netty/issues/4838.
                this.connHandler.Connection.CloseAsync(ctx.VoidPromise());
            }

            /// <summary>
            /// Determine if the HTTP/2 connection preface been sent.
            /// </summary>
            /// <returns></returns>
            public virtual bool PrefaceSent => true;
        }

        private sealed class PrefaceDecoder : BaseDecoder
        {
            private IByteBuffer clientPrefaceString;
            private bool prefaceSent;

            public PrefaceDecoder(Http2ConnectionHandler connHandler, IChannelHandlerContext ctx)
                : base(connHandler)
            {
                this.clientPrefaceString = ClientPrefaceString(connHandler.Connection);
                // This handler was just added to the context. In case it was handled after
                // the connection became active, send the connection preface now.
                this.SendPreface(ctx);
            }

            public override bool PrefaceSent => this.prefaceSent;

            public override void Decode(IChannelHandlerContext ctx, IByteBuffer input, List<object> output)
            {
                try
                {
                    if (ctx.Channel.Active && this.ReadClientPrefaceString(input) && this.VerifyFirstFrameIsSettings(input))
                    {
                        // After the preface is read, it is time to hand over control to the post initialized decoder.
                        var byteDecoder = new FrameDecoder(this.connHandler);
                        this.connHandler.byteDecoder = byteDecoder;
                        byteDecoder.Decode(ctx, input, output);
                    }
                }
                catch (Exception e)
                {
                    this.connHandler.OnError(ctx, false, e);
                }
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                // The channel just became active - send the connection preface to the remote endpoint.
                this.SendPreface(ctx);
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                this.Cleanup();
                base.ChannelInactive(ctx);
            }

            public override void HandlerRemoved(IChannelHandlerContext ctx)
            {
                this.Cleanup();
            }

            /// <summary>
            /// Releases the <see cref="clientPrefaceString"/>. Any active streams will be left in the open.
            /// </summary>
            private void Cleanup()
            {
                var clientPrefaceString = this.clientPrefaceString;
                if (clientPrefaceString is object)
                {
                    clientPrefaceString.Release();
                    this.clientPrefaceString = null;
                }
            }

            /// <summary>
            /// Decodes the client connection preface string from the input buffer.
            /// </summary>
            /// <param name="input"></param>
            /// <returns><c>true</c> if processing of the client preface string is complete. Since client preface strings can
            /// only be received by servers, returns true immediately for client endpoints.</returns>
            private bool ReadClientPrefaceString(IByteBuffer input)
            {
                if (this.clientPrefaceString is null)
                {
                    return true;
                }

                int prefaceRemaining = this.clientPrefaceString.ReadableBytes;
                int bytesRead = Math.Min(input.ReadableBytes, prefaceRemaining);

                // If the input so far doesn't match the preface, break the connection.
                if (0u >= (uint)bytesRead || !ByteBufferUtil.Equals(input, input.ReaderIndex,
                                                             this.clientPrefaceString, this.clientPrefaceString.ReaderIndex,
                                                             bytesRead))
                {
                    int maxSearch = 1024; // picked because 512 is too little, and 2048 too much
                    int http1Index =
                        ByteBufferUtil.IndexOf(HTTP_1_X_BUF, input.Slice(input.ReaderIndex, Math.Min(input.ReadableBytes, maxSearch)));
                    if (http1Index != -1)
                    {
                        ThrowHelper.ThrowConnectionError_UnexpectedHttp1Request(input, http1Index);
                    }
                    ThrowHelper.ThrowConnectionError_Http2ClientPrefaceStringMissingOrCorrupt(input, this.clientPrefaceString.ReadableBytes);
                }
                input.SkipBytes(bytesRead);
                this.clientPrefaceString.SkipBytes(bytesRead);

                if (!this.clientPrefaceString.IsReadable())
                {
                    // Entire preface has been read.
                    this.clientPrefaceString.Release();
                    this.clientPrefaceString = null;
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Peeks at that the next frame in the buffer and verifies that it is a non-ack <c>SETTINGS</c> frame.
            /// </summary>
            /// <param name="input">the inbound buffer.</param>
            /// <returns><c>true</c> if the next frame is a non-ack <c>SETTINGS</c> frame, <c>false</c> if more
            /// data is required before we can determine the next frame type.</returns>
            /// <exception cref="Http2Exception">thrown if the next frame is NOT a non-ack <c>SETTINGS</c> frame.</exception>
            private bool VerifyFirstFrameIsSettings(IByteBuffer input)
            {
                if (input.ReadableBytes < 5)
                {
                    // Need more data before we can see the frame type for the first frame.
                    return false;
                }

                var frameType = (Http2FrameTypes)input.GetByte(input.ReaderIndex + 3);
                int flags = input.GetByte(input.ReaderIndex + 4);
                if (frameType != Http2FrameTypes.Settings || (flags & Http2Flags.ACK) != 0)
                {
                    ThrowHelper.ThrowConnectionError_FirstReceivedFrameWasNotSettings(input);
                }
                return true;
            }

            /// <summary>
            /// Sends the HTTP/2 connection preface upon establishment of the connection, if not already sent.
            /// </summary>
            /// <param name="ctx"></param>
            private void SendPreface(IChannelHandlerContext ctx)
            {
                if (this.prefaceSent || !ctx.Channel.Active) { return; }

                this.prefaceSent = true;

                var isClient = !this.connHandler.Connection.IsServer;
                if (isClient)
                {
                    // Clients must send the preface string as the first bytes on the connection.
                    ctx.WriteAsync(Http2CodecUtil.ConnectionPrefaceBuf())
#if NET40
                       .ContinueWith(t => CloseOnFailure(t, ctx), TaskContinuationOptions.ExecuteSynchronously);
#else
                       .ContinueWith(CloseOnFailureAction, ctx, TaskContinuationOptions.ExecuteSynchronously);
#endif
                }

                // Both client and server must send their initial settings.
                this.connHandler.encoder
                        .WriteSettingsAsync(ctx, this.connHandler.initialSettings, ctx.NewPromise())
#if NET40
                        .ContinueWith(t => CloseOnFailure(t, ctx), TaskContinuationOptions.ExecuteSynchronously);
#else
                        .ContinueWith(CloseOnFailureAction, ctx, TaskContinuationOptions.ExecuteSynchronously);
#endif

                if (isClient)
                {
                    // If this handler is extended by the user and we directly fire the userEvent from this context then
                    // the user will not see the event. We should fire the event starting with this handler so this class
                    // (and extending classes) have a chance to process the event.
                    this.connHandler.UserEventTriggered(ctx, Http2ConnectionPrefaceAndSettingsFrameWrittenEvent.Instance);
                }
            }
        }

        private sealed class FrameDecoder : BaseDecoder
        {
            public FrameDecoder(Http2ConnectionHandler connHandler) : base(connHandler) { }

            public override void Decode(IChannelHandlerContext ctx, IByteBuffer input, List<object> output)
            {
                try
                {
                    this.connHandler.decoder.DecodeFrame(ctx, input, output);
                }
                catch (Exception e)
                {
                    this.connHandler.OnError(ctx, false, e);
                }
            }
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            // Initialize the encoder, decoder, flow controllers, and internal state.
            this.encoder.LifecycleManager(this);
            this.decoder.LifecycleManager(this);
            this.encoder.FlowController.SetChannelHandlerContext(ctx);
            this.decoder.FlowController.SetChannelHandlerContext(ctx);
            this.byteDecoder = new PrefaceDecoder(this, ctx);
        }

        protected override void HandlerRemovedInternal(IChannelHandlerContext ctx)
        {
            if (this.byteDecoder is object)
            {
                this.byteDecoder.HandlerRemoved(ctx);
                this.byteDecoder = null;
            }
        }

        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            if (this.byteDecoder is null)
            {
                this.byteDecoder = new PrefaceDecoder(this, ctx);
            }
            this.byteDecoder.ChannelActive(ctx);
            base.ChannelActive(ctx);
        }

        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            // Call super class first, as this may result in decode being called.
            base.ChannelInactive(ctx);
            if (this.byteDecoder is object)
            {
                this.byteDecoder.ChannelInactive(ctx);
                this.byteDecoder = null;
            }
        }

        public override void ChannelWritabilityChanged(IChannelHandlerContext ctx)
        {
            // Writability is expected to change while we are writing. We cannot allow this event to trigger reentering
            // the allocation and write loop. Reentering the event loop will lead to over or illegal allocation.
            try
            {
                if (ctx.Channel.IsWritable)
                {
                    this.Flush(ctx);
                }
                encoder.FlowController.ChannelWritabilityChanged();
            }
            finally
            {
                base.ChannelWritabilityChanged(ctx);
            }
        }

        protected override void Decode(IChannelHandlerContext ctx, IByteBuffer input, List<object> output)
        {
            this.byteDecoder.Decode(ctx, input, output);
        }

        public override Task BindAsync(IChannelHandlerContext context, EndPoint localAddress)
        {
            return context.BindAsync(localAddress);
        }

        public override Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress)
        {
            return context.ConnectAsync(remoteAddress, localAddress);
        }

        public override void Disconnect(IChannelHandlerContext context, IPromise promise)
        {
            context.DisconnectAsync(promise);
        }

        public override void Close(IChannelHandlerContext ctx, IPromise promise)
        {
            promise = promise.Unvoid();
            // Avoid NotYetConnectedException
            if (!ctx.Channel.Active)
            {
                ctx.CloseAsync(promise);
                return;
            }

            // If the user has already sent a GO_AWAY frame they may be attempting to do a graceful shutdown which requires
            // sending multiple GO_AWAY frames. We should only send a GO_AWAY here if one has not already been sent. If
            // a GO_AWAY has been sent we send a empty buffer just so we can wait to close until all other data has been
            // flushed to the OS.
            // https://github.com/netty/netty/issues/5307
            var future = this.Connection.GoAwaySent() ? ctx.WriteAsync(Unpooled.Empty) : this.GoAwayAsync(ctx, null);
            ctx.Flush();
            this.DoGracefulShutdown(ctx, future, promise);
        }

        private void DoGracefulShutdown(IChannelHandlerContext ctx, Task future, IPromise promise)
        {
            if (this.IsGracefulShutdownComplete)
            {
                // If there are no active streams, close immediately after the GO_AWAY write completes.
                if (future.IsCompleted)
                {
                    ctx.CloseAsync(promise);
                }
                else
                {
#if NET40
                    future.ContinueWith(t => CloseChannelOnComplete(t, Tuple.Create(ctx, promise)),
                        TaskContinuationOptions.ExecuteSynchronously);
#else
                    future.ContinueWith(CloseChannelOnCompleteAction,
                        Tuple.Create(ctx, promise),
                        TaskContinuationOptions.ExecuteSynchronously);
#endif
                }
            }
            else
            {
                // If there are active streams we should wait until they are all closed before closing the connection.
                if (this.gracefulShutdownTimeout < TimeSpan.Zero)
                {
                    this.closeListener = new ClosingChannelFutureListener(ctx, promise);
                }
                else
                {
                    this.closeListener = new ClosingChannelFutureListener(ctx, promise, this.gracefulShutdownTimeout);
                }
            }
        }

        public override void Deregister(IChannelHandlerContext context, IPromise promise)
        {
            context.DeregisterAsync(promise);
        }

        public override void Read(IChannelHandlerContext context)
        {
            context.Read();
        }

        public override void Write(IChannelHandlerContext context, object message, IPromise promise)
        {
            context.WriteAsync(message, promise);
        }

        public override void ChannelReadComplete(IChannelHandlerContext ctx)
        {
            // Trigger flush after read on the assumption that flush is cheap if there is nothing to write and that
            // for flow-control the read may release window that causes data to be written that can now be flushed.
            try
            {
                // First call channelReadComplete0(...) as this may produce more data that we want to flush
                this.ChannelReadComplete0(ctx);
            }
            finally
            {
                this.Flush(ctx);
            }
        }

        protected void ChannelReadComplete0(IChannelHandlerContext ctx)
        {
            // Discard bytes of the cumulation buffer if needed.
            this.DiscardSomeReadBytes();

            // Ensure we never stale the HTTP/2 Channel. Flow-control is enforced by HTTP/2.
            //
            // See https://tools.ietf.org/html/rfc7540#section-5.2.2
            if (!ctx.Channel.Configuration.AutoRead)
            {
                ctx.Read();
            }

            ctx.FireChannelReadComplete();
        }

        /// <summary>
        /// Handles <see cref="Http2Exception"/> objects that were thrown from other handlers. Ignores all other exceptions.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="cause"></param>
        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            if (Http2CodecUtil.GetEmbeddedHttp2Exception(cause) is object)
            {
                // Some exception in the causality chain is an Http2Exception - handle it.
                this.OnError(ctx, false, cause);
            }
            else
            {
                base.ExceptionCaught(ctx, cause);
            }
        }

        /// <summary>
        /// Closes the local side of the given stream. If this causes the stream to be closed, adds a
        /// hook to close the channel after the given future completes.
        /// </summary>
        /// <param name="stream">the stream to be half closed.</param>
        /// <param name="future">If closing, the future after which to close the channel.</param>
        public virtual void CloseStreamLocal(IHttp2Stream stream, Task future)
        {
            var streamState = stream.State;
            if (Http2StreamState.Open == streamState || Http2StreamState.HalfClosedLocal == streamState)
            {
                stream.CloseLocalSide();
            }
            else
            {
                this.CloseStream(stream, future);
            }
        }

        public virtual void CloseStreamRemote(IHttp2Stream stream, Task future)
        {
            var streamState = stream.State;
            if (Http2StreamState.Open == streamState || Http2StreamState.HalfClosedRemote == streamState)
            {
                stream.CloseRemoteSide();
            }
            else
            {
                this.CloseStream(stream, future);
            }
        }

        public virtual void CloseStream(IHttp2Stream stream, Task future)
        {
            stream.Close();

            if (future.IsCompleted)
            {
                this.CheckCloseConnection(future);
            }
            else
            {
#if NET40
                future.ContinueWith(t => CheckCloseConnOnComplete(t, this), TaskContinuationOptions.ExecuteSynchronously);
#else
                future.ContinueWith(CheckCloseConnOnCompleteAction, this, TaskContinuationOptions.ExecuteSynchronously);
#endif
            }
        }

        /// <summary>
        /// Central handler for all exceptions caught during HTTP/2 processing.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="outbound"></param>
        /// <param name="cause"></param>
        public virtual void OnError(IChannelHandlerContext ctx, bool outbound, Exception cause)
        {
            Http2Exception embedded = Http2CodecUtil.GetEmbeddedHttp2Exception(cause);
            if (Http2Exception.IsStreamError(embedded))
            {
                this.OnStreamError(ctx, outbound, cause, (StreamException)embedded);
            }
            else if (embedded is CompositeStreamException compositException)
            {
                foreach (StreamException streamException in compositException)
                {
                    this.OnStreamError(ctx, outbound, cause, streamException);
                }
            }
            else
            {
                this.OnConnectionError(ctx, outbound, cause, embedded);
            }
            ctx.Flush();
        }

        /// <summary>
        /// Called by the graceful shutdown logic to determine when it is safe to close the connection. Returns <c>true</c>
        /// if the graceful shutdown has completed and the connection can be safely closed. This implementation just
        /// guarantees that there are no active streams. Subclasses may override to provide additional checks.
        /// </summary>
        protected virtual bool IsGracefulShutdownComplete => 0u >= (uint)this.Connection.NumActiveStreams;

        /// <summary>
        /// Handler for a connection error. Sends a <c>GO_AWAY</c> frame to the remote endpoint. Once all
        /// streams are closed, the connection is shut down.
        /// </summary>
        /// <param name="ctx">the channel context</param>
        /// <param name="outbound"><c>true</c> if the error was caused by an outbound operation.</param>
        /// <param name="cause">the exception that was caught</param>
        /// <param name="http2Ex">the <see cref="Http2Exception"/> that is embedded in the causality chain. This may
        /// be <c>null</c> if it's an unknown exception.</param>
        protected virtual void OnConnectionError(IChannelHandlerContext ctx, bool outbound, Exception cause, Http2Exception http2Ex)
        {
            if (http2Ex is null)
            {
                http2Ex = new Http2Exception(Http2Error.InternalError, cause.Message, cause);
            }

            var promise = ctx.NewPromise();
            var future = this.GoAwayAsync(ctx, http2Ex);
            switch (http2Ex.ShutdownHint)
            {
                case ShutdownHint.GracefulShutdown:
                    this.DoGracefulShutdown(ctx, future, promise);
                    break;
                default:
                    if (future.IsCompleted)
                    {
                        ctx.CloseAsync(promise);
                    }
                    else
                    {
#if NET40
                        future.ContinueWith(t => CloseChannelOnComplete(t, Tuple.Create(ctx, promise)),
                            TaskContinuationOptions.ExecuteSynchronously);
#else
                        future.ContinueWith(CloseChannelOnCompleteAction,
                            Tuple.Create(ctx, promise),
                            TaskContinuationOptions.ExecuteSynchronously);
#endif
                    }
                    break;
            }
        }

        /// <summary>
        /// Handler for a stream error. Sends a <c>RST_STREAM</c> frame to the remote endpoint and closes the stream.
        /// </summary>
        /// <param name="ctx">the channel context</param>
        /// <param name="outbound"><c>true</c> if the error was caused by an outbound operation.</param>
        /// <param name="cause">the exception that was caught</param>
        /// <param name="http2Ex">the <see cref="StreamException"/> that is embedded in the causality chain.</param>
        protected virtual void OnStreamError(IChannelHandlerContext ctx, bool outbound, Exception cause, StreamException http2Ex)
        {
            var streamId = http2Ex.StreamId;
            var stream = this.Connection.Stream(streamId);

            //if this is caused by reading headers that are too large, send a header with status 431
            if (http2Ex is HeaderListSizeException headerListSizeException &&
                headerListSizeException.DuringDecode &&
                this.Connection.IsServer)
            {
                // NOTE We have to check to make sure that a stream exists before we send our reply.
                // We likely always create the stream below as the stream isn't created until the
                // header block is completely processed.

                // The case of a streamId referring to a stream which was already closed is handled
                // by createStream and will land us in the catch block below
                if (stream is null)
                {
                    try
                    {
                        stream = this.encoder.Connection.Remote.CreateStream(streamId, true);
                    }
                    catch (Http2Exception)
                    {
                        this.ResetUnknownStreamAsync(ctx, streamId, http2Ex.Error, ctx.NewPromise());
                        return;
                    }
                }

                // ensure that we have not already sent headers on this stream
                if (stream is object && !stream.IsHeadersSent)
                {
                    try
                    {
                        this.HandleServerHeaderDecodeSizeError(ctx, stream);
                    }
                    catch (Exception cause2)
                    {
                        this.OnError(ctx, outbound, ThrowHelper.GetConnectionError_ErrorDecodeSizeError(cause2));
                    }
                }
            }

            if (stream is null)
            {
                if (!outbound || this.Connection.Local.MayHaveCreatedStream(streamId))
                {
                    this.ResetUnknownStreamAsync(ctx, streamId, http2Ex.Error, ctx.NewPromise());
                }
            }
            else
            {
                this.ResetStreamAsync(ctx, stream, http2Ex.Error, ctx.NewPromise());
            }
        }

        /// <summary>
        /// Notifies client that this server has received headers that are larger than what it is
        /// willing to accept. Override to change behavior.
        /// </summary>
        /// <param name="ctx">the channel context</param>
        /// <param name="stream">the Http2Stream on which the header was received</param>
        protected virtual void HandleServerHeaderDecodeSizeError(IChannelHandlerContext ctx, IHttp2Stream stream)
        {
            this.encoder.WriteHeadersAsync(ctx, stream.Id, HEADERS_TOO_LARGE_HEADERS, 0, true, ctx.NewPromise());
        }

        protected IHttp2FrameWriter FrameWriter => this.encoder.FrameWriter;

        /// <summary>
        /// Sends a <c>RST_STREAM</c> frame even if we don't know about the stream. This error condition is most likely
        /// triggered by the first frame of a stream being invalid. That is, there was an error reading the frame before
        /// we could create a new stream.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="streamId"></param>
        /// <param name="errorCode"></param>
        /// <param name="promise"></param>
        /// <returns></returns>
        private Task ResetUnknownStreamAsync(IChannelHandlerContext ctx, int streamId, Http2Error errorCode, IPromise promise)
        {
            var future = this.FrameWriter.WriteRstStreamAsync(ctx, streamId, errorCode, promise);
            if (future.IsCompleted)
            {
                this.CloseConnectionOnError(ctx, future);
            }
            else
            {
#if NET40
                future.ContinueWith(t => CloseConnectionOnErrorOnComplete(t, Tuple.Create(this, ctx)), TaskContinuationOptions.ExecuteSynchronously);
#else
                future.ContinueWith(CloseConnectionOnErrorOnCompleteAction, Tuple.Create(this, ctx), TaskContinuationOptions.ExecuteSynchronously);
#endif
            }
            return future;
        }

        public virtual Task ResetStreamAsync(IChannelHandlerContext ctx, int streamId, Http2Error errorCode, IPromise promise)
        {
            var stream = this.Connection.Stream(streamId);
            if (stream is null)
            {
                return ResetUnknownStreamAsync(ctx, streamId, errorCode, promise.Unvoid());
            }

            return this.ResetStreamAsync(ctx, stream, errorCode, promise);
        }

        private Task ResetStreamAsync(IChannelHandlerContext ctx, IHttp2Stream stream, Http2Error errorCode, IPromise promise)
        {
            promise = promise.Unvoid();
            if (stream.IsResetSent)
            {
                // Don't write a RST_STREAM frame if we have already written one.
                promise.Complete();
                return promise.Task;
            }
            Task future;
            // If the remote peer is not aware of the steam, then we are not allowed to send a RST_STREAM
            // https://tools.ietf.org/html/rfc7540#section-6.4.
            if (stream.State == Http2StreamState.Idle ||
                this.Connection.Local.Created(stream) && !stream.IsHeadersSent && !stream.IsPushPromiseSent)
            {
                promise.Complete();
                future = promise.Task;
            }
            else
            {
                future = this.FrameWriter.WriteRstStreamAsync(ctx, stream.Id, errorCode, promise);
            }

            // Synchronously set the resetSent flag to prevent any subsequent calls
            // from resulting in multiple reset frames being sent.
            stream.ResetSent();

            if (future.IsCompleted)
            {
                this.ProcessRstStreamWriteResult(ctx, stream, future);
            }
            else
            {
#if NET40
                future.ContinueWith(t => ProcessRstStreamWriteResultOnComplete(t, Tuple.Create(this, ctx, stream)),
                    TaskContinuationOptions.ExecuteSynchronously);
#else
                future.ContinueWith(ProcessRstStreamWriteResultOnCompleteAction,
                    Tuple.Create(this, ctx, stream), TaskContinuationOptions.ExecuteSynchronously);
#endif
            }

            return future;
        }

        public virtual Task GoAwayAsync(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData, IPromise promise)
        {
            promise = promise.Unvoid();
            var connection = this.Connection;
            try
            {
                if (!connection.GoAwaySent(lastStreamId, errorCode, debugData))
                {
                    debugData.Release();
                    promise.TryComplete();
                    return promise.Task;
                }
            }
            catch (Exception cause)
            {
                debugData.Release();
                promise.TrySetException(cause);
                return promise.Task;
            }

            // Need to retain before we write the buffer because if we do it after the refCnt could already be 0 and
            // result in an IllegalRefCountException.
            debugData.Retain();
            var future = this.FrameWriter.WriteGoAwayAsync(ctx, lastStreamId, errorCode, debugData, promise);

            if (future.IsCompleted)
            {
                ProcessGoAwayWriteResult(ctx, lastStreamId, errorCode, debugData, future);
            }
            else
            {
#if NET40
                future.ContinueWith(t => ProcessGoAwayWriteResultOnComplete(t, Tuple.Create(ctx, lastStreamId, errorCode, debugData)),
                    TaskContinuationOptions.ExecuteSynchronously);
#else
                future.ContinueWith(ProcessGoAwayWriteResultOnCompleteAction,
                    Tuple.Create(ctx, lastStreamId, errorCode, debugData), TaskContinuationOptions.ExecuteSynchronously);
#endif
            }

            return future;
        }

        /// <summary>
        /// Closes the connection if the graceful shutdown process has completed.
        /// </summary>
        /// <param name="future">Represents the status that will be passed to the <see cref="ClosingChannelFutureListener"/>.</param>
        private void CheckCloseConnection(Task future)
        {
            // If this connection is closing and the graceful shutdown has completed, close the connection
            // once this operation completes.
            if (this.closeListener is object && this.IsGracefulShutdownComplete)
            {
                var closeListener = this.closeListener;
                // This method could be called multiple times
                // and we don't want to notify the closeListener multiple times.
                this.closeListener = null;
                try
                {
                    closeListener.OperationComplete(future);
                }
                catch (Exception e)
                {
                    ThrowHelper.ThrowInvalidOperationException_CloseListenerThrewAnUnexpectedException(e);
                }
            }
        }

        private Task GoAwayAsync(IChannelHandlerContext ctx, Http2Exception cause)
        {
            var errorCode = cause is object ? cause.Error : Http2Error.NoError;
            int lastKnownStream = this.Connection.Remote.LastStreamCreated;
            return GoAwayAsync(ctx, lastKnownStream, errorCode, Http2CodecUtil.ToByteBuf(ctx, cause), ctx.NewPromise());
        }

        private void ProcessRstStreamWriteResult(IChannelHandlerContext ctx, IHttp2Stream stream, Task future)
        {
            if (future.IsSuccess())
            {
                this.CloseStream(stream, future);
            }
            else
            {
                // The connection will be closed and so no need to change the resetSent flag to false.
                this.OnConnectionError(ctx, true, future.Exception.InnerException, null);
            }
        }

        private void CloseConnectionOnError(IChannelHandlerContext ctx, Task future)
        {
            if (!future.IsSuccess())
            {
                this.OnConnectionError(ctx, true, future.Exception.InnerException, null);
            }
        }

        private static readonly Action<Task, object> CloseChannelOnCompleteAction = CloseChannelOnComplete;
        private static void CloseChannelOnComplete(Task t, object s)
        {
            var wrapped = (Tuple<IChannelHandlerContext, IPromise>)s;
            wrapped.Item1.CloseAsync(wrapped.Item2);
        }

        private static readonly Action<Task, object> CloseConnectionOnErrorOnCompleteAction = CloseConnectionOnErrorOnComplete;
        private static void CloseConnectionOnErrorOnComplete(Task t, object s)
        {
            var wrapped = (Tuple<Http2ConnectionHandler, IChannelHandlerContext>)s;
            wrapped.Item1.CloseConnectionOnError(wrapped.Item2, t);
        }

        private static readonly Action<Task, object> ProcessRstStreamWriteResultOnCompleteAction = ProcessRstStreamWriteResultOnComplete;
        private static void ProcessRstStreamWriteResultOnComplete(Task t, object s)
        {
            var wrapped = (Tuple<Http2ConnectionHandler, IChannelHandlerContext, IHttp2Stream>)s;
            wrapped.Item1.ProcessRstStreamWriteResult(wrapped.Item2, wrapped.Item3, t);
        }

        private static readonly Action<Task, object> ProcessGoAwayWriteResultOnCompleteAction = ProcessGoAwayWriteResultOnComplete;
        private static void ProcessGoAwayWriteResultOnComplete(Task t, object s)
        {
            var wrapped = (Tuple<IChannelHandlerContext, int, Http2Error, IByteBuffer>)s;
            ProcessGoAwayWriteResult(wrapped.Item1, wrapped.Item2, wrapped.Item3, wrapped.Item4, t);
        }

        private static readonly Action<Task, object> CheckCloseConnOnCompleteAction = CheckCloseConnOnComplete;
        private static void CheckCloseConnOnComplete(Task t, object s)
        {
            var self = (Http2ConnectionHandler)s;
            self.CheckCloseConnection(t);
        }

        private static readonly Action<Task, object> CloseOnFailureAction = CloseOnFailure;
        private static void CloseOnFailure(Task t, object s)
        {
            if (!t.IsSuccess())
            {
                ((IChannelHandlerContext)s).Channel.CloseAsync();
            }
        }

        /// <summary>
        /// Returns the client preface string if this is a client connection, otherwise returns <c>null</c>.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        private static IByteBuffer ClientPrefaceString(IHttp2Connection connection)
        {
            return connection.IsServer ? Http2CodecUtil.ConnectionPrefaceBuf() : null;
        }

        private static void ProcessGoAwayWriteResult(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData, Task future)
        {
            try
            {
                if (future.IsSuccess())
                {
                    if (errorCode != Http2Error.NoError)
                    {
                        if (Logger.DebugEnabled)
                        {
                            Logger.SendGoAwaySuccess(ctx, lastStreamId, errorCode, debugData, future);
                        }
                        ctx.CloseAsync();
                    }
                }
                else
                {
                    if (Logger.DebugEnabled)
                    {
                        Logger.SendingGoAwayFailed(ctx, lastStreamId, errorCode, debugData, future);
                    }
                    ctx.CloseAsync();
                }
            }
            finally
            {
                // We're done with the debug data now.
                debugData.Release();
            }
        }

        sealed class ClosingChannelFutureListener
        {
            private readonly IChannelHandlerContext ctx;
            private readonly IPromise promise;
            private readonly IScheduledTask timeoutTask;

            public ClosingChannelFutureListener(IChannelHandlerContext ctx, IPromise promise)
            {
                this.ctx = ctx;
                this.promise = promise;
                this.timeoutTask = null;
            }

            private static readonly Action<object, object> CloseChannelAction = CloseChannel;
            private static void CloseChannel(object c, object p)
            {
                ((IChannelHandlerContext)c).CloseAsync((IPromise)p);
            }
            public ClosingChannelFutureListener(IChannelHandlerContext ctx, IPromise promise, TimeSpan timeout)
            {
                this.ctx = ctx;
                this.promise = promise;
                this.timeoutTask = ctx.Executor.Schedule(CloseChannelAction, ctx, promise, timeout);
            }

            public void OperationComplete(Task sentGoAwayFuture)
            {
                this.timeoutTask?.Cancel();
                this.ctx.CloseAsync(this.promise);
            }
        }
    }
}
