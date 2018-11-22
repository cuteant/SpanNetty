
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class Http2MultiplexCodecClientUpgradeTest
    {
        sealed class NoopHandler : ChannelHandlerAdapter
        {
            public override bool IsSharable => true;

            public override void ChannelActive(IChannelHandlerContext context)
            {
                context.Channel.CloseAsync();
            }
        }

        sealed class UpgradeHandler : ChannelHandlerAdapter
        {
            internal Http2StreamState stateOnActive;
            internal int streamId;

            public override void ChannelActive(IChannelHandlerContext context)
            {
                var ch = (IHttp2StreamChannel)context.Channel;
                this.stateOnActive = ch.Stream.State;
                this.streamId = ch.Stream.Id;
                base.ChannelActive(context);
            }
        }

        private Http2MultiplexCodec NewCodec(IChannelHandler upgradeHandler)
        {
            Http2MultiplexCodecBuilder builder = Http2MultiplexCodecBuilder.ForClient(new NoopHandler());
            builder.WithUpgradeStreamHandler(upgradeHandler);
            builder.GracefulShutdownTimeout = TimeSpan.Zero;
            return builder.Build();
        }

        [Fact]
        public void UpgradeHandlerGetsActivated()
        {
            UpgradeHandler upgradeHandler = new UpgradeHandler();
            Http2MultiplexCodec codec = this.NewCodec(upgradeHandler);
            EmbeddedChannel ch = new EmbeddedChannel(codec);

            codec.OnHttpClientUpgrade();

            Assert.False(upgradeHandler.stateOnActive.LocalSideOpen);
            Assert.True(upgradeHandler.stateOnActive.RemoteSideOpen);
            Assert.Equal(1, upgradeHandler.streamId);
            Assert.True(ch.FinishAndReleaseAll());
        }

        [Fact]
        public void ClientUpgradeWithoutUpgradeHandlerThrowsHttp2Exception()
        {
            var b = Http2MultiplexCodecBuilder.ForClient(new NoopHandler());
            b.GracefulShutdownTimeout = TimeSpan.Zero;
            Http2MultiplexCodec codec = b.Build();
            EmbeddedChannel ch = new EmbeddedChannel(codec);
            try
            {
                Assert.Throws<Http2Exception>(() => codec.OnHttpClientUpgrade());
            }
            finally
            {
                Assert.True(ch.FinishAndReleaseAll());
            }
        }
    }
}
