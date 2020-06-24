namespace DotNetty.Transport.Tests.Channel
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class SimpleUserEventChannelHandlerTest
    {
        private readonly FooEventCatcher _fooEventCatcher;
        private readonly AllEventCatcher _allEventCatcher;
        private readonly EmbeddedChannel _channel;

        public SimpleUserEventChannelHandlerTest()
        {
            _fooEventCatcher = new FooEventCatcher();
            _allEventCatcher = new AllEventCatcher();
            _channel = new EmbeddedChannel(_fooEventCatcher, _allEventCatcher);
        }

        [Fact]
        public void TestTypeMatch()
        {
            FooEvent fooEvent = new FooEvent();
            _channel.Pipeline.FireUserEventTriggered(fooEvent);
            Assert.Single(_fooEventCatcher.CaughtEvents);
            Assert.Empty(_allEventCatcher.CaughtEvents);
            Assert.Equal(0, fooEvent.ReferenceCount);
            Assert.False(_channel.Finish());
        }

        [Fact]
        public void TestTypeMismatch()
        {
            BarEvent barEvent = new BarEvent();
            _channel.Pipeline.FireUserEventTriggered(barEvent);
            Assert.Empty(_fooEventCatcher.CaughtEvents);
            Assert.Single(_allEventCatcher.CaughtEvents);
            Assert.True(barEvent.Release());
            Assert.False(_channel.Finish());
        }

        sealed class FooEvent : DefaultByteBufferHolder
        {
            public FooEvent()
                : base(Unpooled.Buffer())
            {
            }
        }

        sealed class BarEvent : DefaultByteBufferHolder
        {
            public BarEvent()
                : base(Unpooled.Buffer())
            {
            }
        }

        class FooEventCatcher : SimpleUserEventChannelHandler<FooEvent>
        {
            public List<FooEvent> CaughtEvents;

            public FooEventCatcher()
            {
                CaughtEvents = new List<FooEvent>();
            }

            protected override void EventReceived(IChannelHandlerContext ctx, FooEvent evt)
            {
                CaughtEvents.Add(evt);
            }
        }

        class AllEventCatcher : ChannelHandlerAdapter
        {
            public readonly List<object> CaughtEvents;

            public AllEventCatcher()
            {
                CaughtEvents = new List<object>();
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                CaughtEvents.Add(evt);
            }
        }
    }
}