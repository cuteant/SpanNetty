
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
        private readonly List<object> queue = new List<object>();
        private readonly IConsumer<IChannelHandlerContext> channelReadCompleteConsumer;

        private Exception lastException;
        private IChannelHandlerContext ctx;
        private bool channelActive;
        private string writabilityStates = "";

        public LastInboundHandler() : this(NoopConsumer<IChannelHandlerContext>.Instance) { }

        public LastInboundHandler(IConsumer<IChannelHandlerContext> channelReadCompleteConsumer)
        {
            this.channelReadCompleteConsumer = channelReadCompleteConsumer ?? throw new ArgumentNullException(nameof(channelReadCompleteConsumer));
        }

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            base.HandlerAdded(context);
            this.ctx = context;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            if (this.channelActive)
            {
                throw new InvalidOperationException("channelActive may only be fired once.");
            }
            this.channelActive = true;
            base.ChannelActive(context);
        }

        public bool IsChannelActive => this.channelActive;

        public string WritabilityStates => this.writabilityStates;

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            if (!this.channelActive)
            {
                throw new InvalidOperationException("channelInactive may only be fired once after channelActive.");
            }
            this.channelActive = false;
            base.ChannelInactive(context);
        }

        public override void ChannelWritabilityChanged(IChannelHandlerContext context)
        {
            if (string.IsNullOrEmpty(this.writabilityStates))
            {
                this.writabilityStates = context.Channel.IsWritable.ToString();
            }
            else
            {
                this.writabilityStates += "," + context.Channel.IsWritable;
            }
            base.ChannelWritabilityChanged(context);
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            this.queue.Add(message);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            this.channelReadCompleteConsumer.Accept(context);
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            this.queue.Add(new UserEvent(evt));
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception cause)
        {
            if (lastException != null)
            {
                //cause.printStackTrace();
            }
            else
            {
                this.lastException = cause;
            }
        }

        public T ReadInbound<T>()
        {
            for (int i = 0; i < this.queue.Count; i++)
            {
                var item = this.queue[i];
                if (!(item is UserEvent))
                {
                    this.queue.RemoveAt(i);
                    return (T)item;
                }
            }
            return default;
        }

        public T BlockingReadInbound<T>()
        {
            T msg;
            var sw = new SpinWait();
            while ((msg = this.ReadInbound<T>()) == null)
            {
                sw.SpinOnce();
            }
            return msg;

        }

        public T ReadUserEvent<T>()
        {
            for (int i = 0; i < this.queue.Count; i++)
            {
                var item = this.queue[i];
                if (item is UserEvent userEvt)
                {
                    this.queue.RemoveAt(i);
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
            if (this.queue.Count <= 0) { return default; }

            var o = this.queue[0];
            this.queue.RemoveAt(0);
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
                this.ctx.WriteAsync(item);
            }
            this.ctx.Flush();
            EmbeddedChannel ch = (EmbeddedChannel)this.ctx.Channel;
            ch.RunPendingTasks();
            ch.CheckException();
            this.CheckException();
        }

        public void FinishAndReleaseAll()
        {
            this.CheckException();
            object o;
            while ((o = this.ReadInboundMessageOrUserEvent<object>()) != null)
            {
                ReferenceCountUtil.Release(o);
            }
        }

        public IChannel Channel => this.ctx.Channel;

        public void CheckException()
        {
            if (this.lastException == null) { return; }
            var t = this.lastException;
            this.lastException = null;
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
