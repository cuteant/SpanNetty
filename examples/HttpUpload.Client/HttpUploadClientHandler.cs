namespace HttpUpload.Client
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http.Cookies;
    using DotNetty.Codecs.Http.Multipart;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;

    public class HttpUploadClientHandler : SimpleChannelInboundHandler2<IHttpObject>
    {
        static readonly ILogger s_logger = TraceLogger.GetLogger<HttpUploadClientHandler>();

        bool readingChunks;

        protected override void ChannelRead0(IChannelHandlerContext ctx, IHttpObject msg)
        {
            if (msg is IHttpResponse response)
            {
                s_logger.LogInformation("STATUS: " + response.Status);
                s_logger.LogInformation("VERSION: " + response.ProtocolVersion);

                if (!response.Headers.IsEmpty)
                {
                    foreach (var name in response.Headers.Names())
                    {
                        foreach (var value in response.Headers.GetAll(name))
                        {
                            s_logger.LogInformation("HEADER: " + name + " = " + value);
                        }
                    }
                }

                if (response.Status.Code == 200 && HttpUtil.IsTransferEncodingChunked(response))
                {
                    this.readingChunks = true;
                    s_logger.LogInformation("CHUNKED CONTENT {");
                }
                else
                {
                    s_logger.LogInformation("CONTENT {");
                }
            }
            if (msg is IHttpContent chunk)
            {
                s_logger.LogInformation(chunk.Content.ToString(Encoding.UTF8));

                if (chunk is ILastHttpContent)
                {
                    if (this.readingChunks)
                    {
                        s_logger.LogInformation("} END OF CHUNKED CONTENT");
                    }
                    else
                    {
                        s_logger.LogInformation("} END OF CONTENT");
                    }
                    this.readingChunks = false;
                }
                else
                {
                    s_logger.LogInformation(chunk.Content.ToString(Encoding.UTF8));
                }
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            s_logger.LogError(exception.ToString());
            context.CloseAsync();
        }
    }
}
