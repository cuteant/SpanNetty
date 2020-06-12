
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public abstract class Http2MultiplexClientUpgradeTest<TCodec>
        where TCodec : Http2FrameCodec
    {
        internal sealed class NoopHandler : ChannelHandlerAdapter
        {
            public override bool IsSharable => true;

            public override void ChannelActive(IChannelHandlerContext context)
            {
                context.Channel.CloseAsync();
            }
        }

        internal sealed class UpgradeHandler : ChannelHandlerAdapter
        {
            internal Http2StreamState _stateOnActive;
            internal int _streamId;
            internal bool _channelInactiveCalled;

            public override void ChannelActive(IChannelHandlerContext context)
            {
                var ch = (IHttp2StreamChannel)context.Channel;
                _stateOnActive = ch.Stream.State;
                _streamId = ch.Stream.Id;
                base.ChannelActive(context);
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                _channelInactiveCalled = true;
                base.ChannelInactive(context);
            }
        }

        protected abstract TCodec NewCodec(IChannelHandler upgradeHandler);

        protected abstract IChannelHandler NewMultiplexer(IChannelHandler upgradeHandler);

        [Fact]
        public void UpgradeHandlerGetsActivated()
        {
            UpgradeHandler upgradeHandler = new UpgradeHandler();
            var codec = NewCodec(upgradeHandler);
            EmbeddedChannel ch = new EmbeddedChannel(codec, NewMultiplexer(upgradeHandler));

            codec.OnHttpClientUpgrade();

            Assert.False(upgradeHandler._stateOnActive.LocalSideOpen());
            Assert.True(upgradeHandler._stateOnActive.RemoteSideOpen());
            Assert.NotNull(codec.Connection.Stream(Http2CodecUtil.HttpUpgradeStreamId).GetProperty<object>(codec._streamKey));
            Assert.Equal(Http2CodecUtil.HttpUpgradeStreamId, upgradeHandler._streamId);
            Assert.True(ch.FinishAndReleaseAll());
            Assert.True(upgradeHandler._channelInactiveCalled);
        }

        [Fact]
        public virtual void ClientUpgradeWithoutUpgradeHandlerThrowsHttp2Exception()
        {
            var codec = NewCodec(null);
            EmbeddedChannel ch = new EmbeddedChannel(codec, NewMultiplexer(null));
            try
            {
                try
                {
                    codec.OnHttpClientUpgrade(); // Http2MultiplexCodec 触发
                }
                finally
                {
                    Assert.True(ch.FinishAndReleaseAll()); // Http2MultiplexHandler 触发
                }
            }
            catch (Exception exc)
            {
                Assert.IsType<Http2Exception>(exc);
            }
        }
    }
}
