﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    //TODO: format as XML-DOC
    /**
  * A decoder that splits the received {@link ByteBuf}s dynamically by the
  * value of the length field in the message.  It is particularly useful when you
  * decode a binary message which has an integer header field that represents the
  * length of the message body or the whole message.
  * <p />
  * {@link LengthFieldBasedFrameDecoder} has many configuration parameters so
  * that it can decode any message with a length field, which is often seen in
  * proprietary client-server protocols. Here are some example that will give
  * you the basic idea on which option does what.
  *
  * <h3>2 bytes length field at offset 0, do not strip header</h3>
  *
  * The value of the length field in this example is <tt>12 (0x0C)</tt> which
  * represents the length of "HELLO, WORLD".  By default, the decoder assumes
  * that the length field represents the number of the bytes that follows the
  * length field.  Therefore, it can be decoded with the simplistic parameter
  * combination.
  * <pre>
  * <b>lengthFieldOffset</b>   = <b>0</b>
  * <b>lengthFieldLength</b>   = <b>2</b>
  * lengthAdjustment    = 0
  * initialBytesToStrip = 0 (= do not strip header)
  *
  * BEFORE DECODE (14 bytes)         AFTER DECODE (14 bytes)
  * +--------+----------------+      +--------+----------------+
  * | Length | Actual Content |----->| Length | Actual Content |
  * | 0x000C | "HELLO, WORLD" |      | 0x000C | "HELLO, WORLD" |
  * +--------+----------------+      +--------+----------------+
  * </pre>
  *
  * <h3>2 bytes length field at offset 0, strip header</h3>
  *
  * Because we can get the length of the content by calling
  * {@link ByteBuf#readableBytes()}, you might want to strip the length
  * field by specifying <tt>initialBytesToStrip</tt>.  In this example, we
  * specified <tt>2</tt>, that is same with the length of the length field, to
  * strip the first two bytes.
  * <pre>
  * lengthFieldOffset   = 0
  * lengthFieldLength   = 2
  * lengthAdjustment    = 0
  * <b>initialBytesToStrip</b> = <b>2</b> (= the length of the Length field)
  *
  * BEFORE DECODE (14 bytes)         AFTER DECODE (12 bytes)
  * +--------+----------------+      +----------------+
  * | Length | Actual Content |----->| Actual Content |
  * | 0x000C | "HELLO, WORLD" |      | "HELLO, WORLD" |
  * +--------+----------------+      +----------------+
  * </pre>
  *
  * <h3>2 bytes length field at offset 0, do not strip header, the length field
  *     represents the length of the whole message</h3>
  *
  * In most cases, the length field represents the length of the message body
  * only, as shown in the previous examples.  However, in some protocols, the
  * length field represents the length of the whole message, including the
  * message header.  In such a case, we specify a non-zero
  * <tt>lengthAdjustment</tt>.  Because the length value in this example message
  * is always greater than the body length by <tt>2</tt>, we specify <tt>-2</tt>
  * as <tt>lengthAdjustment</tt> for compensation.
  * <pre>
  * lengthFieldOffset   =  0
  * lengthFieldLength   =  2
  * <b>lengthAdjustment</b>    = <b>-2</b> (= the length of the Length field)
  * initialBytesToStrip =  0
  *
  * BEFORE DECODE (14 bytes)         AFTER DECODE (14 bytes)
  * +--------+----------------+      +--------+----------------+
  * | Length | Actual Content |----->| Length | Actual Content |
  * | 0x000E | "HELLO, WORLD" |      | 0x000E | "HELLO, WORLD" |
  * +--------+----------------+      +--------+----------------+
  * </pre>
  *
  * <h3>3 bytes length field at the end of 5 bytes header, do not strip header</h3>
  *
  * The following message is a simple variation of the first example.  An extra
  * header value is prepended to the message.  <tt>lengthAdjustment</tt> is zero
  * again because the decoder always takes the length of the prepended data into
  * account during frame length calculation.
  * <pre>
  * <b>lengthFieldOffset</b>   = <b>2</b> (= the length of Header 1)
  * <b>lengthFieldLength</b>   = <b>3</b>
  * lengthAdjustment    = 0
  * initialBytesToStrip = 0
  *
  * BEFORE DECODE (17 bytes)                      AFTER DECODE (17 bytes)
  * +----------+----------+----------------+      +----------+----------+----------------+
  * | Header 1 |  Length  | Actual Content |----->| Header 1 |  Length  | Actual Content |
  * |  0xCAFE  | 0x00000C | "HELLO, WORLD" |      |  0xCAFE  | 0x00000C | "HELLO, WORLD" |
  * +----------+----------+----------------+      +----------+----------+----------------+
  * </pre>
  *
  * <h3>3 bytes length field at the beginning of 5 bytes header, do not strip header</h3>
  *
  * This is an advanced example that shows the case where there is an extra
  * header between the length field and the message body.  You have to specify a
  * positive <tt>lengthAdjustment</tt> so that the decoder counts the extra
  * header into the frame length calculation.
  * <pre>
  * lengthFieldOffset   = 0
  * lengthFieldLength   = 3
  * <b>lengthAdjustment</b>    = <b>2</b> (= the length of Header 1)
  * initialBytesToStrip = 0
  *
  * BEFORE DECODE (17 bytes)                      AFTER DECODE (17 bytes)
  * +----------+----------+----------------+      +----------+----------+----------------+
  * |  Length  | Header 1 | Actual Content |----->|  Length  | Header 1 | Actual Content |
  * | 0x00000C |  0xCAFE  | "HELLO, WORLD" |      | 0x00000C |  0xCAFE  | "HELLO, WORLD" |
  * +----------+----------+----------------+      +----------+----------+----------------+
  * </pre>
  *
  * <h3>2 bytes length field at offset 1 in the middle of 4 bytes header,
  *     strip the first header field and the length field</h3>
  *
  * This is a combination of all the examples above.  There are the prepended
  * header before the length field and the extra header after the length field.
  * The prepended header affects the <tt>lengthFieldOffset</tt> and the extra
  * header affects the <tt>lengthAdjustment</tt>.  We also specified a non-zero
  * <tt>initialBytesToStrip</tt> to strip the length field and the prepended
  * header from the frame.  If you don't want to strip the prepended header, you
  * could specify <tt>0</tt> for <tt>initialBytesToSkip</tt>.
  * <pre>
  * lengthFieldOffset   = 1 (= the length of HDR1)
  * lengthFieldLength   = 2
  * <b>lengthAdjustment</b>    = <b>1</b> (= the length of HDR2)
  * <b>initialBytesToStrip</b> = <b>3</b> (= the length of HDR1 + LEN)
  *
  * BEFORE DECODE (16 bytes)                       AFTER DECODE (13 bytes)
  * +------+--------+------+----------------+      +------+----------------+
  * | HDR1 | Length | HDR2 | Actual Content |----->| HDR2 | Actual Content |
  * | 0xCA | 0x000C | 0xFE | "HELLO, WORLD" |      | 0xFE | "HELLO, WORLD" |
  * +------+--------+------+----------------+      +------+----------------+
  * </pre>
  *
  * <h3>2 bytes length field at offset 1 in the middle of 4 bytes header,
  *     strip the first header field and the length field, the length field
  *     represents the length of the whole message</h3>
  *
  * Let's give another twist to the previous example.  The only difference from
  * the previous example is that the length field represents the length of the
  * whole message instead of the message body, just like the third example.
  * We have to count the length of HDR1 and Length into <tt>lengthAdjustment</tt>.
  * Please note that we don't need to take the length of HDR2 into account
  * because the length field already includes the whole header length.
  * <pre>
  * lengthFieldOffset   =  1
  * lengthFieldLength   =  2
  * <b>lengthAdjustment</b>    = <b>-3</b> (= the length of HDR1 + LEN, negative)
  * <b>initialBytesToStrip</b> = <b> 3</b>
  *
  * BEFORE DECODE (16 bytes)                       AFTER DECODE (13 bytes)
  * +------+--------+------+----------------+      +------+----------------+
  * | HDR1 | Length | HDR2 | Actual Content |----->| HDR2 | Actual Content |
  * | 0xCA | 0x0010 | 0xFE | "HELLO, WORLD" |      | 0xFE | "HELLO, WORLD" |
  * +------+--------+------+----------------+      +------+----------------+
  * </pre>
  * @see LengthFieldPrepender
  */

    public class LengthFieldBasedFrameDecoder : ByteToMessageDecoder
    {
        readonly ByteOrder byteOrder;
        readonly int maxFrameLength;
        readonly int lengthFieldOffset;
        readonly int lengthFieldLength;
        readonly int lengthFieldEndOffset;
        readonly int lengthAdjustment;
        readonly int initialBytesToStrip;
        readonly bool failFast;
        bool discardingTooLongFrame;
        long tooLongFrameLength;
        long bytesToDiscard;

        /// <summary>
        ///     Create a new instance.
        /// </summary>
        /// <param name="maxFrameLength">
        ///     The maximum length of the frame.  If the length of the frame is
        ///     greater than this value then <see cref="TooLongFrameException" /> will be thrown.
        /// </param>
        /// <param name="lengthFieldOffset">The offset of the length field.</param>
        /// <param name="lengthFieldLength">The length of the length field.</param>
        public LengthFieldBasedFrameDecoder(int maxFrameLength, int lengthFieldOffset, int lengthFieldLength)
            : this(maxFrameLength, lengthFieldOffset, lengthFieldLength, 0, 0)
        {
        }

        /// <summary>
        ///     Create a new instance.
        /// </summary>
        /// <param name="maxFrameLength">
        ///     The maximum length of the frame.  If the length of the frame is
        ///     greater than this value then <see cref="TooLongFrameException" /> will be thrown.
        /// </param>
        /// <param name="lengthFieldOffset">The offset of the length field.</param>
        /// <param name="lengthFieldLength">The length of the length field.</param>
        /// <param name="lengthAdjustment">The compensation value to add to the value of the length field.</param>
        /// <param name="initialBytesToStrip">the number of first bytes to strip out from the decoded frame.</param>
        public LengthFieldBasedFrameDecoder(int maxFrameLength, int lengthFieldOffset, int lengthFieldLength, int lengthAdjustment, int initialBytesToStrip)
            : this(maxFrameLength, lengthFieldOffset, lengthFieldLength, lengthAdjustment, initialBytesToStrip, true)
        {
        }

        /// <summary>
        ///     Create a new instance.
        /// </summary>
        /// <param name="maxFrameLength">
        ///     The maximum length of the frame.  If the length of the frame is
        ///     greater than this value then <see cref="TooLongFrameException" /> will be thrown.
        /// </param>
        /// <param name="lengthFieldOffset">The offset of the length field.</param>
        /// <param name="lengthFieldLength">The length of the length field.</param>
        /// <param name="lengthAdjustment">The compensation value to add to the value of the length field.</param>
        /// <param name="initialBytesToStrip">the number of first bytes to strip out from the decoded frame.</param>
        /// <param name="failFast">
        ///     If <c>true</c>, a <see cref="TooLongFrameException" /> is thrown as soon as the decoder notices the length
        ///     of the frame will exceeed <see cref="maxFrameLength" /> regardless of whether the entire frame has been
        ///     read. If <c>false</c>, a <see cref="TooLongFrameException" /> is thrown after the entire frame that exceeds
        ///     <see cref="maxFrameLength" /> has been read.
        ///     Defaults to <c>true</c> in other overloads.
        /// </param>
        public LengthFieldBasedFrameDecoder(int maxFrameLength, int lengthFieldOffset, int lengthFieldLength, int lengthAdjustment, int initialBytesToStrip, bool failFast)
            : this(ByteOrder.BigEndian, maxFrameLength, lengthFieldOffset, lengthFieldLength, lengthAdjustment, initialBytesToStrip, failFast)
        {
        }

        /// <summary>
        ///     Create a new instance.
        /// </summary>
        /// <param name="byteOrder">The <see cref="ByteOrder" /> of the lenght field.</param>
        /// <param name="maxFrameLength">
        ///     The maximum length of the frame.  If the length of the frame is
        ///     greater than this value then <see cref="TooLongFrameException" /> will be thrown.
        /// </param>
        /// <param name="lengthFieldOffset">The offset of the length field.</param>
        /// <param name="lengthFieldLength">The length of the length field.</param>
        /// <param name="lengthAdjustment">The compensation value to add to the value of the length field.</param>
        /// <param name="initialBytesToStrip">the number of first bytes to strip out from the decoded frame.</param>
        /// <param name="failFast">
        ///     If <c>true</c>, a <see cref="TooLongFrameException" /> is thrown as soon as the decoder notices the length
        ///     of the frame will exceeed <see cref="maxFrameLength" /> regardless of whether the entire frame has been
        ///     read. If <c>false</c>, a <see cref="TooLongFrameException" /> is thrown after the entire frame that exceeds
        ///     <see cref="maxFrameLength" /> has been read.
        ///     Defaults to <c>true</c> in other overloads.
        /// </param>
        public LengthFieldBasedFrameDecoder(ByteOrder byteOrder, int maxFrameLength, int lengthFieldOffset, int lengthFieldLength, int lengthAdjustment, int initialBytesToStrip, bool failFast)
        {
            if (maxFrameLength <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFrameLength), "maxFrameLength must be a positive integer: " + maxFrameLength);
            }
            if (lengthFieldOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lengthFieldOffset), "lengthFieldOffset must be a non-negative integer: " + lengthFieldOffset);
            }
            if (initialBytesToStrip < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialBytesToStrip), "initialBytesToStrip must be a non-negative integer: " + initialBytesToStrip);
            }
            if (lengthFieldOffset > maxFrameLength - lengthFieldLength)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFrameLength), "maxFrameLength (" + maxFrameLength + ") " +
                    "must be equal to or greater than " +
                    "lengthFieldOffset (" + lengthFieldOffset + ") + " +
                    "lengthFieldLength (" + lengthFieldLength + ").");
            }

            this.byteOrder = byteOrder;
            this.maxFrameLength = maxFrameLength;
            this.lengthFieldOffset = lengthFieldOffset;
            this.lengthFieldLength = lengthFieldLength;
            this.lengthAdjustment = lengthAdjustment;
            this.lengthFieldEndOffset = lengthFieldOffset + lengthFieldLength;
            this.initialBytesToStrip = initialBytesToStrip;
            this.failFast = failFast;
        }

        protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            object decoded = this.Decode(context, input);
            if (decoded != null)
            {
                output.Add(decoded);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DiscardingTooLongFrame(IByteBuffer input)
        {
            long bytesToDiscard = this.bytesToDiscard;
            int localBytesToDiscard = (int)Math.Min(bytesToDiscard, input.ReadableBytes);
            input.SkipBytes(localBytesToDiscard);
            bytesToDiscard -= localBytesToDiscard;
            this.bytesToDiscard = bytesToDiscard;

            this.FailIfNecessary(false);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FailOnNegativeLengthField(IByteBuffer input, long frameLength, int lengthFieldEndOffset)
        {
            input.SkipBytes(lengthFieldEndOffset);
            CThrowHelper.ThrowCorruptedFrameException_FrameLength(frameLength);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FailOnFrameLengthLessThanLengthFieldEndOffset(IByteBuffer input,
                                                                          long frameLength,
                                                                          int lengthFieldEndOffset)
        {
            input.SkipBytes(lengthFieldEndOffset);
            CThrowHelper.ThrowCorruptedFrameException_LengthFieldEndOffset(frameLength, lengthFieldEndOffset);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ExceededFrameLength(IByteBuffer input, long frameLength)
        {
            long discard = frameLength - input.ReadableBytes;
            this.tooLongFrameLength = frameLength;

            if (discard < 0)
            {
                // buffer contains more bytes then the frameLength so we can discard all now
                input.SkipBytes((int)frameLength);
            }
            else
            {
                // Enter the discard mode and discard everything received so far.
                this.discardingTooLongFrame = true;
                this.bytesToDiscard = discard;
                input.SkipBytes(input.ReadableBytes);
            }
            this.FailIfNecessary(true);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FailOnFrameLengthLessThanInitialBytesToStrip(IByteBuffer input,
                                                                         int frameLength,
                                                                         int initialBytesToStrip)
        {
            input.SkipBytes(frameLength);
            CThrowHelper.ThrowCorruptedFrameException_InitialBytesToStrip(frameLength, initialBytesToStrip);
        }

        /// <summary>
        ///     Create a frame out of the <see cref="IByteBuffer" /> and return it.
        /// </summary>
        /// <param name="context">
        ///     The <see cref="IChannelHandlerContext" /> which this <see cref="ByteToMessageDecoder" /> belongs
        ///     to.
        /// </param>
        /// <param name="input">The <see cref="IByteBuffer" /> from which to read data.</param>
        /// <returns>The <see cref="IByteBuffer" /> which represents the frame or <c>null</c> if no frame could be created.</returns>
        protected virtual object Decode(IChannelHandlerContext context, IByteBuffer input)
        {
            if (this.discardingTooLongFrame)
            {
                this.DiscardingTooLongFrame(input);
            }

            var thisLengthFieldEndOffset = this.lengthFieldEndOffset;
            if (input.ReadableBytes < thisLengthFieldEndOffset)
            {
                return null;
            }

            int actualLengthFieldOffset = input.ReaderIndex + this.lengthFieldOffset;
            long frameLength = GetUnadjustedFrameLength(input, actualLengthFieldOffset, this.lengthFieldLength, this.byteOrder);

            if (frameLength < 0)
            {
                FailOnNegativeLengthField(input, frameLength, thisLengthFieldEndOffset);
            }

            frameLength += this.lengthAdjustment + thisLengthFieldEndOffset;

            if (frameLength < thisLengthFieldEndOffset)
            {
                FailOnFrameLengthLessThanLengthFieldEndOffset(input, frameLength, thisLengthFieldEndOffset);
            }

            if (frameLength > this.maxFrameLength)
            {
                this.ExceededFrameLength(input, frameLength);
                return null;
            }

            // never overflows because it's less than maxFrameLength
            int frameLengthInt = (int)frameLength;
            if (input.ReadableBytes < frameLengthInt)
            {
                return null;
            }

            var thisInitialBytesToStrip = this.initialBytesToStrip;
            if (thisInitialBytesToStrip > frameLengthInt)
            {
                FailOnFrameLengthLessThanInitialBytesToStrip(input, frameLengthInt, thisInitialBytesToStrip);
            }
            input.SkipBytes(thisInitialBytesToStrip);

            // extract frame
            int readerIndex = input.ReaderIndex;
            int actualFrameLength = frameLengthInt - thisInitialBytesToStrip;
            IByteBuffer frame = this.ExtractFrame(context, input, readerIndex, actualFrameLength);
            input.SetReaderIndex(readerIndex + actualFrameLength);
            return frame;
        }

        /// <summary>
        ///     Decodes the specified region of the buffer into an unadjusted frame length.  The default implementation is
        ///     capable of decoding the specified region into an unsigned 8/16/24/32/64 bit integer.  Override this method to
        ///     decode the length field encoded differently.
        ///     Note that this method must not modify the state of the specified buffer (e.g.
        ///     <see cref="IByteBuffer.ReaderIndex" />,
        ///     <see cref="IByteBuffer.WriterIndex" />, and the content of the buffer.)
        /// </summary>
        /// <param name="buffer">The buffer we'll be extracting the frame length from.</param>
        /// <param name="offset">The offset from the absolute <see cref="IByteBuffer.ReaderIndex" />.</param>
        /// <param name="length">The length of the framelenght field. Expected: 1, 2, 3, 4, or 8.</param>
        /// <param name="order">The preferred <see cref="ByteOrder" /> of buffer.</param>
        /// <returns>A long integer that represents the unadjusted length of the next frame.</returns>
        protected static long GetUnadjustedFrameLength(IByteBuffer buffer, int offset, int length, ByteOrder order)
        {
            switch (length)
            {
                case 1:
                    return buffer.GetByte(offset);
                case 2:
                    return order == ByteOrder.BigEndian ? buffer.GetUnsignedShort(offset) : buffer.GetUnsignedShortLE(offset);
                case 3:
                    return order == ByteOrder.BigEndian ? buffer.GetUnsignedMedium(offset) : buffer.GetUnsignedMediumLE(offset);
                case 4:
                    return order == ByteOrder.BigEndian ? buffer.GetInt(offset) : buffer.GetIntLE(offset);
                case 8:
                    return order == ByteOrder.BigEndian ? buffer.GetLong(offset) : buffer.GetLongLE(offset);
                default:
                    return CThrowHelper.ThrowDecoderException(length);
            }
        }

        protected virtual IByteBuffer ExtractFrame(IChannelHandlerContext context, IByteBuffer buffer, int index, int length)
        {
            return buffer.RetainedSlice(index, length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void FailIfNecessary(bool firstDetectionOfTooLongFrame)
        {
            if (this.bytesToDiscard == 0)
            {
                // Reset to the initial state and tell the handlers that
                // the frame was too large.
                long tooLongFrameLength = this.tooLongFrameLength;
                this.tooLongFrameLength = 0;
                this.discardingTooLongFrame = false;
                if (!this.failFast || firstDetectionOfTooLongFrame)
                {
                    if (tooLongFrameLength > 0)
                    {
                        Fail(tooLongFrameLength, this.maxFrameLength);
                    }
                    else
                    {
                        Fail(this.maxFrameLength);
                    }
                }
            }
            else
            {
                // Keep discarding and notify handlers if necessary.
                if (this.failFast && firstDetectionOfTooLongFrame)
                {
                    if (tooLongFrameLength > 0)
                    {
                        Fail(tooLongFrameLength, this.maxFrameLength);
                    }
                    else
                    {
                        Fail(this.maxFrameLength);
                    }
                }
            }
        }

        private static void Fail(long frameLength, int maxFrameLength)
        {
            throw GetTooLongFrameException();
            TooLongFrameException GetTooLongFrameException()
            {
                return new TooLongFrameException("Adjusted frame length exceeds " + maxFrameLength +
                    ": " + frameLength + " - discarded");
            }
        }

        private static void Fail(int maxFrameLength)
        {
            throw GetTooLongFrameException();
            TooLongFrameException GetTooLongFrameException()
            {
                return new TooLongFrameException(
                    "Adjusted frame length exceeds " + maxFrameLength +
                        " - discarding");
            }
        }
    }
}