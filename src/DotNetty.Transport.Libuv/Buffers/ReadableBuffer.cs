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
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Transport.Libuv;

    public readonly struct ReadableBuffer : IDisposable
    {
        internal static readonly ReadableBuffer Empty = new ReadableBuffer(Unpooled.Empty, 0);

        private readonly IByteBuffer _buffer;

        internal ReadableBuffer(IByteBuffer buffer, int count)
        {
            if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }
            if ((uint)count > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count); }

            _buffer = buffer;
            _buffer.SetWriterIndex(_buffer.WriterIndex + count);
        }

        private ReadableBuffer(IByteBuffer buffer)
        {
            if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }

            _buffer = buffer;
        }

        public readonly int Count => _buffer.ReadableBytes;

        public readonly IByteBuffer Buffer => _buffer;

        public ReadableBuffer Retain()
        {
            _buffer.Retain();
            return this;
        }

        public static ReadableBuffer Composite(IEnumerable<ReadableBuffer> buffers)
        {
            if (buffers is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffers); }

            CompositeByteBuffer composite = Unpooled.CompositeBuffer();
            foreach (ReadableBuffer buf in buffers)
            {
                IByteBuffer byteBuffer = buf._buffer;
                if (byteBuffer.IsReadable())
                {
                    composite.AddComponent(byteBuffer);
                }
            }

            return new ReadableBuffer(composite);
        }

        //public string ReadString(Encoding encoding, byte[] separator)
        //{
        //    Contract.Requires(encoding is object);
        //    Contract.Requires(separator is object && separator.Length > 0);

        //    int readableBytes = this.buffer.ReadableBytes;
        //    if (readableBytes == 0)
        //    {
        //        return string.Empty;
        //    }

        //    IByteBuffer buf = Unpooled.WrappedBuffer(separator);
        //    return ByteBufferUtil.ReadString(this.buffer, buf, encoding);
        //}

        public string ReadString(Encoding encoding) => _buffer.ReadString(_buffer.ReadableBytes, encoding);

        public string ReadString(int length, Encoding encoding) => _buffer.ReadString(length, encoding);

        public bool ReadBoolean() => _buffer.ReadBoolean();

        public byte ReadByte() => _buffer.ReadByte();

        public sbyte ReadSByte() => unchecked((sbyte)_buffer.ReadByte());

        public short ReadInt16() => _buffer.ReadShort();

        public short ReadInt16LE() => _buffer.ReadShortLE();

        public ushort ReadUInt16() => _buffer.ReadUnsignedShort();

        public ushort ReadUInt16LE() => _buffer.ReadUnsignedShortLE();

        public int ReadInt24() => _buffer.ReadMedium();

        public int ReadInt24LE() => _buffer.ReadMediumLE();

        public uint ReadUInt24() => unchecked((uint)_buffer.ReadUnsignedMedium());

        public uint ReadUInt24LE() => unchecked((uint)_buffer.ReadUnsignedMediumLE());

        public int ReadInt32() => _buffer.ReadInt();

        public int ReadInt32LE() => _buffer.ReadIntLE();

        public uint ReadUInt32() => _buffer.ReadUnsignedInt();

        public uint ReadUInt32LE() => _buffer.ReadUnsignedIntLE();

        public long ReadInt64() => _buffer.ReadLong();

        public long ReadInt64LE() => _buffer.ReadLongLE();

        public ulong ReadUInt64() => unchecked((ulong)_buffer.ReadLong());

        public ulong ReadUInt64LE() => unchecked((ulong)_buffer.ReadLongLE());

        public float ReadFloat() => _buffer.ReadFloat();

        public float ReadFloatLE() => _buffer.ReadFloatLE();

        public double ReadDouble() => _buffer.ReadDouble();

        public double ReadDoubleLE() => _buffer.ReadDoubleLE();

        public void ReadBytes(byte[] destination) => _buffer.ReadBytes(destination);

        public void ReadBytes(byte[] destination, int length) => _buffer.ReadBytes(destination, 0, length);

        public void Dispose()
        {
            if (_buffer.IsAccessible)
            {
                _buffer.Release();
            }
        }
    }
}
