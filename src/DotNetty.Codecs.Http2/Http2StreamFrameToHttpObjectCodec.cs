// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// This handler converts from <see cref="IHttp2StreamFrame"/> to <see cref="IHttpObject"/>,
    /// and back. It can be used as an adapter in conjunction with <see cref="Http2MultiplexCodec"/>
    /// to make http/2 connections backward-compatible with
    /// <see cref="IChannelHandler"/>s expecting <see cref="IHttpObject"/>
    ///
    /// <para>For simplicity, it converts to chunked encoding unless the entire stream
    /// is a single header.</para>
    /// </summary>
    public class Http2StreamFrameToHttpObjectCodec : MessageToMessageCodec2<IHttp2StreamFrame, IHttpObject>
    {
        private readonly bool isServer;
        private readonly bool validateHeaders;

        private HttpScheme scheme;

        public Http2StreamFrameToHttpObjectCodec(bool isServer)
            : this(isServer, true)
        {
        }

        public Http2StreamFrameToHttpObjectCodec(bool isServer, bool validateHeaders)
        {
            this.isServer = isServer;
            this.validateHeaders = validateHeaders;
            this.scheme = HttpScheme.Http;
        }

        public override bool TryAcceptInboundMessage(object msg, out IHttp2StreamFrame cast)
        {
            switch (msg)
            {
                case IHttp2HeadersFrame headersFrame:
                    cast = headersFrame;
                    return true;

                case IHttp2DataFrame dataFrame:
                    cast = dataFrame;
                    return true;

                default:
                    cast = null;
                    return false;
            }
        }

        protected override void Decode(IChannelHandlerContext ctx, IHttp2StreamFrame msg, List<object> output)
        {
            switch (msg)
            {
                case IHttp2HeadersFrame headersFrame:
                    IHttp2Headers headers = headersFrame.Headers;
                    IHttp2FrameStream stream = headersFrame.Stream;
                    int id = stream == null ? 0 : stream.Id;

                    var status = headers.Status;

                    // 100-continue response is a special case where Http2HeadersFrame#isEndStream=false
                    // but we need to decode it as a FullHttpResponse to play nice with HttpObjectAggregator.
                    if (null != status && HttpResponseStatus.Continue.CodeAsText.ContentEquals(status))
                    {
                        IFullHttpMessage fullMsg = this.NewFullMessage(id, headers, ctx.Allocator);
                        output.Add(fullMsg);
                        return;
                    }

                    if (headersFrame.IsEndStream)
                    {
                        if (headers.Method == null && status == null)
                        {
                            ILastHttpContent last = new DefaultLastHttpContent(Unpooled.Empty, validateHeaders);
                            HttpConversionUtil.AddHttp2ToHttpHeaders(id, headers, last.TrailingHeaders,
                                                                     HttpVersion.Http11, true, true);
                            output.Add(last);
                        }
                        else
                        {
                            IFullHttpMessage full = this.NewFullMessage(id, headers, ctx.Allocator);
                            output.Add(full);
                        }
                    }
                    else
                    {
                        IHttpMessage req = this.NewMessage(id, headers);
                        if (!HttpUtil.IsContentLengthSet(req))
                        {
                            req.Headers.Add(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);
                        }
                        output.Add(req);
                    }
                    break;

                case IHttp2DataFrame dataFrame:
                    if (dataFrame.IsEndStream)
                    {
                        output.Add(new DefaultLastHttpContent((IByteBuffer)dataFrame.Content.Retain(), this.validateHeaders));
                    }
                    else
                    {
                        output.Add(new DefaultHttpContent((IByteBuffer)dataFrame.Content.Retain()));
                    }
                    break;
            }
        }

        private static void EncodeLastContent(ILastHttpContent last, List<object> output, bool validateHeaders)
        {
            var needFiller = !(last is IFullHttpMessage) && last.TrailingHeaders.IsEmpty;
            var content = last.Content;
            if (content.IsReadable() || needFiller)
            {
                output.Add(new DefaultHttp2DataFrame((IByteBuffer)content.Retain(), last.TrailingHeaders.IsEmpty));
            }
            if (!last.TrailingHeaders.IsEmpty)
            {
                var headers = HttpConversionUtil.ToHttp2Headers(last.TrailingHeaders, validateHeaders);
                output.Add(new DefaultHttp2HeadersFrame(headers, true));
            }
        }

        /// <summary>
        /// Encode from an <see cref="IHttpObject"/> to an <see cref="IHttp2StreamFrame"/>. This method will
        /// be called for each written message that can be handled by this encoder.
        /// <para>NOTE: 100-Continue responses that are NOT <see cref="IFullHttpResponse"/> will be rejected.</para>
        /// </summary>
        /// <param name="ctx">the <see cref="IChannelHandlerContext"/> which this handler belongs to</param>
        /// <param name="msg">the <see cref="IHttpObject"/> message to encode</param>
        /// <param name="output">the <see cref="List{Object}"/> into which the encoded msg should be added
        /// needs to do some kind of aggregation</param>
        protected override void Encode(IChannelHandlerContext ctx, IHttpObject msg, List<object> output)
        {
            // 100-continue is typically a FullHttpResponse, but the decoded
            // Http2HeadersFrame should not be marked as endStream=true
            if (msg is IHttpResponse res)
            {
                if (res.Status.Equals(HttpResponseStatus.Continue))
                {
                    if (res is IFullHttpResponse)
                    {
                        var headers = this.ToHttp2Headers(res);
                        output.Add(new DefaultHttp2HeadersFrame(headers, false));
                        return;
                    }
                    ThrowHelper.ThrowEncoderException_ContinueResponseMustBeFullHttpResponse();
                }
            }

            if (msg is IHttpMessage httpMsg)
            {
                var headers = this.ToHttp2Headers(httpMsg);
                var noMoreFrames = false;
                if (msg is IFullHttpMessage fullHttpMsg)
                {
                    noMoreFrames = !fullHttpMsg.Content.IsReadable() && fullHttpMsg.TrailingHeaders.IsEmpty;
                }

                output.Add(new DefaultHttp2HeadersFrame(headers, noMoreFrames));
            }

            if (msg is ILastHttpContent last)
            {
                EncodeLastContent(last, output, this.validateHeaders);
            }
            else if (msg is IHttpContent cont)
            {
                output.Add(new DefaultHttp2DataFrame((IByteBuffer)cont.Content.Retain(), false));
            }
        }

        private IHttp2Headers ToHttp2Headers(IHttpMessage msg)
        {
            if (msg is IHttpRequest)
            {
                msg.Headers.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, scheme.Name);
            }

            return HttpConversionUtil.ToHttp2Headers(msg, this.validateHeaders);
        }

        private IHttpMessage NewMessage(int id, IHttp2Headers headers)
        {
            if (this.isServer)
            {
                return HttpConversionUtil.ToHttpRequest(id, headers, this.validateHeaders);
            }
            else
            {
                return HttpConversionUtil.ToHttpResponse(id, headers, this.validateHeaders);
            }
        }

        private IFullHttpMessage NewFullMessage(int id, IHttp2Headers headers, IByteBufferAllocator alloc)
        {
            if (this.isServer)
            {
                return HttpConversionUtil.ToFullHttpRequest(id, headers, alloc, this.validateHeaders);
            }
            else
            {
                return HttpConversionUtil.ToFullHttpResponse(id, headers, alloc, this.validateHeaders);
            }
        }

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            base.HandlerAdded(context);

            // this handler is typically used on an Http2StreamChannel. at this
            // stage, ssl handshake should've been established. checking for the
            // presence of SslHandler in the parent's channel pipeline to
            // determine the HTTP scheme should suffice, even for the case where
            // SniHandler is used.
            this.scheme = IsSsl(context) ? HttpScheme.Https : HttpScheme.Http;
        }

        protected static bool IsSsl(IChannelHandlerContext ctx)
        {
            var ch = ctx.Channel;
            var connChannel = (ch is IHttp2StreamChannel) ? ch.Parent : ch;
            return null != connChannel.Pipeline.Get<TlsHandler>();
        }
    }
}
