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
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// An listener of HTTP/2 frames.
    /// </summary>
    public interface IHttp2FrameListener
    {
        /// <summary>
        /// Handles an inbound <c>DATA</c> frame.
        /// </summary>
        /// <param name="ctx">the context from the handler where the frame was read.</param>
        /// <param name="streamId">the subject stream for the frame.</param>
        /// <param name="data">payload buffer for the frame. This buffer will be released by the codec.</param>
        /// <param name="padding">additional bytes that should be added to obscure the true content size. Must be between 0 and
        /// 256 (inclusive).</param>
        /// <param name="endOfStream">Indicates whether this is the last frame to be sent from the remote endpoint for this stream.</param>
        /// <returns>the number of bytes that have been processed by the application. The returned bytes are used by the
        /// inbound flow controller to determine the appropriate time to expand the inbound flow control window (i.e. send
        /// <c>WINDOW_UPDATE</c>). Returning a value equal to the length of <paramref name="data"/> + <paramref name="padding"/>
        /// will effectively
        /// opt-out of application-level flow control for this frame.Returning a value less than the length of <paramref name="data"/>
        /// + <paramref name="padding"/> will defer the returning of the processed bytes, which the application must later return via
        /// <see cref="IHttp2LocalFlowController.ConsumeBytes(IHttp2Stream, int)"/>. The returned value must
        /// be >= <c>0</c> and &lt;= <paramref name="data"/>.<see cref="IByteBuffer.ReadableBytes"/> + <paramref name="padding"/>.
        /// </returns>
        int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream);

        /// <summary>
        /// Handles an inbound <c>HEADERS</c> frame.
        /// <para>Only one of the following methods will be called for each <c>HEADERS</c> frame sequence.
        /// One will be called when the <c>END_HEADERS</c> flag has been received.</para>
        /// <para><see cref="OnHeadersRead(IChannelHandlerContext, int, IHttp2Headers, int, bool)"/></para>
        /// <para><see cref="OnHeadersRead(IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool)"/></para>
        /// <para><see cref="OnPushPromiseRead(IChannelHandlerContext, int, int, IHttp2Headers, int)"/></para>
        /// 
        /// To say it another way; the <see cref="IHttp2Headers"/> will contain all of the headers
        /// for the current message exchange step (additional queuing is not necessary).
        /// </summary>
        /// <param name="ctx">the context from the handler where the frame was read.</param>
        /// <param name="streamId">the subject stream for the frame.</param>
        /// <param name="headers">the received headers.</param>
        /// <param name="padding">additional bytes that should be added to obscure the true content size. Must be between 0 and
        /// 256 (inclusive).</param>
        /// <param name="endOfStream">Indicates whether this is the last frame to be sent from the remote endpoint
        /// for this stream.</param>
        void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream);

        /// <summary>
        /// Handles an inbound <c>HEADERS</c> frame with priority information specified.
        /// Only called if <c>END_HEADERS</c> encountered.
        /// <para>Only one of the following methods will be called for each <c>HEADERS</c> frame sequence.
        /// One will be called when the <c>END_HEADERS</c> flag has been received.</para>
        /// <para><see cref="OnHeadersRead(IChannelHandlerContext, int, IHttp2Headers, int, bool)"/></para>
        /// <para><see cref="OnHeadersRead(IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool)"/></para>
        /// <para><see cref="OnPushPromiseRead(IChannelHandlerContext, int, int, IHttp2Headers, int)"/></para>
        /// 
        /// To say it another way; the <see cref="IHttp2Headers"/> will contain all of the headers
        /// for the current message exchange step (additional queuing is not necessary).
        /// </summary>
        /// <param name="ctx">the context from the handler where the frame was read.</param>
        /// <param name="streamId">the subject stream for the frame.</param>
        /// <param name="headers">the received headers.</param>
        /// <param name="streamDependency">the stream on which this stream depends, or 0 if dependent on the connection.</param>
        /// <param name="weight">the new weight for the stream.</param>
        /// <param name="exclusive">whether or not the stream should be the exclusive dependent of its parent.</param>
        /// <param name="padding">additional bytes that should be added to obscure the true content size. Must be between 0 and
        /// 256 (inclusive).</param>
        /// <param name="endOfStream">Indicates whether this is the last frame to be sent from the remote endpoint
        /// for this stream.</param>
        void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers,
            int streamDependency, short weight, bool exclusive, int padding, bool endOfStream);

        /// <summary>
        /// Handles an inbound <c>PRIORITY</c> frame.
        /// 
        /// Note that is it possible to have this method called and no stream object exist for either
        /// <paramref name="streamId"/>, <paramref name="streamDependency"/>, or both. This is because the <c>PRIORITY</c> frame can be
        /// sent/received when streams are in the <c>CLOSED</c> state.
        /// </summary>
        /// <param name="ctx">the context from the handler where the frame was read.</param>
        /// <param name="streamId">the subject stream for the frame.</param>
        /// <param name="streamDependency">the stream on which this stream depends, or 0 if dependent on the connection.</param>
        /// <param name="weight">the new weight for the stream.</param>
        /// <param name="exclusive">whether or not the stream should be the exclusive dependent of its parent.</param>
        void OnPriorityRead(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive);

        /// <summary>
        /// Handles an inbound <c>RST_STREAM</c> frame.
        /// </summary>
        /// <param name="ctx">the context from the handler where the frame was read.</param>
        /// <param name="streamId">the stream that is terminating.</param>
        /// <param name="errorCode">the error code identifying the type of failure.</param>
        void OnRstStreamRead(IChannelHandlerContext ctx, int streamId, Http2Error errorCode);

        /// <summary>
        /// Handles an inbound <c>SETTINGS</c> acknowledgment frame.
        /// </summary>
        /// <param name="ctx">the context from the handler where the frame was read.</param>
        void OnSettingsAckRead(IChannelHandlerContext ctx);

        /// <summary>
        /// Handles an inbound <c>SETTINGS</c> frame.
        /// </summary>
        /// <param name="ctx">the context from the handler where the frame was read.</param>
        /// <param name="settings">the settings received from the remote endpoint.</param>
        void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings);

        /// <summary>
        /// Handles an inbound <c>PING</c> frame.
        /// </summary>
        /// <param name="ctx">the context from the handler where the frame was read.</param>
        /// <param name="data">the payload of the frame.</param>
        void OnPingRead(IChannelHandlerContext ctx, long data);

        /// <summary>
        /// Handles an inbound <c>PING</c> acknowledgment.
        /// </summary>
        /// <param name="ctx">the context from the handler where the frame was read.</param>
        /// <param name="data">the payload of the frame.</param>
        void OnPingAckRead(IChannelHandlerContext ctx, long data);

        /// <summary>
        /// Handles an inbound <c>PUSH_PROMISE</c> frame. Only called if <c>END_HEADERS</c> encountered.
        /// <para>Promised requests MUST be authoritative, cacheable, and safe.
        /// See <a href="https://tools.ietf.org/html/rfc7540#section-8.2">[RFC 7540], Section 8.2</a>.</para>
        /// Only one of the following methods will be called for each <c>HEADERS</c> frame sequence.
        /// One will be called when the <c>END_HEADERS</c> flag has been received.
        /// <para><see cref="OnHeadersRead(IChannelHandlerContext, int, IHttp2Headers, int, bool)"/></para>
        /// <para><see cref="OnHeadersRead(IChannelHandlerContext, int, IHttp2Headers, int, short, bool, int, bool)"/></para>
        /// <para><see cref="OnPushPromiseRead(IChannelHandlerContext, int, int, IHttp2Headers, int)"/></para>
        /// 
        /// To say it another way; the <see cref="IHttp2Headers"/> will contain all of the headers
        /// for the current message exchange step (additional queuing is not necessary).
        /// </summary>
        /// <param name="ctx">the context from the handler where the frame was read.</param>
        /// <param name="streamId">the stream the frame was sent on.</param>
        /// <param name="promisedStreamId">the ID of the promised stream.</param>
        /// <param name="headers">the received headers.</param>
        /// <param name="padding">additional bytes that should be added to obscure the true content size. Must be between 0 and
        /// 256 (inclusive).</param>
        void OnPushPromiseRead(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding);

        /// <summary>
        /// Handles an inbound <c>GO_AWAY</c> frame.
        /// </summary>
        /// <param name="ctx">the context from the handler where the frame was read.</param>
        /// <param name="lastStreamId">the last known stream of the remote endpoint.</param>
        /// <param name="errorCode">the error code, if abnormal closure.</param>
        /// <param name="debugData">application-defined debug data. If this buffer needs to be retained by the
        /// listener they must make a copy.</param>
        void OnGoAwayRead(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData);

        /// <summary>
        /// Handles an inbound <c>WINDOW_UPDATE</c> frame.
        /// </summary>
        /// <param name="ctx">the context from the handler where the frame was read.</param>
        /// <param name="streamId">the stream the frame was sent on.</param>
        /// <param name="windowSizeIncrement">the increased number of bytes of the remote endpoint's flow control window.</param>
        void OnWindowUpdateRead(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement);

        /// <summary>
        /// Handler for a frame not defined by the HTTP/2 spec.
        /// </summary>
        /// <param name="ctx">the context from the handler where the frame was read.</param>
        /// <param name="frameType">the frame type from the HTTP/2 header.</param>
        /// <param name="streamId">the stream the frame was sent on.</param>
        /// <param name="flags">the flags in the frame header.</param>
        /// <param name="payload">the payload of the frame.</param>
        void OnUnknownFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload);
    }
}
