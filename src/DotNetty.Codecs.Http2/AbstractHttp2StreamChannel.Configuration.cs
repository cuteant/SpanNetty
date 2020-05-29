namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Transport.Channels;

    partial class AbstractHttp2StreamChannel
    {
        /// <summary>
        /// <see cref="IChannelConfiguration"/> so that the high and low writebuffer watermarks can reflect the outbound flow control
        /// window, without having to create a new <c>WriteBufferWaterMark</c> object whenever the flow control window
        /// changes.
        /// </summary>
        sealed class Http2StreamChannelConfiguration : DefaultChannelConfiguration
        {
            public Http2StreamChannelConfiguration(AbstractHttp2StreamChannel channel)
                : base(channel)
            {
            }

            public override IMessageSizeEstimator MessageSizeEstimator
            {
                get => FlowControlledFrameSizeEstimator.Instance;
                set => throw new NotSupportedException();
            }
        }
    }
}
