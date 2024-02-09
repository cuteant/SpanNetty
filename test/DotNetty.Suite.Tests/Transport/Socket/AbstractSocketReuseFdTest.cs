namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit.Abstractions;

    abstract class AbstractSocketReuseFdTest : AbstractSocketTest
    {
        public AbstractSocketReuseFdTest(ITestOutputHelper output)
            : base(output)
        {
        }

        public void TestReuseFd(ServerBootstrap sb, Bootstrap cb)
        {
            sb.ChildOption(ChannelOption.AutoRead, true);
            cb.Option(ChannelOption.AutoRead, true);

            // Use a number which will typically not exceed /proc/sys/net/core/somaxconn (which is 128 on linux by default
            // often).
            int numChannels = 100;
            AtomicReference<Exception> globalException = new AtomicReference<Exception>();
            AtomicInteger serverRemaining = new AtomicInteger(numChannels);
            AtomicInteger clientRemaining = new AtomicInteger(numChannels);
            IPromise serverDonePromise = new DefaultPromise();
            IPromise clientDonePromise = new DefaultPromise();

            sb.ChildHandler(new ActionChannelInitializer<IChannel>(sch =>
            {
                ReuseFdHandler sh = new ReuseFdHandler(
                    false,
                    globalException,
                    serverRemaining,
                    serverDonePromise);
                sch.Pipeline.AddLast("handler", sh);
            }));

            cb.Handler(new ActionChannelInitializer<IChannel>(sch =>
            {
                ReuseFdHandler ch = new ReuseFdHandler(
                    true,
                    globalException,
                    clientRemaining,
                    clientDonePromise);
                sch.Pipeline.AddLast("handler", ch);
            }));

            IChannel sc = sb.BindAsync().GetAwaiter().GetResult();
            for (int i = 0; i < numChannels; i++)
            {
                cb.ConnectAsync(sc.LocalAddress).ContinueWith(t =>
                {
                    if (t.IsFailure())
                    {
                        clientDonePromise.TrySetException(t.Exception);
                    }
                });
            }

            clientDonePromise.Task.GetAwaiter().GetResult();
            serverDonePromise.Task.GetAwaiter().GetResult();
            sc.CloseAsync().GetAwaiter().GetResult();
            if (globalException.Value is object)
            {
                throw globalException.Value;
            }
        }

        sealed class ReuseFdHandler : ChannelHandlerAdapter
        {
            public const string EXPECTED_PAYLOAD = "payload";

            private readonly IPromise _donePromise;
            private readonly AtomicInteger _remaining;
            private readonly bool _client;
            internal volatile IChannel _channel;
            internal readonly AtomicReference<Exception> _globalException;
            internal readonly AtomicReference<Exception> _exception = new AtomicReference<Exception>();
            internal readonly StringBuilder _received = new StringBuilder();

            public ReuseFdHandler(
                bool client,
                AtomicReference<Exception> globalException,
                AtomicInteger remaining,
                IPromise donePromise)
            {
                _client = client;
                _globalException = globalException;
                _remaining = remaining;
                _donePromise = donePromise;
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                _channel = ctx.Channel;
                if (_client)
                {
                    ctx.WriteAndFlushAsync(Unpooled.CopiedBuffer(EXPECTED_PAYLOAD, Encoding.ASCII));
                }
            }

            public override void ChannelRead(IChannelHandlerContext ctx, object msg)
            {
                if (msg is IByteBuffer buf)
                {
                    _received.Append(buf.ToString(Encoding.ASCII));
                    buf.Release();

                    if (_received.ToString().Equals(EXPECTED_PAYLOAD))
                    {
                        if (_client)
                        {
                            ctx.CloseAsync();
                        }
                        else
                        {
                            ctx.WriteAndFlushAsync(Unpooled.CopiedBuffer(EXPECTED_PAYLOAD, Encoding.ASCII));
                        }
                    }
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                if (_exception.CompareAndSet(null, cause))
                {
                    _donePromise.TrySetException(new InvalidOperationException("exceptionCaught: " + ctx.Channel, cause));
                    ctx.CloseAsync();
                }
                _globalException.CompareAndSet(null, cause);
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                if (_remaining.Decrement() == 0)
                {
                    if (_received.ToString().Equals(EXPECTED_PAYLOAD))
                    {
                        _donePromise.Complete();
                    }
                    else
                    {
                        _donePromise.TrySetException(new Exception("Unexpected payload:" + _received));
                    }
                }
            }
        }
    }
}
