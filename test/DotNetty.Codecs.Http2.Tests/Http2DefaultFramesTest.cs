
namespace DotNetty.Codecs.Http2.Tests
{
    using DotNetty.Buffers;
    using Xunit;

    public class Http2DefaultFramesTest
    {
        [Fact]
        public void TestEqualOperation()
        {
            // in this case, 'goAwayFrame' and 'unknownFrame' will also have an EMPTY_BUFFER data
            // so we want to check that 'dflt' will not consider them equal.
            DefaultHttp2GoAwayFrame goAwayFrame = new DefaultHttp2GoAwayFrame(Http2Error.ProtocolError);
            DefaultHttp2UnknownFrame unknownFrame = new DefaultHttp2UnknownFrame(Http2FrameTypes.Headers, new Http2Flags(1));
            var dflt = new DefaultByteBufferHolder(Unpooled.Empty);
            try
            {
                // not using 'assertNotEquals' to be explicit about which object we are calling .equals() on
                Assert.False(dflt.Equals(goAwayFrame));
                Assert.False(dflt.Equals(unknownFrame));
            }
            finally
            {
                goAwayFrame.Release();
                unknownFrame.Release();
                dflt.Release();
            }
        }
    }
}
