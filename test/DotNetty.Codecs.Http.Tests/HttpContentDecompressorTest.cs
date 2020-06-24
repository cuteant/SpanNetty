namespace DotNetty.Codecs.Http.Tests
{
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public sealed class HttpContentDecompressorTest
    {
        [Fact]
        public void TestInvokeReadWhenNotProduceMessage()
        {
            var readCalled = new AtomicInteger();
            EmbeddedChannel channel = new EmbeddedChannel(new TestHandler(readCalled), new HttpContentDecompressor(), new DecompressorHandler());

            channel.Configuration.IsAutoRead = false;

            readCalled.Value = 0;
            IHttpResponse response = new DefaultHttpResponse(HttpVersion.Http11, HttpResponseStatus.OK);
            response.Headers.Set(HttpHeaderNames.ContentEncoding, "gzip");
            response.Headers.Set(HttpHeaderNames.ContentType, "application/json;charset=UTF-8");
            response.Headers.Set(HttpHeaderNames.TransferEncoding, HttpHeaderValues.Chunked);

            Assert.True(channel.WriteInbound(response));

            // we triggered read explicitly
            Assert.Equal(1, readCalled.Value);

            Assert.True(channel.ReadInbound() is IHttpResponse);

            Assert.False(channel.WriteInbound(new DefaultHttpContent(Unpooled.Empty)));

            // read was triggered by the HttpContentDecompressor itself as it did not produce any message to the next
            // inbound handler.
            Assert.Equal(2, readCalled.Value);
            Assert.False(channel.FinishAndReleaseAll());
        }

        class TestHandler : ChannelHandlerAdapter
        {
            private readonly AtomicInteger _readCalled;

            public TestHandler(AtomicInteger readCalled)
            {
                _readCalled = readCalled;
            }

            public override void Read(IChannelHandlerContext context)
            {
                _readCalled.Increment();
                context.Read();
            }
        }

        class DecompressorHandler : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                context.FireChannelRead(message);
                context.Read();
            }
        }
    }
}
