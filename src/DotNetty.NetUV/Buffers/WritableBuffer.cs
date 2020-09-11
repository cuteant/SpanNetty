/*
 * Copyright (c) Johnny Z. All rights reserved.
 *
 *   https://github.com/StormHub/NetUV
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com)
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Buffers
{
    using System;
    using System.Text;
    using DotNetty.NetUV;

    public readonly struct WritableBuffer : IDisposable
    {
        private readonly IByteBuffer _buffer;

        internal WritableBuffer(IByteBuffer buffer)
        {
            if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }

            _buffer = buffer;
        }

        public readonly int Count => _buffer.ReadableBytes;

        public WritableBuffer Retain()
        {
            _buffer.Retain();
            return this;
        }

        public static WritableBuffer From(byte[] array)
        {
            if (array is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array); }

            IByteBuffer buf = Unpooled.WrappedBuffer(array);
            return new WritableBuffer(buf);
        }

        public readonly IByteBuffer GetBuffer() => _buffer;

        public void WriteBoolean(bool value) => _buffer.WriteBoolean(value);

        public void WriteByte(byte value) => _buffer.WriteByte(value);

        public void WriteSByte(sbyte value) => _buffer.WriteByte((byte)value);

        public void WriteInt16(short value) => _buffer.WriteShort(value);

        public void WriteInt16LE(short value) => _buffer.WriteShortLE(value);

        public void WriteUInt16(ushort value) => _buffer.WriteUnsignedShort(value);

        public void WriteUInt16LE(ushort value) => _buffer.WriteUnsignedShortLE(value);

        public void WriteInt24(int value) => _buffer.WriteMedium(value);

        public void WriteInt24LE(int value) => _buffer.WriteMediumLE(value);

        public void WriteInt32(int value) => _buffer.WriteInt(value);

        public void WriteInt32LE(int value) => _buffer.WriteIntLE(value);

        public void WriteUInt32(uint value) => _buffer.WriteInt((int)value);

        public void WriteUInt32LE(uint value) => _buffer.WriteIntLE((int)value);

        public void WriteInt64(long value) => _buffer.WriteLong(value);

        public void WriteInt64LE(long value) => _buffer.WriteLongLE(value);

        public void WriteUInt64(ulong value) => _buffer.WriteLong((long)value);

        public void WriteUInt64LE(ulong value) => _buffer.WriteLongLE((long)value);

        public void WriteFloat(float value) => _buffer.WriteFloat(value);

        public void WriteFloatLE(float value) => _buffer.WriteFloatLE(value);

        public void WriteDouble(double value) => _buffer.WriteDouble(value);

        public void WriteDoubleLE(double value) => _buffer.WriteDoubleLE(value);

        public void WriteString(string value, Encoding encoding) => _buffer.WriteString(value, encoding);

        public void Dispose()
        {
            if (_buffer.IsAccessible)
            {
                _buffer.Release();
            }
        }
    }
}
