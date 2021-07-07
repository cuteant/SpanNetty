namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;
    using Xunit;
    using Xunit.Abstractions;

    public class SocketStringEchoTest : AbstractSocketTest
    {
        static readonly Random s_random;
        static readonly string[] s_data;

        static SocketStringEchoTest()
        {
            DotNetty.Common.ResourceLeakDetector.Level = Common.ResourceLeakDetector.DetectionLevel.Disabled;
            s_random = new Random();
            s_data = new string[1024];
            for (int i = 0; i < s_data.Length; i++)
            {
                int eLen = s_random.Next(512);
                char[] e = new char[eLen];
                for (int j = 0; j < eLen; j++)
                {
                    e[j] = (char)('a' + s_random.Next(26));
                }

                s_data[i] = new string(e);
            }
        }

        public SocketStringEchoTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestStringEcho(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestStringEcho0(sb, cb);
        }

        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestStringEcho_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestStringEcho0(sb, cb);
        }

        [Theory()]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestStringEcho_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestStringEcho0(sb, cb);
        }

        [Theory()]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestStringEcho_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestStringEcho0(sb, cb);
        }

        private async Task TestStringEcho0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestStringEcho0(sb, cb, true, Output);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }


        [Theory]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestStringEchoNotAutoRead(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestStringEchoNotAutoRead0(sb, cb);
        }

        [Theory()]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestStringEchoNotAutoRead_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = DefaultServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestStringEchoNotAutoRead0(sb, cb);
        }

        [Theory()]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestStringEchoNotAutoRead_LibuvServer_SocketClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = DefaultClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestStringEchoNotAutoRead0(sb, cb);
        }

        [Theory()]
        [MemberData(nameof(GetAllocators), DisableDiscoveryEnumeration = true)]
        public Task TestStringEchoNotAutoRead_LibuvServer_LibuvClient(IByteBufferAllocator allocator)
        {
            var sb = LibuvServerBootstrapFactory.Instance.NewInstance();
            var cb = LibuvClientBootstrapFactory.Instance.NewInstance();
            Configure(sb, cb, allocator);

            return TestStringEchoNotAutoRead0(sb, cb);
        }

        private async Task TestStringEchoNotAutoRead0(ServerBootstrap sb, Bootstrap cb)
        {
            try
            {
                await TestStringEcho0(sb, cb, false, Output);
            }
            finally
            {
                Task.WaitAll(
                    sb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    sb.ChildGroup().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)),
                    cb.Group().ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)));
            }
        }

        private static async Task TestStringEcho0(ServerBootstrap sb, Bootstrap cb, bool autoRead, ITestOutputHelper output)
        {
            sb.ChildOption(ChannelOption.AutoRead, autoRead);
            cb.Option(ChannelOption.AutoRead, autoRead);

            IPromise serverDonePromise = new DefaultPromise();
            IPromise clientDonePromise = new DefaultPromise();
            StringEchoHandler sh = new StringEchoHandler(autoRead, serverDonePromise, output);
            StringEchoHandler ch = new StringEchoHandler(autoRead, clientDonePromise, output);

            sb.ChildHandler(new ActionChannelInitializer<IChannel>(sch =>
            {
                sch.Pipeline.AddLast("framer", new DelimiterBasedFrameDecoder(512, Delimiters.LineDelimiter()));
                sch.Pipeline.AddLast("decoder", new StringDecoder(Encoding.ASCII));
                sch.Pipeline.AddBefore("decoder", "encoder", new StringEncoder(Encoding.ASCII));
                sch.Pipeline.AddAfter("decoder", "handler", sh);
            }));

            cb.Handler(new ActionChannelInitializer<IChannel>(sch =>
            {
                sch.Pipeline.AddLast("framer", new DelimiterBasedFrameDecoder(512, Delimiters.LineDelimiter()));
                sch.Pipeline.AddLast("decoder", new StringDecoder(Encoding.ASCII));
                sch.Pipeline.AddBefore("decoder", "encoder", new StringEncoder(Encoding.ASCII));
                sch.Pipeline.AddAfter("decoder", "handler", ch);
            }));

            IChannel sc = await sb.BindAsync();
            IChannel cc = await cb.ConnectAsync(sc.LocalAddress);
            for (int i = 0; i < s_data.Length; i++)
            {
                string element = s_data[i];
                string delimiter = s_random.Next(0, 1) == 1 ? "\r\n" : "\n";
                await cc.WriteAndFlushAsync(element + delimiter);
            }

            await ch._donePromise.Task;
            await sh._donePromise.Task;
            await sh._channel.CloseAsync();
            await ch._channel.CloseAsync();
            await sc.CloseAsync();

            if (sh._exception.Value != null && !(sh._exception.Value is SocketException || (sh._exception.Value is ChannelException chexc && chexc.InnerException is OperationException) || sh._exception.Value is OperationException))
            {
                throw sh._exception.Value;
            }
            if (ch._exception.Value != null && !(ch._exception.Value is SocketException || (sh._exception.Value is ChannelException chexc1 && chexc1.InnerException is OperationException) || sh._exception.Value is OperationException))
            {
                throw ch._exception.Value;
            }
            if (sh._exception.Value != null)
            {
                throw sh._exception.Value;
            }
            if (ch._exception.Value != null)
            {
                throw ch._exception.Value;
            }
        }

        sealed class StringEchoHandler : SimpleChannelInboundHandler<string>
        {
            internal readonly IPromise _donePromise;
            internal readonly AtomicReference<Exception> _exception = new AtomicReference<Exception>();
            private readonly ITestOutputHelper _output;
            private readonly bool _autoRead;

            private int _dataIndex;
            internal volatile IChannel _channel;

            public StringEchoHandler(bool autoRead, IPromise donePromise, ITestOutputHelper output)
            {
                _autoRead = autoRead;
                _donePromise = donePromise;
                _output = output;
            }

            public override void ChannelActive(IChannelHandlerContext ctx)
            {
                _channel = ctx.Channel;
                if (!_autoRead)
                {
                    ctx.Read();
                }
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, string msg)
            {
                if (!s_data[_dataIndex].Equals(msg))
                {
                    _donePromise.TrySetException(new InvalidOperationException("index: " + _dataIndex + " didn't match!"));
                    ctx.CloseAsync();
                    return;
                }

                if (_channel.Parent != null)
                {
                    string delimiter = s_random.Next(0, 1) == 1 ? "\r\n" : "\n";
                    _channel.WriteAsync(msg + delimiter);
                }

                if (++_dataIndex >= s_data.Length)
                {
                    _donePromise.Complete();
                }
            }

            public override void ChannelReadComplete(IChannelHandlerContext ctx)
            {
                try
                {
                    ctx.Flush();
                }
                finally
                {
                    if (!_autoRead)
                    {
                        ctx.Read();
                    }
                }
            }

            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                if (_exception.CompareAndSet(null, cause))
                {
                    _output.WriteLine(cause.ToString());
                    _donePromise.TrySetException(new InvalidOperationException("exceptionCaught: " + ctx.Channel, cause));
                    ctx.CloseAsync();
                }
            }

            public override void ChannelInactive(IChannelHandlerContext ctx)
            {
                _donePromise.TrySetException(new InvalidOperationException("channelInactive: " + ctx.Channel));
            }
        }
    }
}
