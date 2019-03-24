// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    /// <inheritdoc />
    /// <summary>
    ///     Represents an empty byte buffer
    /// </summary>
    public sealed partial class EmptyByteBuffer : IByteBuffer
    {
        public const int EmptyByteBufferHashCode = 1;
        static readonly ArraySegment<byte> EmptyBuffer = new ArraySegment<byte>(ArrayExtensions.ZeroBytes);
        static readonly ArraySegment<byte>[] EmptyBuffers = { EmptyBuffer };

        public EmptyByteBuffer(IByteBufferAllocator allocator)
        {
            if (null == allocator) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.allocator); }

            this.Allocator = allocator;
        }

        public int Capacity => 0;

        public IByteBuffer AdjustCapacity(int newCapacity) => throw new NotSupportedException();

        public int MaxCapacity => 0;

        public IByteBufferAllocator Allocator { get; }

        public IByteBuffer Unwrap() => null;

        public bool IsDirect => true;

        public bool IsReadOnly => false;

        public IByteBuffer AsReadOnly() => Unpooled.UnmodifiableBuffer(this);

        public int ReaderIndex => 0;

        public IByteBuffer SetReaderIndex(int readerIndex) => this.CheckIndex(readerIndex);

        public int WriterIndex => 0;

        public IByteBuffer SetWriterIndex(int writerIndex) => this.CheckIndex(writerIndex);

        public IByteBuffer SetIndex(int readerIndex, int writerIndex)
        {
            this.CheckIndex(readerIndex);
            this.CheckIndex(writerIndex);
            return this;
        }

        public int ReadableBytes => 0;

        public int WritableBytes => 0;

        public int MaxWritableBytes => 0;

        public bool IsWritable() => false;

        public bool IsWritable(int size) => false;

        public IByteBuffer Clear() => this;

        public IByteBuffer MarkReaderIndex() => this;

        public IByteBuffer ResetReaderIndex() => this;

        public IByteBuffer MarkWriterIndex() => this;

        public IByteBuffer ResetWriterIndex() => this;

        public IByteBuffer DiscardReadBytes() => this;

        public IByteBuffer DiscardSomeReadBytes() => this;

        public IByteBuffer EnsureWritable(int minWritableBytes)
        {
            if (minWritableBytes < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(minWritableBytes, ExceptionArgument.minWritableBytes); }

            if (minWritableBytes != 0)
            {
                ThrowHelper.ThrowIndexOutOfRangeException();
            }
            return this;
        }

        public int EnsureWritable(int minWritableBytes, bool force)
        {
            if (minWritableBytes < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(minWritableBytes, ExceptionArgument.minWritableBytes); }

            if (minWritableBytes == 0)
            {
                return 0;
            }

            return 1;
        }

        public bool GetBoolean(int index) => throw new IndexOutOfRangeException();

        public byte GetByte(int index) => throw new IndexOutOfRangeException();

        public short GetShort(int index) => throw new IndexOutOfRangeException();

        public short GetShortLE(int index) => throw new IndexOutOfRangeException();

        public int GetMedium(int index) => throw new IndexOutOfRangeException();

        public int GetMediumLE(int index) => throw new IndexOutOfRangeException();

        public int GetUnsignedMedium(int index) => throw new IndexOutOfRangeException();

        public int GetUnsignedMediumLE(int index) => throw new IndexOutOfRangeException();

        public int GetInt(int index) => throw new IndexOutOfRangeException();

        public int GetIntLE(int index) => throw new IndexOutOfRangeException();

        public long GetLong(int index) => throw new IndexOutOfRangeException();

        public long GetLongLE(int index) => throw new IndexOutOfRangeException();

        public IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length) => this.CheckIndex(index, length);

        public IByteBuffer GetBytes(int index, byte[] destination) => this.CheckIndex(index, destination.Length);

        public IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length) => this.CheckIndex(index, length);

        public IByteBuffer GetBytes(int index, Stream destination, int length) => this.CheckIndex(index, length);

        public ICharSequence GetCharSequence(int index, int length, Encoding encoding)
        {
            this.CheckIndex(index, length);
            return null;
        }

        public string GetString(int index, int length, Encoding encoding)
        {
            this.CheckIndex(index, length);
            return null;
        }

        public IByteBuffer SetBoolean(int index, bool value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetByte(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetShort(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetShortLE(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetMedium(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetMediumLE(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetInt(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetIntLE(int index, int value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetLong(int index, long value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetLongLE(int index, long value) => throw new IndexOutOfRangeException();

        public IByteBuffer SetBytes(int index, IByteBuffer src, int length) => this.CheckIndex(index, length);

        public IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length) => this.CheckIndex(index, length);

        public IByteBuffer SetBytes(int index, byte[] src) => this.CheckIndex(index, src.Length);

        public IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length) => this.CheckIndex(index, length);

        public Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            this.CheckIndex(index, length);
            return TaskUtil.Zero;
        }

        public IByteBuffer SetZero(int index, int length) => this.CheckIndex(index, length);

        public int SetCharSequence(int index, ICharSequence sequence, Encoding encoding) => throw new IndexOutOfRangeException();

        public int SetString(int index, string value, Encoding encoding) => throw new IndexOutOfRangeException();

        public bool ReadBoolean() => throw new IndexOutOfRangeException();

        public byte ReadByte() => throw new IndexOutOfRangeException();

        public short ReadShort() => throw new IndexOutOfRangeException();

        public short ReadShortLE() => throw new IndexOutOfRangeException();

        public int ReadMedium() => throw new IndexOutOfRangeException();

        public int ReadMediumLE() => throw new IndexOutOfRangeException();

        public int ReadUnsignedMedium() => throw new IndexOutOfRangeException();

        public int ReadUnsignedMediumLE() => throw new IndexOutOfRangeException();

        public int ReadInt() => throw new IndexOutOfRangeException();

        public int ReadIntLE() => throw new IndexOutOfRangeException();

        public long ReadLong() => throw new IndexOutOfRangeException();

        public long ReadLongLE() => throw new IndexOutOfRangeException();

        public IByteBuffer ReadBytes(int length) => this.CheckLength(length);

        public IByteBuffer ReadBytes(IByteBuffer destination, int length) => this.CheckLength(length);

        public IByteBuffer ReadBytes(IByteBuffer destination, int dstIndex, int length) => this.CheckLength(length);

        public IByteBuffer ReadBytes(byte[] destination) => this.CheckLength(destination.Length);

        public IByteBuffer ReadBytes(byte[] destination, int dstIndex, int length) => this.CheckLength(length);

        public IByteBuffer ReadBytes(Stream destination, int length) => this.CheckLength(length);

        public ICharSequence ReadCharSequence(int length, Encoding encoding)
        {
            this.CheckLength(length);
            return null;
        }

        public string ReadString(int length, Encoding encoding)
        {
            this.CheckLength(length);
            return null;
        }

        public IByteBuffer SkipBytes(int length) => this.CheckLength(length);

        public IByteBuffer WriteBoolean(bool value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteByte(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteShort(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteShortLE(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteMedium(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteMediumLE(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteUnsignedMedium(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteUnsignedMediumLE(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteInt(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteIntLE(int value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteLong(long value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteLongLE(long value) => throw new IndexOutOfRangeException();

        public IByteBuffer WriteBytes(IByteBuffer src, int length) => this.CheckLength(length);

        public IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length) => this.CheckLength(length);

        public IByteBuffer WriteBytes(byte[] src) => this.CheckLength(src.Length);

        public IByteBuffer WriteBytes(byte[] src, int srcIndex, int length) => this.CheckLength(length);

        public IByteBuffer WriteZero(int length) => this.CheckLength(length);

        public int WriteCharSequence(ICharSequence sequence, Encoding encoding) => throw new IndexOutOfRangeException();

        public int WriteString(string value, Encoding encoding) => throw new IndexOutOfRangeException();

        public int ForEachByte(int index, int length, IByteProcessor processor)
        {
            this.CheckIndex(index, length);
            return -1;
        }

        public int ForEachByteDesc(int index, int length, IByteProcessor processor)
        {
            this.CheckIndex(index, length);
            return -1;
        }

        public IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            return this;
        }

        public IByteBuffer Slice() => this;

        public IByteBuffer RetainedSlice() => this;

        public IByteBuffer Slice(int index, int length) => this.CheckIndex(index, length);

        public IByteBuffer RetainedSlice(int index, int length) => this.CheckIndex(index, length);

        public IByteBuffer Duplicate() => this;

        public int IoBufferCount => 1;

        public ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);
            return EmptyBuffer;
        }

        public ArraySegment<byte>[] GetIoBuffers(int index, int length)
        {
            this.CheckIndex(index, length);
            return EmptyBuffers;
        }

        public bool HasArray => true;

        public byte[] Array => ArrayExtensions.ZeroBytes;

        public byte[] ToArray() => ArrayExtensions.ZeroBytes;

        public int ArrayOffset => 0;

        public bool HasMemoryAddress => false;

        public ref byte GetPinnableMemoryAddress() => throw new NotSupportedException();

        public IntPtr AddressOfPinnedMemory() => IntPtr.Zero;

        public override int GetHashCode() => EmptyByteBufferHashCode;

        public bool Equals(IByteBuffer buffer) => buffer != null && !buffer.IsReadable();

        public override bool Equals(object obj)
        {
            var buffer = obj as IByteBuffer;
            return this.Equals(buffer);
        }

        public int CompareTo(IByteBuffer buffer) => buffer.IsReadable() ? -1 : 0;

        public override string ToString() => string.Empty;

        public bool IsReadable() => false;

        public bool IsReadable(int size) => false;

        public int ReferenceCount => 1;

        public IReferenceCounted Retain() => this;

        public IByteBuffer RetainedDuplicate() => this;

        public IReferenceCounted Retain(int increment) => this;

        public IReferenceCounted Touch() => this;

        public IReferenceCounted Touch(object hint) => this;

        public bool Release() => false;

        public bool Release(int decrement) => false;

        public IByteBuffer ReadSlice(int length) => this.CheckLength(length);

        public IByteBuffer ReadRetainedSlice(int length) => this.CheckLength(length);

        public Task WriteBytesAsync(Stream stream, int length, CancellationToken cancellationToken)
        {
            this.CheckLength(length);
            return TaskUtil.Completed;
        }

        // ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
        IByteBuffer CheckIndex(int index)
        {
            if (index != 0)
            {
                ThrowHelper.ThrowIndexOutOfRangeException();
            }
            return this;
        }

        IByteBuffer CheckIndex(int index, int length)
        {
            if (length < 0)
            {
                ThrowHelper.ThrowArgumentException_CheckIndex(length);
            }
            if (index != 0 || length != 0)
            {
                ThrowHelper.ThrowIndexOutOfRangeException();
            }

            return this;
        }
        // ReSharper restore ParameterOnlyUsedForPreconditionCheck.Local

        IByteBuffer CheckLength(int length)
        {
            if (length < 0)
            {
                ThrowHelper.ThrowArgumentException_CheckLength(length);
            }
            if (length != 0)
            {
                ThrowHelper.ThrowIndexOutOfRangeException();
            }
            return this;
        }
    }
}
