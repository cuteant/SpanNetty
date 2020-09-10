namespace DotNetty.Suite.Tests.Transport
{
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;

    public class DefaultEventLoopTest : AbstractSingleThreadEventLoopTest<LocalServerChannel>
    {
        protected override IEventLoopGroup NewEventLoopGroup()
        {
            return new DefaultEventLoopGroup();
        }

        protected override IChannel NewChannel()
        {
            return new LocalChannel();
        }
    }
}