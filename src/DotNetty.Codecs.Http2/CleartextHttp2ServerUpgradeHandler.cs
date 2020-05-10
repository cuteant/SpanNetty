// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Performing cleartext upgrade, by h2c HTTP upgrade or Prior Knowledge.
    /// This handler config pipeline for h2c upgrade when handler added.
    /// And will update pipeline once it detect the connection is starting HTTP/2 by
    /// prior knowledge or not.
    /// </summary>
    public sealed class CleartextHttp2ServerUpgradeHandler : ChannelHandlerAdapter
    {
        private static readonly IByteBuffer CONNECTION_PREFACE = Unpooled.UnreleasableBuffer(Http2CodecUtil.ConnectionPrefaceBuf());

        private readonly HttpServerCodec httpServerCodec;
        private readonly HttpServerUpgradeHandler httpServerUpgradeHandler;
        private readonly IChannelHandler http2ServerHandler;

        /// <summary>
        /// Creates the channel handler provide cleartext HTTP/2 upgrade from HTTP
        /// upgrade or prior knowledge.
        /// </summary>
        /// <param name="httpServerCodec">the http server codec</param>
        /// <param name="httpServerUpgradeHandler">the http server upgrade handler for HTTP/2</param>
        /// <param name="http2ServerHandler">the http2 server handler, will be added into pipeline
        /// when starting HTTP/2 by prior knowledge</param>
        public CleartextHttp2ServerUpgradeHandler(HttpServerCodec httpServerCodec,
            HttpServerUpgradeHandler httpServerUpgradeHandler, IChannelHandler http2ServerHandler)
        {
            if (httpServerCodec is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.httpServerCodec); }
            if (httpServerUpgradeHandler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.httpServerUpgradeHandler); }
            if (http2ServerHandler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.http2ServerHandler); }

            this.httpServerCodec = httpServerCodec;
            this.httpServerUpgradeHandler = httpServerUpgradeHandler;
            this.http2ServerHandler = http2ServerHandler;
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            ctx.Pipeline
               .AddBefore(ctx.Name, null, new PriorKnowledgeHandler(this))
               .AddBefore(ctx.Name, null, httpServerCodec)
               .Replace(this, null, httpServerUpgradeHandler);
        }

        /// <summary>
        /// Peek inbound message to determine current connection wants to start HTTP/2
        /// by HTTP upgrade or prior knowledge.
        /// </summary>
        private sealed class PriorKnowledgeHandler : ByteToMessageDecoder
        {
            readonly CleartextHttp2ServerUpgradeHandler upgradeHandler;

            public PriorKnowledgeHandler(CleartextHttp2ServerUpgradeHandler upgradeHandler) => this.upgradeHandler = upgradeHandler;

            protected override void Decode(IChannelHandlerContext ctx, IByteBuffer input, List<object> output)
            {
                int prefaceLength = CONNECTION_PREFACE.ReadableBytes;
                int bytesRead = Math.Min(input.ReadableBytes, prefaceLength);

                var pipeline = ctx.Pipeline;
                if (!ByteBufferUtil.Equals(CONNECTION_PREFACE, CONNECTION_PREFACE.ReaderIndex,
                    input, input.ReaderIndex, bytesRead))
                {
                    pipeline.Remove(this);
                }
                else if (bytesRead == prefaceLength)
                {
                    // Full h2 preface match, removed source codec, using http2 codec to handle
                    // following network traffic
                    pipeline
                       .Remove(this.upgradeHandler.httpServerCodec)
                       .Remove(this.upgradeHandler.httpServerUpgradeHandler);

                    pipeline.AddAfter(ctx.Name, null, this.upgradeHandler.http2ServerHandler);
                    pipeline.Remove(this);

                    ctx.FireUserEventTriggered(PriorKnowledgeUpgradeEvent.Instance);
                }
            }
        }
    }
}
