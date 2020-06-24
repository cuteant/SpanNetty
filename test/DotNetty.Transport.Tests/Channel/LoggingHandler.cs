namespace DotNetty.Transport.Tests.Channel
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    public class LoggingHandler : ChannelHandlerAdapter
    {
        public enum Event
        {
            WRITE, FLUSH, BIND, CONNECT, DISCONNECT, CLOSE, DEREGISTER, READ, WRITABILITY,
            HANDLER_ADDED, HANDLER_REMOVED, EXCEPTION, READ_COMPLETE, REGISTERED, UNREGISTERED, ACTIVE, INACTIVE,
            USER
        }

        private readonly StringBuilder _log = new StringBuilder();
        private readonly HashSet<Event> _interest = new HashSet<Event>();

        public LoggingHandler()
        {
            _log = new StringBuilder();
            _interest = new HashSet<Event>();
            for (var idx = (int)Event.WRITE; idx <= (int)Event.USER; idx++)
            {
                _interest.Add((Event)idx);
            }
        }

        public override void Write(IChannelHandlerContext context, object message, IPromise promise)
        {
            Log(Event.WRITE);
            context.WriteAsync(message, promise);
        }

        public override void Flush(IChannelHandlerContext context)
        {
            Log(Event.FLUSH);
            context.Flush();
        }

        public override Task BindAsync(IChannelHandlerContext context, EndPoint localAddress)
        {
            Log(Event.BIND, "localAddress=" + localAddress);
            return context.BindAsync(localAddress);
        }

        public override Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress)
        {
            Log(Event.CONNECT, "remoteAddress=" + remoteAddress + " localAddress=" + localAddress);
            return context.ConnectAsync(remoteAddress, localAddress);
        }

        public override void Disconnect(IChannelHandlerContext context, IPromise promise)
        {
            Log(Event.DISCONNECT);
            context.DisconnectAsync(promise);
        }

        public override void Close(IChannelHandlerContext context, IPromise promise)
        {
            Log(Event.CLOSE);
            context.CloseAsync(promise);
        }

        public override void Deregister(IChannelHandlerContext context, IPromise promise)
        {
            Log(Event.DEREGISTER);
            context.DeregisterAsync(promise);
        }

        public override void Read(IChannelHandlerContext context)
        {
            Log(Event.READ);
            context.Read();
        }

        public override void ChannelWritabilityChanged(IChannelHandlerContext context)
        {
            Log(Event.WRITABILITY, "writable=" + context.Channel.IsWritable);
            context.FireChannelWritabilityChanged();
        }

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            Log(Event.HANDLER_ADDED);
        }

        public override void HandlerRemoved(IChannelHandlerContext context)
        {
            Log(Event.HANDLER_REMOVED);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            Log(Event.EXCEPTION, exception.ToString());
        }

        public override void ChannelRegistered(IChannelHandlerContext context)
        {
            Log(Event.REGISTERED);
            context.FireChannelRegistered();
        }

        public override void ChannelUnregistered(IChannelHandlerContext context)
        {
            Log(Event.UNREGISTERED);
            context.FireChannelUnregistered();
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            Log(Event.ACTIVE);
            context.FireChannelActive();
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            Log(Event.INACTIVE);
            context.FireChannelInactive();
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            Log(Event.READ);
            context.FireChannelRead(message);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            Log(Event.READ_COMPLETE);
            context.FireChannelReadComplete();
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            Log(Event.USER, evt.ToString());
            context.FireUserEventTriggered(evt);
        }

        public string GetLog() => _log.ToString();

        public void Clear()
        {
            _log.Clear();
        }

        public void SetInterest(params Event[] events)
        {
            _interest.Clear();
            _interest.UnionWith(events);
        }

        private void Log(Event e)
        {
            Log(e, null);
        }

        private void Log(Event e, string msg)
        {
            if (_interest.Contains(e))
            {
                _log.Append(e);
                if (msg != null)
                {
                    _log.Append(": ").Append(msg);
                }
                _log.Append('\n');
            }
        }
    }
}