
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;

    public interface IConsumer<T>
    {
        void Accept(T obj);
    }

    public sealed class NoopConsumer<T> : IConsumer<T>
    {
        public static readonly NoopConsumer<T> Instance = new NoopConsumer<T>();
        private NoopConsumer() { }
        public void Accept(T obj) { }
    }

    /**
     * Channel handler that allows to easily access inbound messages.
     */
    public class LastInboundHandler : ChannelDuplexHandler
    {
        private readonly List<object> _queue = new List<object>();
        private readonly IConsumer<IChannelHandlerContext> _channelReadCompleteConsumer;

        private Exception _lastException;
        private IChannelHandlerContext _ctx;
        private bool _channelActive;
        private string _writabilityStates = "";

        public LastInboundHandler() : this(NoopConsumer<IChannelHandlerContext>.Instance) { }

        public LastInboundHandler(IConsumer<IChannelHandlerContext> channelReadCompleteConsumer)
        {
            _channelReadCompleteConsumer = channelReadCompleteConsumer ?? throw new ArgumentNullException(nameof(channelReadCompleteConsumer));
        }

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            base.HandlerAdded(context);
            _ctx = context;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            if (_channelActive)
            {
                throw new InvalidOperationException("channelActive may only be fired once.");
            }
            _channelActive = true;
            base.ChannelActive(context);
        }

        public bool IsChannelActive => _channelActive;

        public string WritabilityStates => _writabilityStates;

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            if (!_channelActive)
            {
                throw new InvalidOperationException("channelInactive may only be fired once after channelActive.");
            }
            _channelActive = false;
            base.ChannelInactive(context);
        }

        public override void ChannelWritabilityChanged(IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(_writabilityStates))
            {
                _writabilityStates = context.Channel.IsWritable.ToString();
            }
            else
            {
                _writabilityStates += "," + context.Channel.IsWritable;
            }
            base.ChannelWritabilityChanged(context);
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            _queue.Add(message);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            _channelReadCompleteConsumer.Accept(context);
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            _queue.Add(new UserEvent(evt));
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception cause)
        {
            if (_lastException != null)
            {
                //cause.printStackTrace();
            }
            else
            {
                _lastException = cause;
            }
        }

        public T ReadInbound<T>()
        {
            return (T)ReadInbound();
        }

        public object ReadInbound()
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                var item = _queue[i];
                if (!(item is UserEvent))
                {
                    _queue.RemoveAt(i);
                    return item;
                }
            }
            return null;
        }

        public T BlockingReadInbound<T>()
        {
            T msg;
            var sw = new SpinWait();
            while ((msg = ReadInbound<T>()) == null)
            {
                sw.SpinOnce();
            }
            return msg;

        }

        public T ReadUserEvent<T>()
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                var item = _queue[i];
                if (item is UserEvent userEvt)
                {
                    _queue.RemoveAt(i);
                    return (T)userEvt.evt;
                }
            }
            return default;
        }

        /**
         * Useful to test order of events and messages.
         */
        public T ReadInboundMessageOrUserEvent<T>()
        {
            if (_queue.Count <= 0) { return default; }

            var o = _queue[0];
            _queue.RemoveAt(0);
            if (o is UserEvent userEvent)
            {
                return (T)userEvent.evt;
            }
            return (T)o;
        }

        public void WriteOutbound(params object[] msgs)
        {
            if (null == msgs) { return; }
            foreach (var item in msgs)
            {
                _ctx.WriteAsync(item);
            }
            _ctx.Flush();
            EmbeddedChannel ch = (EmbeddedChannel)_ctx.Channel;
            ch.RunPendingTasks();
            ch.CheckException();
            CheckException();
        }

        public void FinishAndReleaseAll()
        {
            CheckException();
            object o;
            while ((o = ReadInboundMessageOrUserEvent<object>()) != null)
            {
                ReferenceCountUtil.Release(o);
            }
        }

        public IChannel Channel => _ctx.Channel;

        public void CheckException()
        {
            if (_lastException == null) { return; }
            var t = _lastException;
            _lastException = null;
            throw t;
        }

        sealed class UserEvent
        {
            internal readonly object evt;

            public UserEvent(object evt)
            {
                this.evt = evt;
            }
        }
    }
}
