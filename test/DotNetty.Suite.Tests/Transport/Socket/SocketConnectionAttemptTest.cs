//namespace DotNetty.Suite.Tests.Transport.Socket
//{
//    using System;
//    using System.Net;
//    using System.Net.Sockets;
//    using System.Threading;
//    using System.Threading.Tasks;
//    using DotNetty.Buffers;
//    using DotNetty.Common.Utilities;
//    using DotNetty.Transport.Bootstrapping;
//    using DotNetty.Transport.Channels;
//    using Xunit;
//    using Xunit.Abstractions;

//    public class SocketConnectionAttemptTest : AbstractClientSocketTest
//    {
//        public SocketConnectionAttemptTest(ITestOutputHelper output)
//            : base(output)
//        {
//        }

//        public void testConnectCancellation(Bootstrap cb)
//        {
//            cb.Handler(new TestHandler(Output)).Option(ChannelOption.ConnectTimeout, TimeSpan.FromSeconds(4));
//            var future = cb.ConnectAsync(new IPEndPoint(IPAddress.Parse("198.51.100.254"), 65535));
//            try
//            {
//                if (future.Wait(1000))
//                {
//                    if (future.IsSuccess())
//                    {
//                        Assert.False(true, "A connection attempt to " + "198.51.100.254" + " must not succeed.");
//                    }
//                    else
//                    {
//                        throw future.Exception.InnerException;
//                    }
//                }

//                if (future.cancel(true))
//                {
//                    assertThat(future.channel().closeFuture().await(500), is (true));
//                    assertThat(future.isCancelled(), is (true));
//                }
//                else
//                {
//                    // Cancellation not supported by the transport.
//                }
//            }
//            finally
//            {
//                future.channel().close();
//            }
//        }

//        sealed class TestHandler : ChannelHandlerAdapter
//        {
//            private readonly ITestOutputHelper _output;

//            public TestHandler(ITestOutputHelper output)
//            {
//                _output = output;
//            }

//            public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
//            {
//                _output.WriteLine($"Unexpected exception: {cause}");
//            }
//        }
//    }
//}
