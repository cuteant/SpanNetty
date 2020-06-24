namespace DotNetty.Transport.Tests.Channel.Local
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using DotNetty.Transport.Channels.Local;
    using Xunit;

    public class LocalTransportThreadModelTest3 : IDisposable
    {
        enum EventType
        {
            EXCEPTION_CAUGHT,
            USER_EVENT,
            MESSAGE_RECEIVED_LAST,
            INACTIVE,
            ACTIVE,
            UNREGISTERED,
            REGISTERED,
            MESSAGE_RECEIVED,
            WRITE,
            READ
        }

        sealed class ChannelInboundHandlerAdapter0 : ChannelHandlerAdapter
        {
            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                // Discard
                ReferenceCountUtil.Release(message);
            }
        }

        private readonly IEventLoopGroup _group;
        private readonly LocalAddress _localAddr;

        public LocalTransportThreadModelTest3()
        {
            // Configure a test server
            _group = new MultithreadEventLoopGroup(1);
            ServerBootstrap sb = new ServerBootstrap();
            sb.Group(_group)
                .Channel<LocalServerChannel>()
                .ChildHandler(new ActionChannelInitializer<LocalChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new ChannelInboundHandlerAdapter0());
                }));
            _localAddr = (LocalAddress)sb.BindAsync(LocalAddress.Any).GetAwaiter().GetResult().LocalAddress;
        }

        public void Dispose()
        {
            _group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
        }

        public void TestConcurrentAddRemove(bool inbound)
        {

        }

        private static Deque<EventType> Events(bool inbound, int size)
        {
            EventType[] events;
            if (inbound)
            {
                events = new EventType[] {
                    EventType.USER_EVENT, EventType.MESSAGE_RECEIVED, EventType.MESSAGE_RECEIVED_LAST,
                    EventType.EXCEPTION_CAUGHT};
            }
            else
            {
                events = new EventType[] {
                    EventType.READ, EventType.WRITE, EventType.EXCEPTION_CAUGHT };
            }

            Random random = new Random();
            Deque<EventType> expectedEvents = new Deque<EventType>();
            for (int i = 0; i < size; i++)
            {
                expectedEvents.AddToBack(events[random.Next(events.Length)]);
            }
            return expectedEvents;
        }

        sealed class EventForwarder : ChannelDuplexHandler
        {
            public override bool IsSharable => true;
        }

        sealed class EventRecorder : ChannelDuplexHandler
        {
            private readonly Queue<EventType> _events;
            private readonly bool _inbound;

            public EventRecorder(Queue<EventType> events, bool inbound)
            {
                _events = events;
                _inbound = inbound;
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                _events.Enqueue(EventType.EXCEPTION_CAUGHT);
            }

            public override void UserEventTriggered(IChannelHandlerContext context, object evt)
            {
                if (_inbound)
                {
                    _events.Enqueue(EventType.USER_EVENT);
                }
            }

            public override void ChannelReadComplete(IChannelHandlerContext context)
            {
                if (_inbound)
                {
                    _events.Enqueue(EventType.MESSAGE_RECEIVED_LAST);
                }
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                _events.Enqueue(EventType.INACTIVE);
            }

            public override void ChannelActive(IChannelHandlerContext context)
            {
                _events.Enqueue(EventType.ACTIVE);
            }

            public override void ChannelRegistered(IChannelHandlerContext context)
            {
                _events.Enqueue(EventType.REGISTERED);
            }

            public override void ChannelUnregistered(IChannelHandlerContext context)
            {
                _events.Enqueue(EventType.UNREGISTERED);
            }

            public override void ChannelRead(IChannelHandlerContext context, object message)
            {
                if (_inbound)
                {
                    _events.Enqueue(EventType.MESSAGE_RECEIVED);
                }
            }

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                if (!_inbound)
                {
                    _events.Enqueue(EventType.WRITE);
                }
                promise.Complete();
            }

            public override void Read(IChannelHandlerContext context)
            {
                if (!_inbound)
                {
                    _events.Enqueue(EventType.READ);
                }
            }
        }
    }
}