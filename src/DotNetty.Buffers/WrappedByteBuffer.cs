// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    /// Wraps another <see cref="IByteBuffer"/>.
    /// 
    /// It's important that the {@link #readerIndex()} and {@link #writerIndex()} will not do any adjustments on the
    /// indices on the fly because of internal optimizations made by {@link ByteBufUtil#writeAscii(ByteBuf, CharSequence)}
    /// and {@link ByteBufUtil#writeUtf8(ByteBuf, CharSequence)}.
    public abstract partial class WrappedByteBuffer : IByteBuffer
    {
        internal protected readonly IByteBuffer Buf;

        protected WrappedByteBuffer(IByteBuffer buf)
        {
            if (buf is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buf); }

            Buf = buf;
        }

        public bool HasMemoryAddress => Buf.HasMemoryAddress;

        public bool IsContiguous => Buf.IsContiguous;

        public ref byte GetPinnableMemoryAddress() => ref Buf.GetPinnableMemoryAddress();

        public IntPtr AddressOfPinnedMemory() => Buf.AddressOfPinnedMemory();

        public int Capacity => Buf.Capacity;

        public virtual IByteBuffer AdjustCapacity(int newCapacity)
        {
            Buf.AdjustCapacity(newCapacity);
            return this;
        }

        public int MaxCapacity => Buf.MaxCapacity;

        public IByteBufferAllocator Allocator => Buf.Allocator;

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public IByteBuffer Unwrap() => Buf;

        public bool IsReadOnly => Buf.IsReadOnly;

        public virtual IByteBuffer AsReadOnly() => Buf.AsReadOnly();

        public bool IsDirect => Buf.IsDirect;

        public int ReaderIndex => Buf.ReaderIndex;

        public IByteBuffer SetReaderIndex(int readerIndex)
        {
            Buf.SetReaderIndex(readerIndex);
            return this;
        }

        public int WriterIndex => Buf.WriterIndex;

        public IByteBuffer SetWriterIndex(int writerIndex)
        {
            Buf.SetWriterIndex(writerIndex);
            return this;
        }

        public virtual IByteBuffer SetIndex(int readerIndex, int writerIndex)
        {
            Buf.SetIndex(readerIndex, writerIndex);
            return this;
        }

        public int ReadableBytes => Buf.ReadableBytes;

        public int WritableBytes => Buf.WritableBytes;

        public int MaxWritableBytes => Buf.MaxWritableBytes;

        public int MaxFastWritableBytes => Buf.MaxFastWritableBytes;

        public bool IsReadable() => Buf.IsReadable();

        public bool IsWritable() => Buf.IsWritable();

        public IByteBuffer Clear()
        {
            Buf.Clear();
            return this;
        }

        public IByteBuffer MarkReaderIndex()
        {
            Buf.MarkReaderIndex();
            return this;
        }

        public IByteBuffer ResetReaderIndex()
        {
            Buf.ResetReaderIndex();
            return this;
        }

        public IByteBuffer MarkWriterIndex()
        {
            Buf.MarkWriterIndex();
            return this;
        }

        public IByteBuffer ResetWriterIndex()
        {
            Buf.ResetWriterIndex();
            return this;
        }

        public virtual IByteBuffer DiscardReadBytes()
        {
            Buf.DiscardReadBytes();
            return this;
        }

        public virtual IByteBuffer DiscardSomeReadBytes()
        {
            Buf.DiscardSomeReadBytes();
            return this;
        }

        public virtual IByteBuffer EnsureWritable(int minWritableBytes)
        {
            Buf.EnsureWritable(minWritableBytes);
            return this;
        }

        public virtual int EnsureWritable(int minWritableBytes, bool force) => Buf.EnsureWritable(minWritableBytes, force);

        public virtual bool GetBoolean(int index) => Buf.GetBoolean(index);

        public virtual byte GetByte(int index) => Buf.GetByte(index);

        public virtual short GetShort(int index) => Buf.GetShort(index);

        public virtual short GetShortLE(int index) => Buf.GetShortLE(index);

        public virtual int GetUnsignedMedium(int index) => Buf.GetUnsignedMedium(index);

        public virtual int GetUnsignedMediumLE(int index) => Buf.GetUnsignedMediumLE(index);

        public virtual int GetInt(int index) => Buf.GetInt(index);

        public virtual int GetIntLE(int index) => Buf.GetIntLE(index);

        public virtual long GetLong(int index) => Buf.GetLong(index);

        public virtual long GetLongLE(int index) => Buf.GetLongLE(index);

        public virtual IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            Buf.GetBytes(index, dst, dstIndex, length);
            return this;
        }

        public virtual IByteBuffer GetBytes(int index, byte[] dst)
        {
            Buf.GetBytes(index, dst);
            return this;
        }

        public virtual IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            Buf.GetBytes(index, dst, dstIndex, length);
            return this;
        }

        public virtual IByteBuffer GetBytes(int index, Stream output, int length)
        {
            Buf.GetBytes(index, output, length);
            return this;
        }

        public ICharSequence GetCharSequence(int index, int length, Encoding encoding) => Buf.GetCharSequence(index, length, encoding);

        public string GetString(int index, int length, Encoding encoding) => Buf.GetString(index, length, encoding);

        public virtual IByteBuffer SetBoolean(int index, bool value)
        {
            Buf.SetBoolean(index, value);
            return this;
        }

        public virtual IByteBuffer SetByte(int index, int value)
        {
            Buf.SetByte(index, value);
            return this;
        }

        public virtual IByteBuffer SetShort(int index, int value)
        {
            Buf.SetShort(index, value);
            return this;
        }

        public virtual IByteBuffer SetShortLE(int index, int value)
        {
            Buf.SetShortLE(index, value);
            return this;
        }

        public virtual IByteBuffer SetMedium(int index, int value)
        {
            Buf.SetMedium(index, value);
            return this;
        }

        public virtual IByteBuffer SetMediumLE(int index, int value)
        {
            Buf.SetMediumLE(index, value);
            return this;
        }

        public virtual IByteBuffer SetInt(int index, int value)
        {
            Buf.SetInt(index, value);
            return this;
        }

        public virtual IByteBuffer SetIntLE(int index, int value)
        {
            Buf.SetIntLE(index, value);
            return this;
        }

        public virtual IByteBuffer SetLong(int index, long value)
        {
            Buf.SetLong(index, value);
            return this;
        }

        public virtual IByteBuffer SetLongLE(int index, long value)
        {
            Buf.SetLongLE(index, value);
            return this;
        }

        public virtual IByteBuffer SetBytes(int index, IByteBuffer src, int length)
        {
            Buf.SetBytes(index, src, length);
            return this;
        }

        public virtual IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            Buf.SetBytes(index, src, srcIndex, length);
            return this;
        }

        public virtual IByteBuffer SetBytes(int index, byte[] src)
        {
            Buf.SetBytes(index, src);
            return this;
        }

        public virtual IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            Buf.SetBytes(index, src, srcIndex, length);
            return this;
        }

        public virtual Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken) => Buf.SetBytesAsync(index, src, length, cancellationToken);

        public int SetString(int index, string value, Encoding encoding) => Buf.SetString(index, value, encoding);

        public virtual IByteBuffer SetZero(int index, int length)
        {
            Buf.SetZero(index, length);
            return this;
        }

        public int SetCharSequence(int index, ICharSequence sequence, Encoding encoding) => Buf.SetCharSequence(index, sequence, encoding);

        public virtual bool ReadBoolean() => Buf.ReadBoolean();

        public virtual byte ReadByte() => Buf.ReadByte();

        public virtual short ReadShort() => Buf.ReadShort();

        public virtual short ReadShortLE() => Buf.ReadShortLE();

        public virtual int ReadMedium() => Buf.ReadMedium();

        public virtual int ReadMediumLE() => Buf.ReadMediumLE();

        public virtual int ReadUnsignedMedium() => Buf.ReadUnsignedMedium();

        public virtual int ReadUnsignedMediumLE() => Buf.ReadUnsignedMediumLE();

        public virtual int ReadInt() => Buf.ReadInt();

        public virtual int ReadIntLE() => Buf.ReadIntLE();

        public virtual long ReadLong() => Buf.ReadLong();

        public virtual long ReadLongLE() => Buf.ReadLongLE();

        public virtual IByteBuffer ReadBytes(int length) => Buf.ReadBytes(length);

        public virtual IByteBuffer ReadSlice(int length) => Buf.ReadSlice(length);

        public virtual IByteBuffer ReadRetainedSlice(int length) => Buf.ReadRetainedSlice(length);

        public virtual IByteBuffer ReadBytes(IByteBuffer dst, int length)
        {
            Buf.ReadBytes(dst, length);
            return this;
        }

        public virtual IByteBuffer ReadBytes(IByteBuffer dst, int dstIndex, int length)
        {
            Buf.ReadBytes(dst, dstIndex, length);
            return this;
        }

        public virtual IByteBuffer ReadBytes(byte[] dst)
        {
            Buf.ReadBytes(dst);
            return this;
        }

        public virtual IByteBuffer ReadBytes(byte[] dst, int dstIndex, int length)
        {
            Buf.ReadBytes(dst, dstIndex, length);
            return this;
        }

        public virtual IByteBuffer ReadBytes(Stream output, int length)
        {
            Buf.ReadBytes(output, length);
            return this;
        }

        public ICharSequence ReadCharSequence(int length, Encoding encoding) => Buf.ReadCharSequence(length, encoding);

        public string ReadString(int length, Encoding encoding) => Buf.ReadString(length, encoding);

        public virtual IByteBuffer SkipBytes(int length)
        {
            Buf.SkipBytes(length);
            return this;
        }

        public virtual IByteBuffer WriteBoolean(bool value)
        {
            Buf.WriteBoolean(value);
            return this;
        }

        public virtual IByteBuffer WriteByte(int value)
        {
            Buf.WriteByte(value);
            return this;
        }

        public virtual IByteBuffer WriteShort(int value)
        {
            Buf.WriteShort(value);
            return this;
        }

        public virtual IByteBuffer WriteShortLE(int value)
        {
            Buf.WriteShortLE(value);
            return this;
        }

        public virtual IByteBuffer WriteMedium(int value)
        {
            Buf.WriteMedium(value);
            return this;
        }

        public virtual IByteBuffer WriteMediumLE(int value)
        {
            Buf.WriteMediumLE(value);
            return this;
        }

        public virtual IByteBuffer WriteInt(int value)
        {
            Buf.WriteInt(value);
            return this;
        }

        public virtual IByteBuffer WriteIntLE(int value)
        {
            Buf.WriteIntLE(value);
            return this;
        }

        public virtual IByteBuffer WriteLong(long value)
        {
            Buf.WriteLong(value);
            return this;
        }

        public virtual IByteBuffer WriteLongLE(long value)
        {
            Buf.WriteLongLE(value);
            return this;
        }

        public virtual IByteBuffer WriteBytes(IByteBuffer src, int length)
        {
            Buf.WriteBytes(src, length);
            return this;
        }

        public virtual IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length)
        {
            Buf.WriteBytes(src, srcIndex, length);
            return this;
        }

        public virtual IByteBuffer WriteBytes(byte[] src)
        {
            Buf.WriteBytes(src);
            return this;
        }

        public virtual IByteBuffer WriteBytes(byte[] src, int srcIndex, int length)
        {
            Buf.WriteBytes(src, srcIndex, length);
            return this;
        }

        public virtual Task WriteBytesAsync(Stream input, int length, CancellationToken cancellationToken) => Buf.WriteBytesAsync(input, length, cancellationToken);

        public virtual IByteBuffer WriteZero(int length)
        {
            Buf.WriteZero(length);
            return this;
        }

        public int WriteCharSequence(ICharSequence sequence, Encoding encoding) => Buf.WriteCharSequence(sequence, encoding);

        public int WriteString(string value, Encoding encoding) => Buf.WriteString(value, encoding);

        public virtual int ForEachByte(int index, int length, IByteProcessor processor) => Buf.ForEachByte(index, length, processor);

        public virtual int ForEachByteDesc(int index, int length, IByteProcessor processor) => Buf.ForEachByteDesc(index, length, processor);

        public virtual IByteBuffer Copy(int index, int length) => Buf.Copy(index, length);

        public virtual IByteBuffer Slice() => Buf.Slice();

        public virtual IByteBuffer RetainedSlice() => Buf.RetainedSlice();

        public virtual IByteBuffer Slice(int index, int length) => Buf.Slice(index, length);

        public virtual IByteBuffer RetainedSlice(int index, int length) => Buf.RetainedSlice(index, length);

        public virtual IByteBuffer Duplicate() => Buf.Duplicate();

        public virtual IByteBuffer RetainedDuplicate() => Buf.RetainedDuplicate();

        public virtual bool IsSingleIoBuffer => Buf.IsSingleIoBuffer;

        public virtual int IoBufferCount => Buf.IoBufferCount;

        public virtual ArraySegment<byte> GetIoBuffer(int index, int length) => Buf.GetIoBuffer(index, length);

        public virtual ArraySegment<byte>[] GetIoBuffers(int index, int length) => Buf.GetIoBuffers(index, length);

        public bool HasArray => Buf.HasArray;

        public int ArrayOffset => Buf.ArrayOffset;

        public byte[] Array => Buf.Array;

        public override int GetHashCode() => Buf.GetHashCode();

        public override bool Equals(object obj) => Buf.Equals(obj);

        public bool Equals(IByteBuffer buffer) => Buf.Equals(buffer);

        public int CompareTo(IByteBuffer buffer) => Buf.CompareTo(buffer);

        public override string ToString() => GetType().Name + '(' + Buf + ')';

        public virtual IReferenceCounted Retain(int increment)
        {
            Buf.Retain(increment);
            return this;
        }

        public virtual IReferenceCounted Retain()
        {
            Buf.Retain();
            return this;
        }

        public virtual IReferenceCounted Touch()
        {
            Buf.Touch();
            return this;
        }

        public virtual IReferenceCounted Touch(object hint)
        {
            Buf.Touch(hint);
            return this;
        }

        public bool IsReadable(int size) => Buf.IsReadable(size);

        public bool IsWritable(int size) => Buf.IsWritable(size);

        public int ReferenceCount => Buf.ReferenceCount;

        public virtual bool Release() => Buf.Release();

        public virtual bool Release(int decrement) => Buf.Release(decrement);

        public virtual bool IsAccessible => Buf.IsAccessible;
    }
}