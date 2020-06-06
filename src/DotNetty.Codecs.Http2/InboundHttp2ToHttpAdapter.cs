// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// This adapter provides just header/data events from the HTTP message flow defined
    /// in <a href="https://tools.ietf.org/html/rfc7540#section-8.1">[RFC 7540], Section 8.1</a>.
    /// <para>See <see cref="HttpToHttp2ConnectionHandler"/> to get translation from HTTP/1.x objects to HTTP/2 frames for writes.</para>
    /// </summary>
    public class InboundHttp2ToHttpAdapter : Http2EventAdapter
    {
        private readonly int _maxContentLength;
        private readonly IImmediateSendDetector _sendDetector;
        private readonly IHttp2ConnectionPropertyKey _messageKey;
        private readonly bool _propagateSettings;
        protected readonly IHttp2Connection _connection;
        protected readonly bool _validateHttpHeaders;

        public InboundHttp2ToHttpAdapter(IHttp2Connection connection, int maxContentLength,
            bool validateHttpHeaders, bool propagateSettings)
        {
            if (connection is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connection); }
            if (maxContentLength <= 0) { ThrowHelper.ThrowArgumentException_Positive(maxContentLength, ExceptionArgument.maxContentLength); }

            _connection = connection;
            _maxContentLength = maxContentLength;
            _validateHttpHeaders = validateHttpHeaders;
            _propagateSettings = propagateSettings;
            _sendDetector = DefaultImmediateSendDetector.Instance;
            _messageKey = connection.NewKey();
        }

        /// <summary>
        /// The stream is out of scope for the HTTP message flow and will no longer be tracked
        /// </summary>
        /// <param name="stream">The stream to remove associated state with</param>
        /// <param name="release"><c>true</c> to call release on the value if it is present. <c>false</c> to not call release.</param>
        protected void RemoveMessage(IHttp2Stream stream, bool release)
        {
            IFullHttpMessage msg = stream.RemoveProperty<IFullHttpMessage>(_messageKey);
            if (release && msg is object)
            {
                msg.Release();
            }
        }

        /// <summary>
        /// Get the <see cref="IFullHttpMessage"/> associated with <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">The stream to get the associated state from</param>
        /// <returns>The <see cref="IFullHttpMessage"/> associated with <paramref name="stream"/>.</returns>
        protected IFullHttpMessage GetMessage(IHttp2Stream stream)
        {
            return stream.GetProperty<IFullHttpMessage>(_messageKey);
        }

        /// <summary>
        /// Make <paramref name="message"/> be the state associated with <paramref name="stream"/>.
        /// </summary>
        /// <param name="stream">The stream which <paramref name="message"/> is associated with.</param>
        /// <param name="message">The message which contains the HTTP semantics.</param>
        protected void PutMessage(IHttp2Stream stream, IFullHttpMessage message)
        {
            IFullHttpMessage previous = stream.SetProperty<IFullHttpMessage>(_messageKey, message);
            if (previous != message && previous is object)
            {
                previous.Release();
            }
        }

        public override void OnStreamRemoved(IHttp2Stream stream)
        {
            RemoveMessage(stream, true);
        }

        /// <summary>
        /// Set final headers and fire a channel read event
        /// </summary>
        /// <param name="ctx">The context to fire the event on</param>
        /// <param name="msg">The message to send</param>
        /// <param name="release"><c>true</c> to call release on the value if it is present. <c>false</c> to not call release.</param>
        /// <param name="stream">the stream of the message which is being fired</param>
        protected virtual void FireChannelRead(IChannelHandlerContext ctx, IFullHttpMessage msg, bool release, IHttp2Stream stream)
        {
            RemoveMessage(stream, release);
            HttpUtil.SetContentLength(msg, msg.Content.ReadableBytes);
            ctx.FireChannelRead(msg);
        }

        /// <summary>
        /// Create a new <see cref="IFullHttpMessage"/> based upon the current connection parameters
        /// </summary>
        /// <param name="stream">The stream to create a message for</param>
        /// <param name="headers">The headers associated with <paramref name="stream"/>.</param>
        /// <param name="validateHttpHeaders"><c>true</c> to validate HTTP headers in the http-codec
        /// <para><c>false</c> not to validate HTTP headers in the http-codec</para></param>
        /// <param name="alloc">The <see cref="IByteBufferAllocator"/> to use to generate the content of the message</param>
        /// <returns></returns>
        protected virtual IFullHttpMessage NewMessage(IHttp2Stream stream, IHttp2Headers headers, bool validateHttpHeaders,
            IByteBufferAllocator alloc)
        {
            if (_connection.IsServer)
            {
                return HttpConversionUtil.ToFullHttpRequest(stream.Id, headers, alloc, validateHttpHeaders);
            }
            return HttpConversionUtil.ToFullHttpResponse(stream.Id, headers, alloc, validateHttpHeaders);
        }

        /// <summary>
        /// Provides translation between HTTP/2 and HTTP header objects while ensuring the stream
        /// is in a valid state for additional headers.
        /// </summary>
        /// <param name="ctx">The context for which this message has been received.
        /// Used to send informational header if detected.</param>
        /// <param name="stream">The stream the <paramref name="headers"/> apply to</param>
        /// <param name="headers">The headers to process</param>
        /// <param name="endOfStream"><c>true</c> if the <paramref name="stream"/> has received the end of stream flag</param>
        /// <param name="allowAppend"><c>true</c> if headers will be appended if the stream already exists.
        /// <para>if <c>false</c> and the stream already exists this method returns <c>null</c>.</para></param>
        /// <param name="appendToTrailer"><c>true</c> if a message <paramref name="stream"/> already exists then the headers
        /// should be added to the trailing headers.
        /// <para><c>false</c> then appends will be done to the initial headers.</para></param>
        /// <returns>The object used to track the stream corresponding to <paramref name="stream"/>. <c>null</c> if
        /// <paramref name="allowAppend"/> is <c>false</c> and the stream already exists.</returns>
        /// <exception cref="Http2Exception">If the stream id is not in the correct state to process the headers request</exception>
        protected virtual IFullHttpMessage ProcessHeadersBegin(IChannelHandlerContext ctx, IHttp2Stream stream, IHttp2Headers headers,
            bool endOfStream, bool allowAppend, bool appendToTrailer)
        {
            IFullHttpMessage msg = GetMessage(stream);
            var release = true;
            if (msg is null)
            {
                msg = NewMessage(stream, headers, _validateHttpHeaders, ctx.Allocator);
            }
            else if (allowAppend)
            {
                release = false;
                HttpConversionUtil.AddHttp2ToHttpHeaders(stream.Id, headers, msg, appendToTrailer);
            }
            else
            {
                release = false;
                msg = null;
            }

            if (_sendDetector.MustSendImmediately(msg))
            {
                // Copy the message (if necessary) before sending. The content is not expected to be copied (or used) in
                // this operation but just in case it is used do the copy before sending and the resource may be released
                IFullHttpMessage copy = endOfStream ? null : _sendDetector.CopyIfNeeded(ctx.Allocator, msg);
                FireChannelRead(ctx, msg, release, stream);
                return copy;
            }

            return msg;
        }

        /// <summary>
        /// After HTTP/2 headers have been processed by <see cref="ProcessHeadersBegin"/> this method either
        /// sends the result up the pipeline or retains the message for future processing.
        /// </summary>
        /// <param name="ctx">The context for which this message has been received</param>
        /// <param name="stream">The stream the <paramref name="msg"/> corresponds to</param>
        /// <param name="msg">The object which represents all headers/data for corresponding to <paramref name="stream"/>.</param>
        /// <param name="endOfStream"><c>true</c> if this is the last event for the stream</param>
        private void ProcessHeadersEnd(IChannelHandlerContext ctx, IHttp2Stream stream, IFullHttpMessage msg, bool endOfStream)
        {
            if (endOfStream)
            {
                // Release if the msg from the map is different from the object being forwarded up the pipeline.
                FireChannelRead(ctx, msg, GetMessage(stream) != msg, stream);
            }
            else
            {
                PutMessage(stream, msg);
            }
        }

        public override int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
        {
            IHttp2Stream stream = _connection.Stream(streamId);
            IFullHttpMessage msg = GetMessage(stream);
            if (msg is null)
            {
                ThrowHelper.ThrowConnectionError_DataFrameReceivedForUnknownStream(streamId);
            }

            var content = msg.Content;
            int dataReadableBytes = data.ReadableBytes;
            if (content.ReadableBytes > _maxContentLength - dataReadableBytes)
            {
                ThrowHelper.ThrowConnectionError_ContentLengthExceededMax(_maxContentLength, streamId);
            }

            content.WriteBytes(data, data.ReaderIndex, dataReadableBytes);

            if (endOfStream)
            {
                FireChannelRead(ctx, msg, false, stream);
            }

            // All bytes have been processed.
            return dataReadableBytes + padding;
        }

        public override void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
        {
            IHttp2Stream stream = _connection.Stream(streamId);
            IFullHttpMessage msg = ProcessHeadersBegin(ctx, stream, headers, endOfStream, true, true);
            if (msg is object)
            {
                ProcessHeadersEnd(ctx, stream, msg, endOfStream);
            }
        }

        public override void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
        {
            IHttp2Stream stream = _connection.Stream(streamId);
            IFullHttpMessage msg = ProcessHeadersBegin(ctx, stream, headers, endOfStream, true, true);
            if (msg is object)
            {
                // Add headers for dependency and weight.
                // See https://github.com/netty/netty/issues/5866
                if (streamDependency != Http2CodecUtil.ConnectionStreamId)
                {
                    msg.Headers.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamDependencyId, streamDependency);
                }
                msg.Headers.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, weight);

                ProcessHeadersEnd(ctx, stream, msg, endOfStream);
            }
        }

        public override void OnRstStreamRead(IChannelHandlerContext ctx, int streamId, Http2Error errorCode)
        {
            IHttp2Stream stream = _connection.Stream(streamId);
            IFullHttpMessage msg = GetMessage(stream);
            if (msg is object)
            {
                OnRstStreamRead(stream, msg);
            }
            ctx.FireExceptionCaught(ThrowHelper.GetStreamError_Http2ToHttpLayerCaughtStreamReset(streamId, errorCode));
        }

        public override void OnPushPromiseRead(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding)
        {
            // A push promise should not be allowed to add headers to an existing stream
            IHttp2Stream promisedStream = _connection.Stream(promisedStreamId);
            if (headers.Status is null)
            {
                // A PUSH_PROMISE frame has no Http response status.
                // https://tools.ietf.org/html/rfc7540#section-8.2.1
                // Server push is semantically equivalent to a server responding to a
                // request; however, in this case, that request is also sent by the
                // server, as a PUSH_PROMISE frame.
                headers.Status = HttpResponseStatus.OK.CodeAsText;
            }
            IFullHttpMessage msg = ProcessHeadersBegin(ctx, promisedStream, headers, false, false, false);
            if (msg is null)
            {
                ThrowHelper.ThrowConnectionError_PushPromiseFrameReceivedForPreExistingStreamId(promisedStreamId);
            }

            msg.Headers.SetInt(HttpConversionUtil.ExtensionHeaderNames.StreamPromiseId, streamId);
            msg.Headers.SetShort(HttpConversionUtil.ExtensionHeaderNames.StreamWeight, Http2CodecUtil.DefaultPriorityWeight);

            ProcessHeadersEnd(ctx, promisedStream, msg, false);
        }

        public override void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings)
        {
            if (_propagateSettings)
            {
                // Provide an interface for non-listeners to capture settings
                ctx.FireChannelRead(settings);
            }
        }

        /// <summary>
        /// Called if a <c>RST_STREAM</c> is received but we have some data for that stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="msg"></param>
        protected virtual void OnRstStreamRead(IHttp2Stream stream, IFullHttpMessage msg)
        {
            RemoveMessage(stream, true);
        }

        private sealed class DefaultImmediateSendDetector : IImmediateSendDetector
        {
            public static readonly DefaultImmediateSendDetector Instance = new DefaultImmediateSendDetector();

            public IFullHttpMessage CopyIfNeeded(IByteBufferAllocator allocator, IFullHttpMessage msg)
            {
                if (msg is IFullHttpRequest request)
                {
                    var copy = (IFullHttpRequest)request.Replace(allocator.Buffer(0));
                    copy.Headers.Remove(HttpHeaderNames.Expect);
                    return copy;
                }
                return null;
            }

            public bool MustSendImmediately(IFullHttpMessage msg)
            {
                switch (msg)
                {
                    case IFullHttpResponse response:
                        return response.Status.CodeClass == HttpStatusClass.Informational;
                    case IFullHttpRequest _:
                        return msg.Headers.Contains(HttpHeaderNames.Expect);
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Allows messages to be sent up the pipeline before the next phase in the
        /// HTTP message flow is detected.
        /// </summary>
        private interface IImmediateSendDetector
        {
            /// <summary>
            /// Determine if the response should be sent immediately, or wait for the end of the stream
            /// </summary>
            /// <param name="msg">The response to test</param>
            /// <returns><c>true</c> if the message should be sent immediately
            /// <para><c>false</c> if we should wait for the end of the stream</para></returns>
            bool MustSendImmediately(IFullHttpMessage msg);

            /// <summary>
            /// Determine if a copy must be made after an immediate send happens.
            /// <para>An example of this use case is if a request is received
            /// with a 'Expect: 100-continue' header. The message will be sent immediately,
            /// and the data will be queued and sent at the end of the stream.</para>
            /// </summary>
            /// <param name="allocator">The <see cref="IByteBufferAllocator"/> that can be used to allocate</param>
            /// <param name="msg">The message which has just been sent due to <see cref="MustSendImmediately(IFullHttpMessage)"/>.</param>
            /// <returns>A modified copy of the <paramref name="msg"/> or <c>null</c> if a copy is not needed.</returns>
            IFullHttpMessage CopyIfNeeded(IByteBufferAllocator allocator, IFullHttpMessage msg);
        }
    }
}
