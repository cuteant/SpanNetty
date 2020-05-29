namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Transport.Channels;

    partial class AbstractHttp2StreamChannel
    {
        /// <summary>
        /// Number of bytes to consider non-payload messages. 9 is arbitrary, but also the minimum size of an HTTP/2 frame.
        /// Primarily is non-zero.
        /// </summary>
        private static readonly int MinHttp2FrameSize = 9;

        /// <summary>
        /// Returns the flow-control size for DATA frames, and <see cref="MinHttp2FrameSize"/> for all other frames.
        /// </summary>
        private sealed class FlowControlledFrameSizeEstimator : IMessageSizeEstimator
        {
            public static readonly IMessageSizeEstimator Instance = new FlowControlledFrameSizeEstimator();

            private FlowControlledFrameSizeEstimator() { }

            public IMessageSizeEstimatorHandle NewHandle() => FlowControlledFrameSizeEstimatorHandle.Instance;
        }
        private sealed class FlowControlledFrameSizeEstimatorHandle : IMessageSizeEstimatorHandle
        {
            public static readonly IMessageSizeEstimatorHandle Instance = new FlowControlledFrameSizeEstimatorHandle();

            private FlowControlledFrameSizeEstimatorHandle() { }

            public int Size(object msg)
            {
                return msg is IHttp2DataFrame frame
                    // Guard against overflow.
                    ? (int)Math.Min(int.MaxValue, frame.InitialFlowControlledBytes + (long)MinHttp2FrameSize)
                    : MinHttp2FrameSize;
            }
        }
    }
}
