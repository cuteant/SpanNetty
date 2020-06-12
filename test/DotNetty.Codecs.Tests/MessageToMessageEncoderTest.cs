namespace DotNetty.Codecs.Tests
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Codecs;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;
    using Xunit;

    public class MessageToMessageEncoderTest
    {
        sealed class ThrowExceptionMessageToMessageEncoder : MessageToMessageEncoder<object>
        {
            protected internal override void Encode(IChannelHandlerContext context, object message, List<object> output)
            {
                throw new Exception();
            }
        }

        [Fact]
        public void TestException()
        {
            EmbeddedChannel channel = new EmbeddedChannel(new ThrowExceptionMessageToMessageEncoder());

            try
            {
                channel.WriteOutbound(new object());
                Assert.False(true);
            }
            catch (Exception exc)
            {
                if (exc is AggregateException) { exc = exc.InnerException;  }
                Assert.IsType<EncoderException>(exc);
            }
        }

        [Fact]
        public void TestIntermediateWriteFailures()
        {
            var encoder = new TestMessageToMessageEncoder();
            Exception firstWriteException = new Exception();

            EmbeddedChannel channel = new EmbeddedChannel(new WriteThrower(firstWriteException), encoder);
            object msg = new object();
            var write = channel.WriteAndFlushAsync(msg);
            Assert.Same(firstWriteException, write.Exception.InnerException);
            Assert.Same(msg, channel.ReadOutbound<object>());
            Assert.False(channel.Finish());
        }

        sealed class TestMessageToMessageEncoder : MessageToMessageEncoder<object>
        {
            protected internal override void Encode(IChannelHandlerContext context, object message, List<object> output)
            {
                output.Add(new object());
                output.Add(message);
            }
        }

        sealed class WriteThrower : ChannelHandlerAdapter
        {
            private readonly Exception _firstWriteException;
            private bool _firstWritten;

            public WriteThrower(Exception firstWriteException) => _firstWriteException = firstWriteException;

            public override void Write(IChannelHandlerContext context, object message, IPromise promise)
            {
                if (_firstWritten)
                {
                    context.WriteAsync(message, promise);
                }
                else
                {
                    _firstWritten = true;
                    promise.SetException(_firstWriteException);

                }
            }
        }
    }
}
