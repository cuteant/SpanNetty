//namespace DotNetty.Suite.Tests.Transport.Socket
//{
//    using System;
//    using System.Collections.Concurrent;
//    using System.Collections.Generic;
//    using System.Net;
//    using System.Net.NetworkInformation;
//    using System.Net.Sockets;
//    using DotNetty.Buffers;
//    using DotNetty.Transport.Bootstrapping;
//    using DotNetty.Transport.Channels;
//    using DotNetty.Transport.Channels.Sockets;
//    using Xunit;
//    using Xunit.Abstractions;

//    public class SocketShutdownOutputBySelfTest : AbstractClientSocketTest
//    {
//        public SocketShutdownOutputBySelfTest(ITestOutputHelper output)
//            : base(output)
//        {
//        }

//        private static void CheckThrowable(Exception cause)
//        {
//            if (!(cause is ClosedChannelException) && !(cause is SocketException))
//            {
//                throw cause;
//            }
//        }

//        sealed class TestHandler : SimpleChannelInboundHandler<IByteBuffer>
//        {
//            private readonly ITestOutputHelper _output;
//            internal volatile ISocketChannel _ch;
//            internal BlockingCollection<byte> _queue = new BlockingCollection<byte>();
//            internal BlockingCollection<bool> _writabilityQueue = new BlockingCollection<bool>();
//            private volatile bool _lastWritability;

//            public TestHandler(ITestOutputHelper output)
//            {
//                _output = output;
//            }

//            public override void ChannelWritabilityChanged(IChannelHandlerContext ctx)
//            {
//                _writabilityQueue.Add(ctx.Channel.IsWritable);
//                _lastWritability = ctx.Channel.IsWritable;
//            }

//            public override void ChannelActive(IChannelHandlerContext ctx)
//            {
//                _ch = (ISocketChannel)ctx.Channel;
//            }

//            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
//            {
//                _queue.Add(msg.ReadByte());
//            }

//            private void DrainWritabilityQueue()
//            {
//                while (_writabilityQueue.TryTake(out _, TimeSpan.FromMilliseconds(100)))
//                {
//                    // Just drain the queue.
//                }
//            }

//            internal void AssertWritability(bool isWritable)
//            {
//                try
//                {
//                    var writability = _lastWritability; // _writabilityQueue.takeLast();
//                    Assert.Equal(isWritable, writability);
//                    // TODO(scott): why do we get multiple writability changes here ... race condition?
//                    DrainWritabilityQueue();
//                }
//                catch (Exception exc)
//                {
//                    _output.WriteLine(exc.ToString());
//                }
//            }
//        }
//    }
//}
