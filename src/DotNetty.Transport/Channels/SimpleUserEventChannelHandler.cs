namespace DotNetty.Transport.Channels
{
    using DotNetty.Common.Utilities;

    public abstract class SimpleUserEventChannelHandler<I> : ChannelHandlerAdapter
    {
        readonly bool _autoRelease;

        protected SimpleUserEventChannelHandler()
            : this(true)
        {
        }

        protected SimpleUserEventChannelHandler(bool autoRelease)
        {
            _autoRelease = autoRelease;
        }

        public bool AcceptEvent(object msg) => msg is I;

        public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
        {
            bool release = true;
            try
            {
                if (AcceptEvent(evt))
                {
                    I ievt = (I)evt;
                    EventReceived(ctx, ievt);
                }
                else
                {
                    release = false;
                    _ = ctx.FireUserEventTriggered(evt);
                }
            }
            finally
            {
                if (_autoRelease && release)
                {
                    _ = ReferenceCountUtil.Release(evt);
                }
            }
        }

        protected abstract void EventReceived(IChannelHandlerContext ctx, I evt);
    }
}
