namespace DotNetty.Transport.Tests.Channel
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using Xunit;

    public class BaseChannelTest
    {
        private readonly LoggingHandler _loggingHandler;

        public BaseChannelTest()
        {
            _loggingHandler = new LoggingHandler();
        }

        internal virtual ServerBootstrap GetLocalServerBootstrap()
        {
            IEventLoopGroup serverGroup = new MultithreadEventLoopGroup();
            ServerBootstrap sb = new ServerBootstrap();
            sb.Group(serverGroup);
            sb.Channel<LocalServerChannel>();
            sb.ChildHandler(new ActionChannelInitializer<LocalChannel>(ch =>
            {
            }));
            return sb;
        }

        internal virtual Bootstrap GetLocalClientBootstrap()
        {
            IEventLoopGroup clientGroup = new MultithreadEventLoopGroup();
            Bootstrap cb = new Bootstrap();
            cb.Channel<LocalChannel>();
            cb.Group(clientGroup);

            cb.Handler(_loggingHandler);

            return cb;
        }

        internal static IByteBuffer CreateTestBuf(int len)
        {
            IByteBuffer buf = Unpooled.Buffer(len, len);
            buf.SetIndex(0, len);
            return buf;
        }

        internal void AssertLog(string firstExpected, params string[] otherExpected)
        {
            string actual = _loggingHandler.GetLog();
            if (string.Equals(firstExpected, actual))
            {
                return;
            }
            for (int i = 0; i < otherExpected.Length; i++)
            {
                if (string.Equals(otherExpected[i], actual))
                {
                    return;
                }
            }

            // Let the comparison fail with the first expectation.
            Assert.Equal(firstExpected, actual);
        }

        internal void ClearLog()
        {
            _loggingHandler.Clear();
        }

        internal void SetInterest(params LoggingHandler.Event[] events)
        {
            _loggingHandler.SetInterest(events);
        }
    }
}