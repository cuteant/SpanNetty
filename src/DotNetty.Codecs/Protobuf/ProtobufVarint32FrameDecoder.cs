// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Protobuf
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    ///
    /// A decoder that splits the received {@link ByteBuf}s dynamically by the
    /// value of the Google Protocol Buffers
    /// http://code.google.com/apis/protocolbuffers/docs/encoding.html#varints
    /// Base 128 Varints integer length field in the message. 
    /// For example:
    /// 
    /// BEFORE DECODE (302 bytes)       AFTER DECODE (300 bytes)
    /// +--------+---------------+      +---------------+
    /// | Length | Protobuf Data |----->| Protobuf Data |
    /// | 0xAC02 |  (300 bytes)  |      |  (300 bytes)  |
    /// +--------+---------------+      +---------------+
    ///
    public sealed class ProtobufVarint32FrameDecoder : ByteToMessageDecoder
    {
        // todo: maxFrameLength + safe skip + fail-fast option (just like LengthFieldBasedFrameDecoder)

        protected internal override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            _ = input.MarkReaderIndex();

            int preIndex = input.ReaderIndex;
            int length = ReadRawVarint32(input);

            if (0u >= (uint)(preIndex - input.ReaderIndex))
            {
                return;
            }

            uint uLen = (uint)length;
            if (uLen > SharedConstants.TooBigOrNegative)
            {
                ThrowCorruptedFrameException_Negative_length(length);
            }

            if ((uint)input.ReadableBytes >= uLen)
            {
                IByteBuffer byteBuffer = input.ReadSlice(length);
                output.Add(byteBuffer.Retain());
            }
            else
            {
                _ = input.ResetReaderIndex();
            }
        }

        private static int ReadRawVarint32(IByteBuffer buffer)
        {
            Debug.Assert(buffer is object);

            if (!buffer.IsReadable())
            {
                return 0;
            }

            _ = buffer.MarkReaderIndex();
            byte rawByte = buffer.ReadByte();
            if (rawByte < 128u)
            {
                return rawByte;
            }

            int result = rawByte & 127;
            if (!buffer.IsReadable())
            {
                _ = buffer.ResetReaderIndex();
                return 0;
            }

            rawByte = buffer.ReadByte();
            if (rawByte < 128u)
            {
                result |= rawByte << 7;
            }
            else
            {
                result |= (rawByte & 127) << 7;
                if (!buffer.IsReadable())
                {
                    _ = buffer.ResetReaderIndex();
                    return 0;
                }

                rawByte = buffer.ReadByte();
                if (rawByte < 128u)
                {
                    result |= rawByte << 14;
                }
                else
                {
                    result |= (rawByte & 127) << 14;
                    if (!buffer.IsReadable())
                    {
                        _ = buffer.ResetReaderIndex();
                        return 0;
                    }

                    rawByte = buffer.ReadByte();
                    if (rawByte < 128u)
                    {
                        result |= rawByte << 21;
                    }
                    else
                    {
                        result |= (rawByte & 127) << 21;
                        if (!buffer.IsReadable())
                        {
                            _ = buffer.ResetReaderIndex();
                            return 0;
                        }

                        rawByte = buffer.ReadByte();
                        result |= rawByte << 28;

                        if (rawByte >= 128u)
                        {
                            ThrowCorruptedFrameException_Malformed_varint();
                        }
                    }
                }
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowCorruptedFrameException_Malformed_varint()
        {
            throw GetException();
            static CorruptedFrameException GetException()
            {
                return new CorruptedFrameException("Malformed varint.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowCorruptedFrameException_Negative_length(int length)
        {
            throw GetException();
            CorruptedFrameException GetException()
            {
                return new CorruptedFrameException($"Negative length: {length}");
            }
        }
    }
}
