using System.Collections.Generic;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;

namespace DotNetty.Codecs
{
    /**
     * A decoder that splits the received {@link ByteBuf}s by the fixed number
     * of bytes. For example, if you received the following four fragmented packets:
     * <pre>
     * +---+----+------+----+
     * | A | BC | DEFG | HI |
     * +---+----+------+----+
     * </pre>
     * A {@link FixedLengthFrameDecoder}{@code (3)} will decode them into the
     * following three packets with the fixed length:
     * <pre>
     * +-----+-----+-----+
     * | ABC | DEF | GHI |
     * +-----+-----+-----+
     * </pre>
     */
    public class FixedLengthFrameDecoder : ByteToMessageDecoder
    {
        private readonly int _frameLength;
        private readonly uint _uframeLength;

        public FixedLengthFrameDecoder(int frameLength)
        {
            if (frameLength <= 0) { ThrowHelper.ThrowArgumentException_Positive(frameLength, ExceptionArgument.frameLength); }
            _frameLength = frameLength;
            _uframeLength = (uint)frameLength;
        }

        protected internal sealed override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            var decoded = Decode(context, input);
            if (decoded is object)
            {
                output.Add(decoded);
            }
        }

        /// <summary>Create a frame out of the <see cref="IByteBuffer"/> and return it.</summary>
        /// <param name="ctx">the <see cref="IChannelHandlerContext"/> which this <see cref="ByteToMessageDecoder"/> belongs to</param>
        /// <param name="input">the <see cref="IByteBuffer"/> from which to read data</param>
        /// <returns>the <see cref="IByteBuffer"/> which represent the frame or <c>null</c> if no frame could be created.</returns>
        protected virtual object Decode(IChannelHandlerContext ctx, IByteBuffer input)
        {
            return (uint)input.ReadableBytes < _uframeLength ? null : input.ReadRetainedSlice(_frameLength);
        }
    }
}
