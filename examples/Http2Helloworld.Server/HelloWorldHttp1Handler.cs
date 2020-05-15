// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Http2Helloworld.Server
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// HTTP handler that responds with a "Hello World"
    /// </summary>
    public class HelloWorldHttp1Handler : SimpleChannelInboundHandler2<IFullHttpRequest>
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<HelloWorldHttp1Handler>();

        internal static readonly IByteBuffer RESPONSE_BYTES = Unpooled.UnreleasableBuffer(Unpooled.CopiedBuffer("Hello World", Encoding.UTF8));

        readonly string establishApproach;

        public HelloWorldHttp1Handler(string establishApproach)
        {
            this.establishApproach = establishApproach ?? throw new ArgumentNullException(nameof(establishApproach));
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, IFullHttpRequest req)
        {
            if (HttpUtil.Is100ContinueExpected(req))
            {
                ctx.WriteAsync(new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.Continue));
            }
            var keepAlive = HttpUtil.IsKeepAlive(req);

            var content = ctx.Allocator.Buffer();
            content.WriteBytes(RESPONSE_BYTES.Duplicate());
            ByteBufferUtil.WriteAscii(content, " - via " + req.ProtocolVersion + " (" + this.establishApproach + ")");

            IFullHttpResponse response = new DefaultFullHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK, content);
            response.Headers.Set(HttpHeaderNames.ContentType, "text/plain; charset=UTF-8");
            response.Headers.SetInt(HttpHeaderNames.ContentLength, response.Content.ReadableBytes);

            if (!keepAlive)
            {
                ctx.WriteAsync(response)
                   .ContinueWith(t => ctx.Channel.CloseAsync(), TaskContinuationOptions.ExecuteSynchronously);
            }
            else
            {
                response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.KeepAlive);
                ctx.WriteAsync(response);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            context.Flush();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            s_logger.LogError(exception.ToString());
            context.CloseAsync();
        }
    }
}
