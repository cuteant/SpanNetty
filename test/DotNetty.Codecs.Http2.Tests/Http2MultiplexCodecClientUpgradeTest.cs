
namespace DotNetty.Codecs.Http2.Tests
{
    using DotNetty.Transport.Channels;

    public class Http2MultiplexCodecClientUpgradeTest : Http2MultiplexClientUpgradeTest<Http2MultiplexCodec>
    {
        protected override Http2MultiplexCodec NewCodec(IChannelHandler upgradeHandler)
        {
            Http2MultiplexCodecBuilder builder = Http2MultiplexCodecBuilder.ForClient(new NoopHandler());
            if (upgradeHandler != null)
            {
                builder.WithUpgradeStreamHandler(upgradeHandler);
            }
            return builder.Build();
        }

        protected override IChannelHandler NewMultiplexer(IChannelHandler upgradeHandler)
        {
            return null;
        }
    }
}
