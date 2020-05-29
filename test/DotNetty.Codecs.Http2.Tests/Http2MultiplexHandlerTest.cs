
namespace DotNetty.Codecs.Http2.Tests
{
    using DotNetty.Codecs.Http2;
    using DotNetty.Transport.Channels;

    public class Http2MultiplexHandlerTest : Http2MultiplexTest<Http2FrameCodec>
    {
        protected override Http2FrameCodec NewCodec(TestChannelInitializer childChannelInitializer, IHttp2FrameWriter frameWriter)
        {
            return new Http2FrameCodecBuilder(true).FrameWriter(frameWriter).Build();
        }

        protected override IChannelHandler NewMultiplexer(TestChannelInitializer childChannelInitializer)
        {
            return new Http2MultiplexHandler(childChannelInitializer, null);
        }

        protected override bool UseUserEventForResetFrame()
        {
            return true;
        }

        protected override bool IgnoreWindowUpdateFrames()
        {
            return true;
        }
    }
}
