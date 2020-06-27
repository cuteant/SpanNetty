namespace Http2Helloworld.FrameClient
{
    using System;
    using System.Threading;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http2;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Process <see cref="IFullHttpResponse"/> translated from HTTP/2 frames
    /// </summary>
    public class Http2ClientStreamFrameResponseHandler : SimpleChannelInboundHandler2<IHttp2StreamFrame>
    {
        readonly CountdownEvent _latch;

        public Http2ClientStreamFrameResponseHandler()
        {
            _latch = new CountdownEvent(1);
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, IHttp2StreamFrame msg)
        {
            Console.WriteLine("Received HTTP/2 'stream' frame: " + msg);

            // isEndStream() is not from a common interface, so we currently must check both
            if (msg is IHttp2DataFrame dataFrame && dataFrame.IsEndStream)
            {
                _latch.Signal();
            }
            else if (msg is IHttp2HeadersFrame headersFrame && headersFrame.IsEndStream)
            {
                _latch.Signal();
            }
        }

        /**
         * Waits for the latch to be decremented (i.e. for an end of stream message to be received), or for
         * the latch to expire after 5 seconds.
         * @return true if a successful HTTP/2 end of stream message was received.
         */
        public bool ResponseSuccessfullyCompleted()
        {
            return _latch.Wait(TimeSpan.FromSeconds(5));
        }
    }
}
