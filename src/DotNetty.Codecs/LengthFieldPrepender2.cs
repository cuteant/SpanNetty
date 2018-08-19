// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    ///     An encoder that prepends the length of the message.  The length value is
    ///     prepended as a binary form.
    ///     <p />
    ///     For example, <tt>{@link LengthFieldPrepender}(2)</tt> will encode the
    ///     following 12-bytes string:
    ///     <pre>
    ///         +----------------+
    ///         | "HELLO, WORLD" |
    ///         +----------------+
    ///     </pre>
    ///     into the following:
    ///     <pre>
    ///         +--------+----------------+
    ///         + 0x000C | "HELLO, WORLD" |
    ///         +--------+----------------+
    ///     </pre>
    ///     If you turned on the {@code lengthIncludesLengthFieldLength} flag in the
    ///     constructor, the encoded data would look like the following
    ///     (12 (original data) + 2 (prepended data) = 14 (0xE)):
    ///     <pre>
    ///         +--------+----------------+
    ///         + 0x000E | "HELLO, WORLD" |
    ///         +--------+----------------+
    ///     </pre>
    /// </summary>
    public class LengthFieldPrepender2 : MessageToByteEncoder2<IByteBuffer>
    {
        readonly int lengthFieldLength;
        readonly bool lengthFieldIncludesLengthFieldLength;
        readonly int lengthAdjustment;

        /// <summary>
        ///     Creates a new <see cref="LengthFieldPrepender2" /> instance.
        /// </summary>
        /// <param name="lengthFieldLength">
        ///     The length of the prepended length field.
        ///     Only 1, 2, 3, 4, and 8 are allowed.
        /// </param>
        public LengthFieldPrepender2(int lengthFieldLength)
            : this(lengthFieldLength, false)
        {
        }

        /// <summary>
        ///     Creates a new <see cref="LengthFieldPrepender2" /> instance.
        /// </summary>
        /// <param name="lengthFieldLength">
        ///     The length of the prepended length field.
        ///     Only 1, 2, 3, 4, and 8 are allowed.
        /// </param>
        /// <param name="lengthFieldIncludesLengthFieldLength">
        ///     If <c>true</c>, the length of the prepended length field is added
        ///     to the value of the prepended length field.
        /// </param>
        public LengthFieldPrepender2(int lengthFieldLength, bool lengthFieldIncludesLengthFieldLength)
            : this(lengthFieldLength, 0, lengthFieldIncludesLengthFieldLength)
        {
        }

        /// <summary>
        ///     Creates a new <see cref="LengthFieldPrepender2" /> instance.
        /// </summary>
        /// <param name="lengthFieldLength">
        ///     The length of the prepended length field.
        ///     Only 1, 2, 3, 4, and 8 are allowed.
        /// </param>
        /// <param name="lengthAdjustment">The compensation value to add to the value of the length field.</param>
        public LengthFieldPrepender2(int lengthFieldLength, int lengthAdjustment)
            : this(lengthFieldLength, lengthAdjustment, false)
        {
        }

        /// <summary>
        ///     Creates a new <see cref="LengthFieldPrepender2" /> instance.
        /// </summary>
        /// <param name="lengthFieldLength">
        ///     The length of the prepended length field.
        ///     Only 1, 2, 3, 4, and 8 are allowed.
        /// </param>
        /// <param name="lengthFieldIncludesLengthFieldLength">
        ///     If <c>true</c>, the length of the prepended length field is added
        ///     to the value of the prepended length field.
        /// </param>
        /// <param name="lengthAdjustment">The compensation value to add to the value of the length field.</param>
        public LengthFieldPrepender2(int lengthFieldLength, int lengthAdjustment, bool lengthFieldIncludesLengthFieldLength)
        {
            if (lengthFieldLength != 1 && lengthFieldLength != 2 && lengthFieldLength != 3 &&
                lengthFieldLength != 4 && lengthFieldLength != 8)
            {
                throw new ArgumentException(
                    "lengthFieldLength must be either 1, 2, 3, 4, or 8: " +
                        lengthFieldLength, nameof(lengthFieldLength));
            }

            this.lengthFieldLength = lengthFieldLength;
            this.lengthFieldIncludesLengthFieldLength = lengthFieldIncludesLengthFieldLength;
            this.lengthAdjustment = lengthAdjustment;
        }

        /// <inheritdoc />
        protected override void Encode(IChannelHandlerContext context, IByteBuffer message, IByteBuffer output)
        {
            int bodyLength = message.ReadableBytes;
            int headerLength = this.lengthFieldLength;

            int length = bodyLength + this.lengthAdjustment;
            if (this.lengthFieldIncludesLengthFieldLength)
            {
                length += headerLength;
            }

            if (length < 0)
            {
                ThrowHelper.ThrowArgumentException_LessThanZero(length);
            }

            switch (headerLength)
            {
                case 1:
                    if (length >= 256)
                    {
                        ThrowHelper.ThrowArgumentException_Byte(length);
                    }
                    output.EnsureWritable(headerLength + bodyLength);
                    output.WriteByte((byte)length);
                    break;
                case 2:
                    if (length >= 65536)
                    {
                        ThrowHelper.ThrowArgumentException_Short(length);
                    }
                    output.EnsureWritable(headerLength + bodyLength);
                    output.WriteShort((short)length);
                    break;
                case 3:
                    if (length >= 16777216)
                    {
                        ThrowHelper.ThrowArgumentException_Medium(length);
                    }
                    output.EnsureWritable(headerLength + bodyLength);
                    output.WriteMedium(length);
                    break;
                case 4:
                    output.EnsureWritable(headerLength + bodyLength);
                    output.WriteInt(length);
                    break;
                case 8:
                    output.EnsureWritable(headerLength + bodyLength);
                    output.WriteLong(length);
                    break;
                default:
                    ThrowHelper.ThrowException_UnknownLen(); break;
            }

            output.WriteBytes(message, message.ReaderIndex, bodyLength);
        }
    }
}
