namespace DotNetty.Codecs.Http2
{
    using DotNetty.Transport.Channels;

    partial class AbstractHttp2StreamChannel
    {
        sealed class Http2ChannelPipeline : DefaultChannelPipeline
        {
            private readonly AbstractHttp2StreamChannel _channel;

            public Http2ChannelPipeline(AbstractHttp2StreamChannel channel)
                : base(channel)
            {
                _channel = channel;
            }

            protected override void IncrementPendingOutboundBytes(long size)
            {
                _channel.IncrementPendingOutboundBytes(size, true);
            }

            protected override void DecrementPendingOutboundBytes(long size)
            {
                _channel.DecrementPendingOutboundBytes(size, true);
            }
        }
    }
}
