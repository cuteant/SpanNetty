namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Enforce a limit on the maximum number of consecutive empty DATA frames (without end_of_stream flag) that are allowed
    /// before the connection will be closed.
    /// </summary>
    sealed class Http2EmptyDataFrameListener : Http2FrameListenerDecorator
    {
        private readonly int _maxConsecutiveEmptyFrames;

        private bool _violationDetected;
        private int _emptyDataFrames;

        public Http2EmptyDataFrameListener(IHttp2FrameListener listener, int maxConsecutiveEmptyFrames)
            : base(listener)
        {
            if ((uint)(maxConsecutiveEmptyFrames - 1) > SharedConstants.TooBigOrNegative)
            {
                ThrowHelper.ThrowArgumentException_Positive(maxConsecutiveEmptyFrames, ExceptionArgument.maxConsecutiveEmptyFrames);
            }
            _maxConsecutiveEmptyFrames = maxConsecutiveEmptyFrames;
        }

        public override int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
        {
            if (endOfStream || data.IsReadable())
            {
                _emptyDataFrames = 0;
            }
            else if (_emptyDataFrames++ == _maxConsecutiveEmptyFrames && !_violationDetected)
            {
                _violationDetected = true;
                ThrowHelper.ThrowStreamError_Maximum_number_of_empty_data_frames_without_end_of_stream_flag_received(_maxConsecutiveEmptyFrames);
            }

            return base.OnDataRead(ctx, streamId, data, padding, endOfStream);
        }

        public override void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
        {
            _emptyDataFrames = 0;
            base.OnHeadersRead(ctx, streamId, headers, padding, endOfStream);
        }

        public override void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
        {
            _emptyDataFrames = 0;
            base.OnHeadersRead(ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream);
        }
    }
}