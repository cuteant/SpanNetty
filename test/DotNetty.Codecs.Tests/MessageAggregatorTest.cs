namespace DotNetty.Codecs.Tests
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class MessageAggregatorTest
    {
        sealed class ReadCounter : ChannelHandlerAdapter
        {
            public int Value;

            public override void Read(IChannelHandlerContext context)
            {
                Value++;
                context.Read();
            }
        }

        class MockMessageAggregator : MessageAggregator<IByteBufferHolder, IByteBufferHolder, IByteBufferHolder, IByteBufferHolder>
        {
            private readonly IByteBufferHolder _first;
            private readonly IByteBufferHolder _chunk;
            private readonly IByteBufferHolder _last;

            public MockMessageAggregator(IByteBufferHolder first, IByteBufferHolder chunk, IByteBufferHolder last)
                : base(1024)
            {
                _first = first;
                _chunk = chunk;
                _last = last;
            }

            protected override IByteBufferHolder BeginAggregation(IByteBufferHolder start, IByteBuffer content)
            {
                return start.Replace(content);
            }

            protected override bool IsStartMessage(IByteBufferHolder msg)
            {
                return msg.Equals(_first);
            }

            protected override bool IsContentMessage(IByteBufferHolder msg)
            {
                return msg.Equals(_chunk) || msg.Equals(_last);
            }

            protected override bool IsLastContentMessage(IByteBufferHolder msg)
            {
                return msg.Equals(_last);
            }

            protected override bool IsAggregated(IByteBufferHolder msg)
            {
                return false;
            }

            protected override bool IsContentLengthInvalid(IByteBufferHolder start, int maxContentLength)
            {
                return false;
            }

            protected override object NewContinueResponse(IByteBufferHolder start, int maxContentLength, IChannelPipeline pipeline)
            {
                return null;
            }

            protected override bool CloseAfterContinueResponse(object msg)
            {
                return false;
            }

            protected override bool IgnoreContentAfterContinueResponse(object msg)
            {
                return false;
            }
        }

        private static IByteBufferHolder Message(string str)
        {
            return new DefaultByteBufferHolder(Unpooled.CopiedBuffer(str, Encoding.ASCII));
        }

        [Fact]
        public void TestReadFlowManagement()
        {
            ReadCounter counter = new ReadCounter();
            IByteBufferHolder first = Message("first");
            IByteBufferHolder chunk = Message("chunk");
            IByteBufferHolder last = Message("last");

            var agg = new MockMessageAggregator(first, chunk, last);
            EmbeddedChannel embedded = new EmbeddedChannel(counter, agg);
            embedded.Configuration.AutoRead = false;

            Assert.False(embedded.WriteInbound(first));
            Assert.False(embedded.WriteInbound(chunk));
            Assert.True(embedded.WriteInbound(last));

            Assert.Equal(3, counter.Value); // 2 reads issued from MockMessageAggregator
                                            // 1 read issued from EmbeddedChannel constructor

            IByteBufferHolder all = new DefaultByteBufferHolder(Unpooled.WrappedBuffer(
                (IByteBuffer)first.Content.Retain(), (IByteBuffer)chunk.Content.Retain(), (IByteBuffer)last.Content.Retain()));
            var output = embedded.ReadInbound<IByteBufferHolder>();

            Assert.Equal(all, output);
            Assert.True(all.Release() && output.Release());
            Assert.False(embedded.Finish());
        }
    }
}
