
namespace DotNetty.Codecs.Http2.Tests
{
    using DotNetty.Codecs.Http2;
    using DotNetty.Transport.Channels;

    public class Http2MultiplexCodecTest : Http2MultiplexTest<Http2FrameCodec>
    {
        protected override Http2FrameCodec NewCodec(TestChannelInitializer childChannelInitializer, IHttp2FrameWriter frameWriter)
        {
            return new Http2MultiplexCodecBuilder(true, childChannelInitializer).FrameWriter(frameWriter).Build();
        }

        protected override IChannelHandler NewMultiplexer(TestChannelInitializer childChannelInitializer)
        {
            return null;
        }

        protected override bool UseUserEventForResetFrame()
        {
            return false;
        }

        protected override bool IgnoreWindowUpdateFrames()
        {
            return false;
        }
    }
}
