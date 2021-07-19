using DotNetty.Transport.Channels;

namespace DotNetty.Handlers.Proxy.Tests
{
    internal sealed class UnresponsiveHandler : SimpleChannelInboundHandler<object>
    {
        public static readonly UnresponsiveHandler Instance = new UnresponsiveHandler();

        private UnresponsiveHandler()
        {
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
        {
            //Ignore
        }
    }
}