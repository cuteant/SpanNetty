
namespace DotNetty.Codecs.Http2.Tests
{
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class Http2MultiplexHandlerClientUpgradeTest : Http2MultiplexClientUpgradeTest<Http2FrameCodec>
    {
        protected override Http2FrameCodec NewCodec(IChannelHandler upgradeHandler)
        {
            return Http2FrameCodecBuilder.ForClient().Build();
        }

        protected override IChannelHandler NewMultiplexer(IChannelHandler upgradeHandler)
        {
            return new Http2MultiplexHandler(new NoopHandler(), upgradeHandler);
        }

        //[Fact]
        //public override void ClientUpgradeWithoutUpgradeHandlerThrowsHttp2Exception()
        //{
        //    var codec = NewCodec(null);
        //    EmbeddedChannel ch = new EmbeddedChannel(codec, NewMultiplexer(null));
        //    try
        //    {
        //        Assert.Throws<Http2Exception>(() => codec.OnHttpClientUpgrade());
        //    }
        //    finally
        //    {
        //        Assert.Throws<Http2Exception>(() => ch.FinishAndReleaseAll());
        //        //Assert.True(ch.FinishAndReleaseAll());
        //    }
        //}
    }
}
