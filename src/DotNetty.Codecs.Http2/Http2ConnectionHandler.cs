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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
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

        private readonly IHttp2ConnectionDecoder _decoder;
        private readonly IHttp2ConnectionEncoder _encoder;
        private readonly Http2Settings _initialSettings;
        private readonly bool _decoupleCloseAndGoAway;
        private ClosingChannelFutureListener _closeListener;
        private BaseDecoder _byteDecoder;
        private TimeSpan _gracefulShutdownTimeout;

        private static ReadOnlySpan<byte> HTTP_1_X_BUF => new byte[] { (byte)'H', (byte)'T', (byte)'T', (byte)'P', (byte)'/', (byte)'1', (byte)'.' };

        public Http2ConnectionHandler(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings)
            : this(decoder, encoder, initialSettings, false)
        {
        }

        public Http2ConnectionHandler(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings, bool decoupleCloseAndGoAway)
        {
            if (initialSettings is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.initialSettings); }
            if (decoder is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.decoder); }
            if (encoder is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.encoder); }
            _initialSettings = initialSettings;
            _decoder = decoder;
            _encoder = encoder;
            _decoupleCloseAndGoAway = decoupleCloseAndGoAway;
            if (encoder.Connection != decoder.Connection)
            {
                ThrowHelper.ThrowArgumentException_EncoderAndDecoderDonotShareTheSameConnObject();
            }
        }

        public TimeSpan GracefulShutdownTimeout
        {
            get => _gracefulShutdownTimeout;
            set
            {
                if (value < Timeout.InfiniteTimeSpan)
                {
                    ThrowHelper.ThrowArgumentException_GracefulShutdownTimeout(value);
                }
                _gracefulShutdownTimeout = value;
            }
        }

        public IHttp2Connection Connection => _encoder.Connection;

        public IHttp2ConnectionDecoder Decoder => _decoder;

        public IHttp2ConnectionEncoder Encoder => _encoder;

        private bool PrefaceSent => _byteDecoder is object && _byteDecoder.PrefaceSent;

        /// <summary>
        /// Handles the client-side (cleartext) upgrade from HTTP to HTTP/2.
        /// Reserves local stream 1 for the HTTP/2 response.
        /// </summary>
        public virtual void OnHttpClientUpgrade()
        {
            if (Connection.IsServer)
            {
                ThrowHelper.ThrowConnectionError_HttpUpgradeRequested(true);
            }
            if (!PrefaceSent)
            {
                // If the preface was not sent yet it most likely means the handler was not added to the pipeline before
                // calling this method.
                ThrowHelper.ThrowConnectionError_HttpUpgradeMustOccurAfterPrefaceWasSent();
            }
            if (_decoder.PrefaceReceived)
            {
                ThrowHelper.ThrowConnectionError_HttpUpgradeMustOccurBeforeHttp2PrefaceIsReceived();
            }

            // Create a local stream used for the HTTP cleartext upgrade.
            _ = Connection.Local.CreateStream(Http2CodecUtil.HttpUpgradeStreamId, true);
        }

        /// <summary>
        /// Handles the server-side (cleartext) upgrade from HTTP to HTTP/2.
        /// </summary>
        /// <param name="settings">the settings for the remote endpoint.</param>
        public virtual void OnHttpServerUpgrade(Http2Settings settings)
        {
            if (!Connection.IsServer)
            {
                ThrowHelper.ThrowConnectionError_HttpUpgradeRequested(false);
            }
            if (!PrefaceSent)
            {
                // If the preface was not sent yet it most likely means the handler was not added to the pipeline before
                // calling this method.
                ThrowHelper.ThrowConnectionError_HttpUpgradeMustOccurAfterPrefaceWasSent();
            }
            if (_decoder.PrefaceReceived)
            {
                ThrowHelper.ThrowConnectionError_HttpUpgradeMustOccurBeforeHttp2PrefaceIsReceived();
            }

            // Apply the settings but no ACK is necessary.
            _encoder.RemoteSettings(settings);

            // Create a stream in the half-closed state.
            _ = Connection.Remote.CreateStream(Http2CodecUtil.HttpUpgradeStreamId, true);
        }

        public override void Flush(IChannelHandlerContext ctx)
        {
            try
            {
                // Trigger pending writes in the remote flow controller.
                _encoder.FlowController.WritePendingBytes();
                _ = ctx.Flush();
            }
            catch (Http2Exception e)
            {
                OnError(ctx, true, e);
            }
            catch (Exception cause)
            {
                OnError(ctx, true, ThrowHelper.GetConnectionError_ErrorFlushing(cause));
            }
        }

        private abstract class BaseDecoder
        {
            protected readonly Http2ConnectionHandler _connHandler;

            public BaseDecoder(Http2ConnectionHandler connHandler) => _connHandler = connHandler;

            public abstract void Decode(IChannelHandlerContext ctx, IByteBuffer input, List<object> output);

            public virtual void HandlerRemoved(IChannelHandlerContext ctx) { }

            public virtual void ChannelActive(IChannelHandlerContext ctx) { }

            public virtual void ChannelInactive(IChannelHandlerContext ctx)
            {
                // Connection has terminated, close the encoder and decoder.
                _connHandler._encoder.Close();
                _connHandler._decoder.Close();

                // We need to remove all streams (not just the active ones).
                // See https://github.com/netty/netty/issues/4838.
                _ = _connHandler.Connection.CloseAsync(ctx.VoidPromise());
            }

            /// <summary>
            /// Determine if the HTTP/2 connection preface been sent.
            /// </summary>
            /// <returns></returns>
            public virtual bool PrefaceSent => true;
        }

        private sealed class PrefaceDecoder : BaseDecoder
        {
            private IByteBuffer _clientPrefaceString;
            private bool _prefaceSent;

            public PrefaceDecoder(Http2ConnectionHandler connHandler, IChannelHandlerContext ctx)
                : base(connHandler)
            {
                _clientPrefaceString = ClientPrefaceString(connHandler.Connection);
                // This handler was just added to the context. In case it was handled after
                // the connection became active, send the connection preface now.
                SendPreface(ctx);
            }

            public override bool PrefaceSent => _prefaceSent;

            public override void Decode(IChannelHandlerContext ctx, IByteBuffer input, List<object> output)
            {
                try
                {
                    if (ctx.Channel.IsActive && ReadClientPrefaceString(input) && VerifyFirstFrameIsSettings(input))
                    {
                        // After the preface is read, it is time to hand over control to the post initialized decoder.
                        var byteDecoder = new FrameDecoder(_connHandler);
                        _connHandler._byteDecoder = byteDecoder;
                        byteDecoder.Decode(ctx, input, output);
                    }
                }
                catch (Exception e)
                {
                    _connHandler.OnError(ctx, false, e);
                }
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                // The channel just became active - send the connection preface to the remote endpoint.
                SendPreface(ctx);
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                Cleanup();
                base.ChannelInactive(ctx);
            }

            public override void HandlerRemoved(IChannelHandlerContext ctx)
            {
                Cleanup();
            }

            /// <summary>
            /// Releases the <see cref="_clientPrefaceString"/>. Any active streams will be left in the open.
            /// </summary>
            private void Cleanup()
            {
                var clientPrefaceString = _clientPrefaceString;
                if (clientPrefaceString is object)
                {
                    _ = clientPrefaceString.Release();
                    _clientPrefaceString = null;
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
                if (_clientPrefaceString is null)
                {
                    return true;
                }

                int prefaceRemaining = _clientPrefaceString.ReadableBytes;
                int bytesRead = Math.Min(input.ReadableBytes, prefaceRemaining);

                // If the input so far doesn't match the preface, break the connection.
                if (0u >= (uint)bytesRead || !ByteBufferUtil.Equals(input, input.ReaderIndex,
                                                             _clientPrefaceString, _clientPrefaceString.ReaderIndex,
                                                             bytesRead))
                {
                    int maxSearch = 1024; // picked because 512 is too little, and 2048 too much
                    int http1Index =
                        ByteBufferUtil.IndexOf(HTTP_1_X_BUF, input.Slice(input.ReaderIndex, Math.Min(input.ReadableBytes, maxSearch)));
                    if (http1Index != -1)
                    {
                        ThrowHelper.ThrowConnectionError_UnexpectedHttp1Request(input, http1Index);
                    }
                    ThrowHelper.ThrowConnectionError_Http2ClientPrefaceStringMissingOrCorrupt(input, _clientPrefaceString.ReadableBytes);
                }
                _ = input.SkipBytes(bytesRead);
                _ = _clientPrefaceString.SkipBytes(bytesRead);

                if (!_clientPrefaceString.IsReadable())
                {
                    // Entire preface has been read.
                    _ = _clientPrefaceString.Release();
                    _clientPrefaceString = null;
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
                if (_prefaceSent || !ctx.Channel.IsActive) { return; }

                _prefaceSent = true;

                var isClient = !_connHandler.Connection.IsServer;
                if (isClient)
                {
                    // Clients must send the preface string as the first bytes on the connection.
                    _ = ctx.WriteAsync(Http2CodecUtil.ConnectionPrefaceBuf())
                       .CloseOnFailure(ctx.Channel);
                }

                // Both client and server must send their initial settings.
                _ = _connHandler._encoder
                        .WriteSettingsAsync(ctx, _connHandler._initialSettings, ctx.NewPromise())
                        .CloseOnFailure(ctx.Channel);

                if (isClient)
                {
                    // If this handler is extended by the user and we directly fire the userEvent from this context then
                    // the user will not see the event. We should fire the event starting with this handler so this class
                    // (and extending classes) have a chance to process the event.
                    _connHandler.UserEventTriggered(ctx, Http2ConnectionPrefaceAndSettingsFrameWrittenEvent.Instance);
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
                    _connHandler._decoder.DecodeFrame(ctx, input, output);
                }
                catch (Exception e)
                {
                    _connHandler.OnError(ctx, false, e);
                }
            }
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            // Initialize the encoder, decoder, flow controllers, and internal state.
            _encoder.LifecycleManager(this);
            _decoder.LifecycleManager(this);
            _encoder.FlowController.SetChannelHandlerContext(ctx);
            _decoder.FlowController.SetChannelHandlerContext(ctx);
            _byteDecoder = new PrefaceDecoder(this, ctx);
        }

        protected override void HandlerRemovedInternal(IChannelHandlerContext ctx)
        {
            if (_byteDecoder is object)
            {
                _byteDecoder.HandlerRemoved(ctx);
                _byteDecoder = null;
            }
        }

        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            if (_byteDecoder is null)
            {
                _byteDecoder = new PrefaceDecoder(this, ctx);
            }
            _byteDecoder.ChannelActive(ctx);
            base.ChannelActive(ctx);
        }

        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            // Call super class first, as this may result in decode being called.
            base.ChannelInactive(ctx);
            if (_byteDecoder is object)
            {
                _byteDecoder.ChannelInactive(ctx);
                _byteDecoder = null;
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
                    Flush(ctx);
                }
                _encoder.FlowController.ChannelWritabilityChanged();
            }
            finally
            {
                base.ChannelWritabilityChanged(ctx);
            }
        }

        protected override void Decode(IChannelHandlerContext ctx, IByteBuffer input, List<object> output)
        {
            _byteDecoder.Decode(ctx, input, output);
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
            _ = context.DisconnectAsync(promise);
        }

        public override void Close(IChannelHandlerContext ctx, IPromise promise)
        {
            if (_decoupleCloseAndGoAway)
            {
                _ = ctx.CloseAsync(promise);
                return;
            }
            promise = promise.Unvoid();
            // Avoid NotYetConnectedException
            if (!ctx.Channel.IsActive)
            {
                _ = ctx.CloseAsync(promise);
                return;
            }

            // If the user has already sent a GO_AWAY frame they may be attempting to do a graceful shutdown which requires
            // sending multiple GO_AWAY frames. We should only send a GO_AWAY here if one has not already been sent. If
            // a GO_AWAY has been sent we send a empty buffer just so we can wait to close until all other data has been
            // flushed to the OS.
            // https://github.com/netty/netty/issues/5307
            var future = Connection.GoAwaySent() ? ctx.WriteAsync(Unpooled.Empty) : GoAwayAsync(ctx, null, ctx.NewPromise());
            _ = ctx.Flush();
            DoGracefulShutdown(ctx, future, promise);
        }

        private ClosingChannelFutureListener NewClosingChannelFutureListener(IChannelHandlerContext ctx, IPromise promise,
            ClosingChannelFutureListener closeListener = null)
        {
            if (_gracefulShutdownTimeout < TimeSpan.Zero)
            {
                return closeListener is null
                    ? new ClosingChannelFutureListener(ctx, promise)
                    : new ClosingChannelFutureListenerWrapper(ctx, promise, closeListener);
            }
            else
            {
                return closeListener is null
                    ? new ClosingChannelFutureListener(ctx, promise, _gracefulShutdownTimeout)
                    : new ClosingChannelFutureListenerWrapper(ctx, promise, _gracefulShutdownTimeout, closeListener);
            }
        }

        private void CreateClosingChannelFutureListener(Task future, IChannelHandlerContext ctx, IPromise promise)
        {
            IScheduledTask timeoutTask = null;
            if (_gracefulShutdownTimeout >= TimeSpan.Zero)
            {
                timeoutTask = ctx.Executor.Schedule(ScheduledCloseChannelAction, ctx, promise, _gracefulShutdownTimeout);
            }
            _ = future.ContinueWith(CloseChannelOnCompleteAction,
                    (ctx, promise, timeoutTask), TaskContinuationOptions.ExecuteSynchronously);
        }

        private void DoGracefulShutdown(IChannelHandlerContext ctx, Task future, IPromise promise)
        {
            if (IsGracefulShutdownComplete)
            {
                // If there are no active streams, close immediately after the GO_AWAY write completes or the timeout
                // elapsed.
                if (future.IsCompleted)
                {
#if DEBUG
                    // only for testing
                    CreateClosingChannelFutureListener(future, ctx, promise);
#else
                    ctx.CloseAsync(promise);
#endif
                }
                else
                {
                    CreateClosingChannelFutureListener(future, ctx, promise);
                }
            }
            else
            {
                // If there are active streams we should wait until they are all closed before closing the connection.

                // The ClosingChannelFutureListener will cascade promise completion. We need to always notify the
                // new ClosingChannelFutureListener when the graceful close completes if the promise is not null.
                if (_closeListener is null)
                {
                    _closeListener = NewClosingChannelFutureListener(ctx, promise);
                }
                else if (promise is object)
                {
                    var oldCloseListener = _closeListener;
                    _closeListener = NewClosingChannelFutureListener(ctx, promise, oldCloseListener);
                }
            }
        }

        public override void Deregister(IChannelHandlerContext context, IPromise promise)
        {
            _ = context.DeregisterAsync(promise);
        }

        public override void Read(IChannelHandlerContext context)
        {
            _ = context.Read();
        }

        public override void Write(IChannelHandlerContext context, object message, IPromise promise)
        {
            _ = context.WriteAsync(message, promise);
        }

        public override void ChannelReadComplete(IChannelHandlerContext ctx)
        {
            // Trigger flush after read on the assumption that flush is cheap if there is nothing to write and that
            // for flow-control the read may release window that causes data to be written that can now be flushed.
            try
            {
                // First call channelReadComplete0(...) as this may produce more data that we want to flush
                ChannelReadComplete0(ctx);
            }
            finally
            {
                Flush(ctx);
            }
        }

        protected void ChannelReadComplete0(IChannelHandlerContext ctx)
        {
            // Discard bytes of the cumulation buffer if needed.
            DiscardSomeReadBytes();

            // Ensure we never stale the HTTP/2 Channel. Flow-control is enforced by HTTP/2.
            //
            // See https://tools.ietf.org/html/rfc7540#section-5.2.2
            if (!ctx.Channel.Configuration.IsAutoRead)
            {
                _ = ctx.Read();
            }

            _ = ctx.FireChannelReadComplete();
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
                OnError(ctx, false, cause);
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
            switch (stream.State)
            {
                case Http2StreamState.HalfClosedLocal:
                case Http2StreamState.Open:
                    _ = stream.CloseLocalSide();
                    break;

                default:
                    CloseStream(stream, future);
                    break;
            }
        }

        public virtual void CloseStreamRemote(IHttp2Stream stream, Task future)
        {
            switch (stream.State)
            {
                case Http2StreamState.HalfClosedRemote:
                case Http2StreamState.Open:
                    _ = stream.CloseRemoteSide();
                    break;

                default:
                    CloseStream(stream, future);
                    break;
            }
        }

        public virtual void CloseStream(IHttp2Stream stream, Task future)
        {
            _ = stream.Close();

            if (future.IsCompleted)
            {
                CheckCloseConnection(future);
            }
            else
            {
                _ = future.ContinueWith(CheckCloseConnOnCompleteAction, this, TaskContinuationOptions.ExecuteSynchronously);
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
                OnStreamError(ctx, outbound, cause, (StreamException)embedded);
            }
            else if (embedded is CompositeStreamException compositException)
            {
                foreach (StreamException streamException in compositException)
                {
                    OnStreamError(ctx, outbound, cause, streamException);
                }
            }
            else
            {
                OnConnectionError(ctx, outbound, cause, embedded);
            }
            _ = ctx.Flush();
        }

        /// <summary>
        /// Called by the graceful shutdown logic to determine when it is safe to close the connection. Returns <c>true</c>
        /// if the graceful shutdown has completed and the connection can be safely closed. This implementation just
        /// guarantees that there are no active streams. Subclasses may override to provide additional checks.
        /// </summary>
        protected virtual bool IsGracefulShutdownComplete => 0u >= (uint)Connection.NumActiveStreams;

        /// <summary>
        /// Handler for a connection error. Sends a <c>GO_AWAY</c> frame to the remote endpoint. Once all
        /// streams are closed, the connection is shut down.
        /// </summary>
        /// <param name="ctx">the channel context</param>
        /// <param name="outbound"><c>true</c> if the error was caused by an outbound operation.</param>
        /// <param name="cause">the exception that was caught</param>
        /// <param name="http2Ex">the <see cref="Http2Exception"/> that is embedded in the causality chain. This may
        /// be <c>null</c> if it's an unknown exception.</param>
        protected internal virtual void OnConnectionError(IChannelHandlerContext ctx, bool outbound, Exception cause, Http2Exception http2Ex)
        {
            if (http2Ex is null)
            {
                http2Ex = new Http2Exception(Http2Error.InternalError, cause.Message, cause);
            }

            var promise = ctx.NewPromise();
            var future = GoAwayAsync(ctx, http2Ex, ctx.NewPromise());
            if (http2Ex.ShutdownHint == ShutdownHint.GracefulShutdown)
            {
                DoGracefulShutdown(ctx, future, promise);
            }
            else
            {
                if (future.IsCompleted)
                {
#if DEBUG
                    // only for testing
                    CreateClosingChannelFutureListener(future, ctx, promise);
#else
                    ctx.CloseAsync(promise);
#endif
                }
                else
                {
                    CreateClosingChannelFutureListener(future, ctx, promise);
                }
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
            var stream = Connection.Stream(streamId);

            //if this is caused by reading headers that are too large, send a header with status 431
            if (http2Ex is HeaderListSizeException headerListSizeException &&
                headerListSizeException.DuringDecode &&
                Connection.IsServer)
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
                        stream = _encoder.Connection.Remote.CreateStream(streamId, true);
                    }
                    catch (Http2Exception)
                    {
                        _ = ResetUnknownStreamAsync(ctx, streamId, http2Ex.Error, ctx.NewPromise());
                        return;
                    }
                }

                // ensure that we have not already sent headers on this stream
                if (stream is object && !stream.IsHeadersSent)
                {
                    try
                    {
                        HandleServerHeaderDecodeSizeError(ctx, stream);
                    }
                    catch (Exception cause2)
                    {
                        OnError(ctx, outbound, ThrowHelper.GetConnectionError_ErrorDecodeSizeError(cause2));
                    }
                }
            }

            if (stream is null)
            {
                if (!outbound || Connection.Local.MayHaveCreatedStream(streamId))
                {
                    _ = ResetUnknownStreamAsync(ctx, streamId, http2Ex.Error, ctx.NewPromise());
                }
            }
            else
            {
                _ = ResetStreamAsync(ctx, stream, http2Ex.Error, ctx.NewPromise());
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
            _ = _encoder.WriteHeadersAsync(ctx, stream.Id, HEADERS_TOO_LARGE_HEADERS, 0, true, ctx.NewPromise());
        }

        protected IHttp2FrameWriter FrameWriter => _encoder.FrameWriter;

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
            var future = FrameWriter.WriteRstStreamAsync(ctx, streamId, errorCode, promise);
            if (future.IsCompleted)
            {
                CloseConnectionOnError(ctx, future);
            }
            else
            {
                _ = future.ContinueWith(CloseConnectionOnErrorOnCompleteAction, (this, ctx), TaskContinuationOptions.ExecuteSynchronously);
            }
            return future;
        }

        public virtual Task ResetStreamAsync(IChannelHandlerContext ctx, int streamId, Http2Error errorCode, IPromise promise)
        {
            var stream = Connection.Stream(streamId);
            if (stream is null)
            {
                return ResetUnknownStreamAsync(ctx, streamId, errorCode, promise.Unvoid());
            }

            return ResetStreamAsync(ctx, stream, errorCode, promise);
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
            // Synchronously set the resetSent flag to prevent any subsequent calls
            // from resulting in multiple reset frames being sent.
            //
            // This needs to be done before we notify the promise as the promise may have a listener attached that
            // call resetStream(...) again.
            _ = stream.ResetSent();

            Task future;
            // If the remote peer is not aware of the steam, then we are not allowed to send a RST_STREAM
            // https://tools.ietf.org/html/rfc7540#section-6.4.
            if (Http2StreamState.Idle == stream.State ||
                Connection.Local.Created(stream) && !stream.IsHeadersSent && !stream.IsPushPromiseSent)
            {
                promise.Complete();
                future = promise.Task;
            }
            else
            {
                future = FrameWriter.WriteRstStreamAsync(ctx, stream.Id, errorCode, promise);
            }

            if (future.IsCompleted)
            {
                ProcessRstStreamWriteResult(ctx, stream, future);
            }
            else
            {
                _ = future.ContinueWith(ProcessRstStreamWriteResultOnCompleteAction,
                        (this, ctx, stream), TaskContinuationOptions.ExecuteSynchronously);
            }

            return future;
        }

        public virtual Task GoAwayAsync(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData, IPromise promise)
        {
            promise = promise.Unvoid();
            var connection = Connection;
            try
            {
                if (!connection.GoAwaySent(lastStreamId, errorCode, debugData))
                {
                    _ = debugData.Release();
                    _ = promise.TryComplete();
                    return promise.Task;
                }
            }
            catch (Exception cause)
            {
                _ = debugData.Release();
                _ = promise.TrySetException(cause);
                return promise.Task;
            }

            // Need to retain before we write the buffer because if we do it after the refCnt could already be 0 and
            // result in an IllegalRefCountException.
            _ = debugData.Retain();
            var future = FrameWriter.WriteGoAwayAsync(ctx, lastStreamId, errorCode, debugData, promise);

            if (future.IsCompleted)
            {
                ProcessGoAwayWriteResult(ctx, lastStreamId, errorCode, debugData, future);
            }
            else
            {
                _ = future.ContinueWith(ProcessGoAwayWriteResultOnCompleteAction,
                        (ctx, lastStreamId, errorCode, debugData), TaskContinuationOptions.ExecuteSynchronously);
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
            if (_closeListener is object && IsGracefulShutdownComplete)
            {
                var closeListener = _closeListener;
                // This method could be called multiple times
                // and we don't want to notify the closeListener multiple times.
                _closeListener = null;
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

        private Task GoAwayAsync(IChannelHandlerContext ctx, Http2Exception cause, IPromise promise)
        {
            var errorCode = cause is object ? cause.Error : Http2Error.NoError;
            int lastKnownStream = Connection.Remote.LastStreamCreated;
            return GoAwayAsync(ctx, lastKnownStream, errorCode, Http2CodecUtil.ToByteBuf(ctx, cause), promise);
        }

        private void ProcessRstStreamWriteResult(IChannelHandlerContext ctx, IHttp2Stream stream, Task future)
        {
            if (future.IsSuccess())
            {
                CloseStream(stream, future);
            }
            else
            {
                // The connection will be closed and so no need to change the resetSent flag to false.
                OnConnectionError(ctx, true, future.Exception.InnerException, null);
            }
        }

        private void CloseConnectionOnError(IChannelHandlerContext ctx, Task future)
        {
            if (future.IsFailure())
            {
                OnConnectionError(ctx, true, future.Exception.InnerException, null);
            }
        }

        private static readonly Action<Task, object> CloseChannelOnCompleteAction = (t, s) => CloseChannelOnComplete(t, s);
        private static void CloseChannelOnComplete(Task t, object s)
        {
            var (ctx, promise, timeoutTask) = ((IChannelHandlerContext, IPromise, IScheduledTask))s;
            _ = timeoutTask?.Cancel();
            if (promise is object)
            {
                _ = ctx.CloseAsync(promise);
            }
            else
            {
                _ = ctx.CloseAsync();
            }
        }
        private static readonly Action<object, object> ScheduledCloseChannelAction = (c, p) => ScheduledCloseChannel(c, p);
        private static void ScheduledCloseChannel(object c, object p)
        {
            _ = ((IChannelHandlerContext)c).CloseAsync((IPromise)p);
        }

        private static readonly Action<Task, object> CloseConnectionOnErrorOnCompleteAction = (t, s) => CloseConnectionOnErrorOnComplete(t, s);
        private static void CloseConnectionOnErrorOnComplete(Task t, object s)
        {
            var (self, ctx) = ((Http2ConnectionHandler, IChannelHandlerContext))s;
            self.CloseConnectionOnError(ctx, t);
        }

        private static readonly Action<Task, object> ProcessRstStreamWriteResultOnCompleteAction = (t, s) => ProcessRstStreamWriteResultOnComplete(t, s);
        private static void ProcessRstStreamWriteResultOnComplete(Task t, object s)
        {
            var (self, ctx, stream) = ((Http2ConnectionHandler, IChannelHandlerContext, IHttp2Stream))s;
            self.ProcessRstStreamWriteResult(ctx, stream, t);
        }

        private static readonly Action<Task, object> ProcessGoAwayWriteResultOnCompleteAction = (t, s) => ProcessGoAwayWriteResultOnComplete(t, s);
        private static void ProcessGoAwayWriteResultOnComplete(Task t, object s)
        {
            var (ctx, lastStreamId, errorCode, debugData) = ((IChannelHandlerContext, int, Http2Error, IByteBuffer))s;
            ProcessGoAwayWriteResult(ctx, lastStreamId, errorCode, debugData, t);
        }

        private static readonly Action<Task, object> CheckCloseConnOnCompleteAction = (t, s) => CheckCloseConnOnComplete(t, s);
        private static void CheckCloseConnOnComplete(Task t, object s)
        {
            var self = (Http2ConnectionHandler)s;
            self.CheckCloseConnection(t);
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
#if DEBUG
                        if (Logger.DebugEnabled)
                        {
                            Logger.SendGoAwaySuccess(ctx, lastStreamId, errorCode, debugData, future);
                        }
#endif
                        _ = ctx.CloseAsync();
                    }
                }
                else
                {
#if DEBUG
                    if (Logger.DebugEnabled)
                    {
                        Logger.SendingGoAwayFailed(ctx, lastStreamId, errorCode, debugData, future);
                    }
#endif
                    _ = ctx.CloseAsync();
                }
            }
            finally
            {
                // We're done with the debug data now.
                _ = debugData.Release();
            }
        }

        sealed class ClosingChannelFutureListenerWrapper : ClosingChannelFutureListener
        {
            private readonly ClosingChannelFutureListener _oldCloseListener;

            public ClosingChannelFutureListenerWrapper(IChannelHandlerContext ctx, IPromise promise, ClosingChannelFutureListener closeListener)
                : base(ctx, promise)
            {
                _oldCloseListener = closeListener;
            }

            public ClosingChannelFutureListenerWrapper(IChannelHandlerContext ctx, IPromise promise, TimeSpan timeout, ClosingChannelFutureListener closeListener)
                : base(ctx, promise, timeout)
            {
                _oldCloseListener = closeListener;
            }

            public override void OperationComplete(Task sentGoAwayFuture)
            {
                try
                {
                    _oldCloseListener.OperationComplete(sentGoAwayFuture);
                }
                finally
                {
                    base.OperationComplete(sentGoAwayFuture);
                }
            }
        }

        class ClosingChannelFutureListener
        {
            private readonly IChannelHandlerContext _ctx;
            private readonly IPromise _promise;
            private readonly IScheduledTask _timeoutTask;
            private bool _closed;

            public ClosingChannelFutureListener(IChannelHandlerContext ctx, IPromise promise)
            {
                _ctx = ctx;
                _promise = promise;
                _timeoutTask = null;
            }

            private static readonly Action<object> CloseChannelAction = c => CloseChannel(c);
            private static void CloseChannel(object c)
            {
                ((ClosingChannelFutureListener)c).DoClose();
            }
            public ClosingChannelFutureListener(IChannelHandlerContext ctx, IPromise promise, TimeSpan timeout)
            {
                _ctx = ctx;
                _promise = promise;
                _timeoutTask = ctx.Executor.Schedule(CloseChannelAction, this, timeout);
            }

            public virtual void OperationComplete(Task sentGoAwayFuture)
            {
                _ = (_timeoutTask?.Cancel());
                DoClose();
            }

            private void DoClose()
            {
                // We need to guard against multiple calls as the timeout may trigger close() first and then it will be
                // triggered again because of operationComplete(...) is called.
                if (_closed)
                {
                    // This only happens if we also scheduled a timeout task.
                    Debug.Assert(_timeoutTask is object);
                    return;
                }
                _closed = true;
                if (_promise is null)
                {
                    _ = _ctx.CloseAsync();
                }
                else
                {
                    _ = _ctx.CloseAsync(_promise);
                }
            }
        }
    }
}
