using System;
using DotNetty.Transport.Channels;

namespace DotNetty.Handlers.Proxy.Tests
{
    internal sealed class UnresponsiveHandler : SimpleChannelInboundHandler<object>
    {
        public static readonly UnresponsiveHandler Instance = new UnresponsiveHandler();

        private UnresponsiveHandler()
        {
        }

        public override bool IsSharable => true;

        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            base.ChannelInactive(context);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            base.ExceptionCaught(context, exception);
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
        {
            //Ignore
        }
    }
}