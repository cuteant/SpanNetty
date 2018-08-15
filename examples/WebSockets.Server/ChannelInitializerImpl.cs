using System.IO;
using System.Security.Cryptography.X509Certificates;
using DotNetty.Codecs.Http;
using DotNetty.Handlers.Logging;
using DotNetty.Handlers.Timeout;
using DotNetty.Handlers.Tls;
using DotNetty.Transport.Channels;
using Examples.Common;

namespace WebSockets.Server
{
    internal sealed class ChannelInitializerImpl : IChannelInitializer<IChannel>
    {
        public static readonly IChannelInitializer<IChannel> Instance = new ChannelInitializerImpl();

        void IChannelInitializer<IChannel>.InitChannel(IChannel channel) => InitChannel(channel);

        private static readonly X509Certificate2 s_tlsCertificate;

        static ChannelInitializerImpl()
        {
            s_tlsCertificate = new X509Certificate2(Path.Combine(ExampleHelper.ProcessDirectory, "dotnetty.com.pfx"), "password");
        }


        private static void InitChannel(IChannel channel)
        {
            IChannelPipeline pipeline = channel.Pipeline;
            if (ServerSettings.IsSsl)
            {
                pipeline.AddLast(TlsHandler.Server(s_tlsCertificate));
            }

            pipeline.AddLast("idleStateHandler", new IdleStateHandler(20, 0, 0));

            pipeline.AddLast(new MsLoggingHandler("CONN"));
            pipeline.AddLast(new HttpServerCodec());
            pipeline.AddLast(new HttpObjectAggregator(65536));
            pipeline.AddLast(new WebSocketServerHandler());
        }
    }
}
