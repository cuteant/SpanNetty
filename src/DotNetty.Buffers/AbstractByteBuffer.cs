// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable InconsistentNaming
namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    /// <summary>
    ///     Abstract base class implementation of a <see cref="T:DotNetty.Buffers.IByteBuffer" />
    /// </summary>
    public abstract partial class AbstractByteBuffer : IByteBuffer
    {
        protected const int IndexNotFound = -1;
        protected const uint NIndexNotFound = unchecked((uint)IndexNotFound);

        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<AbstractByteBuffer>();
        const string LegacyPropCheckAccessible = "io.netty.buffer.bytebuf.checkAccessible";
        const string PropCheckAccessible = "io.netty.buffer.checkAccessible";
        protected static readonly bool CheckAccessible; // accessed from CompositeByteBuf
        const string PropCheckBounds = "io.netty.buffer.checkBounds";
        static readonly bool CheckBounds;

        internal static readonly ResourceLeakDetector LeakDetector = ResourceLeakDetector.Create<IByteBuffer>();

        int readerIndex;
        int writerIndex;

        int markedReaderIndex;
        int markedWriterIndex;
        int maxCapacity;

        static AbstractByteBuffer()
        {
            if (SystemPropertyUtil.Contains(PropCheckAccessible))
            {
                CheckAccessible = SystemPropertyUtil.GetBoolean(PropCheckAccessible, true);
            }
            else
            {
                CheckAccessible = SystemPropertyUtil.GetBoolean(LegacyPropCheckAccessible, true);
            }
            CheckBounds = SystemPropertyUtil.GetBoolean(PropCheckBounds, true);
            if (Logger.DebugEnabled)
            {
                Logger.Debug("-D{}: {}", PropCheckAccessible, CheckAccessible);
                Logger.Debug("-D{}: {}", PropCheckBounds, CheckBounds);
            }
        }

        protected AbstractByteBuffer(int maxCapacity)
        {
            if ((uint)maxCapacity > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(maxCapacity, ExceptionArgument.maxCapacity); }

            this.maxCapacity = maxCapacity;
        }
        public virtual bool IsReadOnly => false;

        public virtual IByteBuffer AsReadOnly()
        {
            if (this.IsReadOnly) { return this; }
            return Unpooled.UnmodifiableBuffer(this);
        }

        public abstract int Capacity { get; }

        public abstract IByteBuffer AdjustCapacity(int newCapacity);

        public virtual int MaxCapacity => this.maxCapacity;

        protected void SetMaxCapacity(int newMaxCapacity)
        {
            if (newMaxCapacity < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(newMaxCapacity, ExceptionArgument.newMaxCapacity); }

            this.maxCapacity = newMaxCapacity;
        }

        public abstract IByteBufferAllocator Allocator { get; }

        public virtual int ReaderIndex => this.readerIndex;

        public virtual IByteBuffer SetReaderIndex(int index)
        {
            if (CheckBounds) { CheckIndexBounds(index, this.writerIndex); }

            this.readerIndex = index;
            return this;
        }

        public virtual int WriterIndex => this.writerIndex;

        public virtual IByteBuffer SetWriterIndex(int index)
        {
            if (CheckBounds) { CheckIndexBounds(this.readerIndex, index, this.Capacity); }

            this.SetWriterIndex0(index);
            return this;
        }

        internal protected void SetWriterIndex0(int index)
        {
            this.writerIndex = index;
        }

        public virtual IByteBuffer SetIndex(int readerIdx, int writerIdx)
        {
            if (CheckBounds) { CheckIndexBounds(readerIdx, writerIdx, this.Capacity); }

            this.SetIndex0(readerIdx, writerIdx);
            return this;
        }

        public virtual IByteBuffer Clear()
        {
            this.readerIndex = this.writerIndex = 0;
            return this;
        }

        public virtual bool IsReadable() => this.writerIndex > this.readerIndex;

        public virtual bool IsReadable(int size) => this.writerIndex - this.readerIndex >= size;

        public virtual bool IsWritable() => this.Capacity > this.writerIndex;

        public virtual bool IsWritable(int size) => this.Capacity - this.writerIndex >= size;

        public virtual int ReadableBytes
        {
            [MethodImpl(InlineMethod.AggressiveInlining)]
            get => this.writerIndex - this.readerIndex;
        }

        public virtual int WritableBytes
        {
            [MethodImpl(InlineMethod.AggressiveInlining)]
            get => this.Capacity - this.writerIndex;
        }

        public virtual int MaxWritableBytes
        {
            [MethodImpl(InlineMethod.AggressiveInlining)]
            get => this.MaxCapacity - this.writerIndex;
        }

        public virtual int MaxFastWritableBytes => this.WritableBytes;

        public virtual IByteBuffer MarkReaderIndex()
        {
            this.markedReaderIndex = this.readerIndex;
            return this;
        }

        public virtual IByteBuffer ResetReaderIndex()
        {
            this.SetReaderIndex(this.markedReaderIndex);
            return this;
        }

        public virtual IByteBuffer MarkWriterIndex()
        {
            this.markedWriterIndex = this.writerIndex;
            return this;
        }

        public virtual IByteBuffer ResetWriterIndex()
        {
            this.SetWriterIndex(this.markedWriterIndex);
            return this;
        }

        protected void MarkIndex()
        {
            this.markedReaderIndex = this.readerIndex;
            this.markedWriterIndex = this.writerIndex;
        }

        public virtual IByteBuffer DiscardReadBytes()
        {
            this.EnsureAccessible();

            var readerIdx = this.readerIndex;
            var writerIdx = this.writerIndex;
            if (0u >= (uint)readerIdx)
            {
                return this;
            }

            if (readerIdx != writerIdx)
            {
                this.SetBytes(0, this, readerIdx, writerIdx - readerIdx);
                this.writerIndex = writerIdx - readerIdx;
                this.AdjustMarkers(readerIdx);
                this.readerIndex = 0;
            }
            else
            {
                this.AdjustMarkers(readerIdx);
                this.writerIndex = this.readerIndex = 0;
            }

            return this;
        }

        public virtual IByteBuffer DiscardSomeReadBytes()
        {
            this.EnsureAccessible();

            var readerIdx = this.readerIndex;
            var writerIdx = this.writerIndex;
            if (0u >= (uint)readerIdx)
            {
                return this;
            }

            if (readerIdx == writerIdx)
            {
                this.AdjustMarkers(readerIdx);
                this.writerIndex = this.readerIndex = 0;
                return this;
            }

            if (readerIdx >= this.Capacity.RightUShift(1))
            {
                this.SetBytes(0, this, readerIdx, writerIdx - readerIdx);
                this.writerIndex = writerIdx - readerIdx;
                this.AdjustMarkers(readerIdx);
                this.readerIndex = 0;
            }

            return this;
        }

        protected void AdjustMarkers(int decrement)
        {
            int markedReaderIdx = this.markedReaderIndex;
            if (markedReaderIdx <= decrement)
            {
                this.markedReaderIndex = 0;
                int markedWriterIdx = this.markedWriterIndex;
                if (markedWriterIdx <= decrement)
                {
                    this.markedWriterIndex = 0;
                }
                else
                {
                    this.markedWriterIndex = markedWriterIdx - decrement;
                }
            }
            else
            {
                this.markedReaderIndex = markedReaderIdx - decrement;
                this.markedWriterIndex -= decrement;
            }
        }

        public virtual IByteBuffer EnsureWritable(int minWritableBytes)
        {
            if ((uint)minWritableBytes > SharedConstants.TooBigOrNegative)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_MinWritableBytes();
            }

            this.EnsureWritable0(minWritableBytes);
            return this;
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        protected internal void EnsureWritable0(int minWritableBytes)
        {
            this.EnsureAccessible();

            if (minWritableBytes <= this.WritableBytes) { return; }

            this.EnsureWritableInternal(this.writerIndex, minWritableBytes);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void EnsureWritableInternal(int writerIdx, int sizeHint)
        {
            var maxCapacity = this.MaxCapacity;
            if (CheckBounds)
            {
                CheckMinWritableBounds(sizeHint, writerIdx, maxCapacity, this);
            }

            // Normalize the current capacity to the power of 2.
            int minNewCapacity = writerIdx + sizeHint;
            int newCapacity = this.Allocator.CalculateNewCapacity(minNewCapacity, maxCapacity);

            int fastCapacity = writerIdx + this.MaxFastWritableBytes;
            // Grow by a smaller amount if it will avoid reallocation
            if (newCapacity > fastCapacity && minNewCapacity <= fastCapacity)
            {
                newCapacity = fastCapacity;
            }

            // Adjust to the new capacity.
            this.AdjustCapacity(newCapacity);
        }

        public virtual int EnsureWritable(int minWritableBytes, bool force)
        {
            uint uminWritableBytes = (uint)minWritableBytes;
            if (uminWritableBytes > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(minWritableBytes, ExceptionArgument.minWritableBytes); }

            this.EnsureAccessible();
            if (uminWritableBytes <= (uint)this.WritableBytes)
            {
                return 0;
            }

            var writerIdx = this.writerIndex;
            var maxCapacity = this.MaxCapacity;
            if (uminWritableBytes > (uint)(maxCapacity - writerIdx))
            {
                if (!force || this.Capacity == maxCapacity)
                {
                    return 1;
                }

                this.AdjustCapacity(maxCapacity);
                return 3;
            }

            // Normalize the current capacity to the power of 2.
            int minNewCapacity = writerIdx + minWritableBytes;
            int newCapacity = this.Allocator.CalculateNewCapacity(minNewCapacity, maxCapacity);

            int fastCapacity = writerIdx + this.MaxFastWritableBytes;
            // Grow by a smaller amount if it will avoid reallocation
            if (newCapacity > fastCapacity && minNewCapacity <= fastCapacity)
            {
                newCapacity = fastCapacity;
            }

            // Adjust to the new capacity.
            this.AdjustCapacity(newCapacity);
            return 2;
        }

        public virtual byte GetByte(int index)
        {
            this.CheckIndex(index);
            return this._GetByte(index);
        }

        protected internal abstract byte _GetByte(int index);

        public bool GetBoolean(int index) => this.GetByte(index) != 0;

        public virtual short GetShort(int index)
        {
            this.CheckIndex(index, 2);
            return this._GetShort(index);
        }

        protected internal abstract short _GetShort(int index);

        public virtual short GetShortLE(int index)
        {
            this.CheckIndex(index, 2);
            return this._GetShortLE(index);
        }

        protected internal abstract short _GetShortLE(int index);

        public virtual int GetUnsignedMedium(int index)
        {
            this.CheckIndex(index, 3);
            return this._GetUnsignedMedium(index);
        }

        protected internal abstract int _GetUnsignedMedium(int index);

        public virtual int GetUnsignedMediumLE(int index)
        {
            this.CheckIndex(index, 3);
            return this._GetUnsignedMediumLE(index);
        }

        protected internal abstract int _GetUnsignedMediumLE(int index);

        public virtual int GetInt(int index)
        {
            this.CheckIndex(index, 4);
            return this._GetInt(index);
        }

        protected internal abstract int _GetInt(int index);

        public virtual int GetIntLE(int index)
        {
            this.CheckIndex(index, 4);
            return this._GetIntLE(index);
        }

        protected internal abstract int _GetIntLE(int index);

        public virtual long GetLong(int index)
        {
            this.CheckIndex(index, 8);
            return this._GetLong(index);
        }

        protected internal abstract long _GetLong(int index);

        public virtual long GetLongLE(int index)
        {
            this.CheckIndex(index, 8);
            return this._GetLongLE(index);
        }

        protected internal abstract long _GetLongLE(int index);

        public virtual IByteBuffer GetBytes(int index, byte[] destination)
        {
            this.GetBytes(index, destination, 0, destination.Length);
            return this;
        }

        public abstract IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length);

        public abstract IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length);

        public abstract IByteBuffer GetBytes(int index, Stream destination, int length);

        public virtual unsafe string GetString(int index, int length, Encoding encoding)
        {
            this.CheckIndex0(index, length);
            if (0u >= (uint)length)
            {
                return string.Empty;
            }

            if (this.HasMemoryAddress)
            {
                IntPtr ptr = this.AddressOfPinnedMemory();
                if (ptr != IntPtr.Zero)
                {
                    return UnsafeByteBufferUtil.GetString((byte*)(ptr + index), length, encoding);
                }
                else
                {
                    fixed (byte* p = &this.GetPinnableMemoryAddress())
                        return UnsafeByteBufferUtil.GetString(p + index, length, encoding);
                }
            }
            if (this.HasArray)
            {
                return encoding.GetString(this.Array, this.ArrayOffset + index, length);
            }

            return this.ToString(index, length, encoding);
        }

        public virtual string ReadString(int length, Encoding encoding)
        {
            var readerIdx = this.readerIndex;
            string value = this.GetString(readerIdx, length, encoding);
            this.readerIndex = readerIdx + length;
            return value;
        }

        public virtual unsafe ICharSequence GetCharSequence(int index, int length, Encoding encoding)
        {
            this.CheckIndex0(index, length);
            if (0u >= (uint)length)
            {
                return StringCharSequence.Empty;
            }

            if (SharedConstants.ASCIICodePage == encoding.CodePage)// || SharedConstants.ISO88591CodePage == encoding.CodePage)
            {
                // ByteBufUtil.getBytes(...) will return a new copy which the AsciiString uses directly
                return new AsciiString(ByteBufferUtil.GetBytes(this, index, length, true), false);
            }

            if (this.HasMemoryAddress)
            {
                IntPtr ptr = this.AddressOfPinnedMemory();
                if (ptr != IntPtr.Zero)
                {
                    return new StringCharSequence(UnsafeByteBufferUtil.GetString((byte*)(ptr + index), length, encoding));
                }
                else
                {
                    fixed (byte* p = &this.GetPinnableMemoryAddress())
                        return new StringCharSequence(UnsafeByteBufferUtil.GetString(p + index, length, encoding));
                }
            }
            if (this.HasArray)
            {
                return new StringCharSequence(encoding.GetString(this.Array, this.ArrayOffset + index, length));
            }

            return new StringCharSequence(this.ToString(index, length, encoding));
        }

        public virtual ICharSequence ReadCharSequence(int length, Encoding encoding)
        {
            var readerIdx = this.readerIndex;
            ICharSequence sequence = this.GetCharSequence(readerIdx, length, encoding);
            this.readerIndex = readerIdx + length;
            return sequence;
        }

        public virtual IByteBuffer SetByte(int index, int value)
        {
            this.CheckIndex(index);
            this._SetByte(index, value);
            return this;
        }

        protected internal abstract void _SetByte(int index, int value);

        public virtual IByteBuffer SetBoolean(int index, bool value)
        {
            this.SetByte(index, value ? 1 : 0);
            return this;
        }

        public virtual IByteBuffer SetShort(int index, int value)
        {
            this.CheckIndex(index, 2);
            this._SetShort(index, value);
            return this;
        }

        protected internal abstract void _SetShort(int index, int value);

        public virtual IByteBuffer SetShortLE(int index, int value)
        {
            this.CheckIndex(index, 2);
            this._SetShortLE(index, value);
            return this;
        }

        protected internal abstract void _SetShortLE(int index, int value);

        public virtual IByteBuffer SetMedium(int index, int value)
        {
            this.CheckIndex(index, 3);
            this._SetMedium(index, value);
            return this;
        }

        protected internal abstract void _SetMedium(int index, int value);

        public virtual IByteBuffer SetMediumLE(int index, int value)
        {
            this.CheckIndex(index, 3);
            this._SetMediumLE(index, value);
            return this;
        }

        protected internal abstract void _SetMediumLE(int index, int value);

        public virtual IByteBuffer SetInt(int index, int value)
        {
            this.CheckIndex(index, 4);
            this._SetInt(index, value);
            return this;
        }

        protected internal abstract void _SetInt(int index, int value);

        public virtual IByteBuffer SetIntLE(int index, int value)
        {
            this.CheckIndex(index, 4);
            this._SetIntLE(index, value);
            return this;
        }

        protected internal abstract void _SetIntLE(int index, int value);

        public virtual IByteBuffer SetLong(int index, long value)
        {
            this.CheckIndex(index, 8);
            this._SetLong(index, value);
            return this;
        }

        protected internal abstract void _SetLong(int index, long value);

        public virtual IByteBuffer SetLongLE(int index, long value)
        {
            this.CheckIndex(index, 8);
            this._SetLongLE(index, value);
            return this;
        }

        protected internal abstract void _SetLongLE(int index, long value);

        public virtual IByteBuffer SetBytes(int index, byte[] src)
        {
            this.SetBytes(index, src, 0, src.Length);
            return this;
        }

        public abstract IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length);

        public virtual IByteBuffer SetBytes(int index, IByteBuffer src, int length)
        {
            if (src is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }

            this.CheckIndex(index, length);
            if (CheckBounds) { CheckReadableBounds(src, length); }

            this.SetBytes(index, src, src.ReaderIndex, length);
            src.SetReaderIndex(src.ReaderIndex + length);
            return this;
        }

        public abstract IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length);

        public abstract Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken);

        public virtual IByteBuffer SetZero(int index, int length)
        {
            if (0u >= (uint)length)
            {
                return this;
            }

            this.CheckIndex(index, length);

            int nLong = length.RightUShift(3);
            int nBytes = length & 7;
            for (int i = nLong; i > 0; i--)
            {
                this._SetLong(index, 0);
                index += 8;
            }
            if (nBytes == 4)
            {
                this._SetInt(index, 0);
                // Not need to update the index as we not will use it after this.
            }
            else if (nBytes < 4)
            {
                for (int i = nBytes; i > 0; i--)
                {
                    this._SetByte(index, 0);
                    index++;
                }
            }
            else
            {
                this._SetInt(index, 0);
                index += 4;
                for (int i = nBytes - 4; i > 0; i--)
                {
                    this._SetByte(index, 0);
                    index++;
                }
            }

            return this;
        }

        public virtual int SetString(int index, string value, Encoding encoding) => this.SetString0(index, value, encoding, false);

        int SetString0(int index, string value, Encoding encoding, bool expand)
        {
            switch (encoding.CodePage)
            {
                case SharedConstants.UTF8CodePage:
                    int len = ByteBufferUtil.Utf8MaxBytes(value);
                    if (expand)
                    {
                        this.EnsureWritable0(len);
                        this.CheckIndex0(index, len);
                    }
                    else
                    {
                        this.CheckIndex(index, len);
                    }
                    return ByteBufferUtil.WriteUtf8(this, index, value);

                case SharedConstants.ASCIICodePage:
                    int length = value.Length;
                    if (expand)
                    {
                        this.EnsureWritable0(length);
                        this.CheckIndex0(index, length);
                    }
                    else
                    {
                        this.CheckIndex(index, length);
                    }
                    return ByteBufferUtil.WriteAscii(this, index, value);

                default:
                    byte[] bytes = encoding.GetBytes(value);
                    if (expand)
                    {
                        this.EnsureWritable0(bytes.Length);
                        // setBytes(...) will take care of checking the indices.
                    }
                    this.SetBytes(index, bytes);
                    return bytes.Length;
            }
        }

        public virtual int SetCharSequence(int index, ICharSequence sequence, Encoding encoding) => this.SetCharSequence0(index, sequence, encoding, false);

        int SetCharSequence0(int index, ICharSequence sequence, Encoding encoding, bool expand)
        {
            switch (encoding.CodePage)
            {
                case SharedConstants.UTF8CodePage:
                    int len = ByteBufferUtil.Utf8MaxBytes(sequence);
                    if (expand)
                    {
                        this.EnsureWritable0(len);
                        this.CheckIndex0(index, len);
                    }
                    else
                    {
                        this.CheckIndex(index, len);
                    }
                    return ByteBufferUtil.WriteUtf8(this, index, sequence);

                case SharedConstants.ASCIICodePage:
                    int length = sequence.Count;
                    if (expand)
                    {
                        this.EnsureWritable0(length);
                        this.CheckIndex0(index, length);
                    }
                    else
                    {
                        this.CheckIndex(index, length);
                    }
                    return ByteBufferUtil.WriteAscii(this, index, sequence);

                default:
                    byte[] bytes = encoding.GetBytes(sequence.ToString());
                    if (expand)
                    {
                        this.EnsureWritable0(bytes.Length);
                        // setBytes(...) will take care of checking the indices.
                    }
                    this.SetBytes(index, bytes);
                    return bytes.Length;
            }
        }

        public virtual byte ReadByte()
        {
            this.CheckReadableBytes0(1);
            int i = this.readerIndex;
            byte b = this._GetByte(i);
            this.readerIndex = i + 1;
            return b;
        }

        public bool ReadBoolean() => this.ReadByte() != 0;

        public virtual short ReadShort()
        {
            this.CheckReadableBytes0(2);
            short v = this._GetShort(this.readerIndex);
            this.readerIndex += 2;
            return v;
        }

        public virtual short ReadShortLE()
        {
            this.CheckReadableBytes0(2);
            short v = this._GetShortLE(this.readerIndex);
            this.readerIndex += 2;
            return v;
        }

        public int ReadMedium()
        {
            uint value = (uint)this.ReadUnsignedMedium();
            if ((value & 0x800000) != 0)
            {
                value |= 0xff000000;
            }

            return (int)value;
        }

        public int ReadMediumLE()
        {
            uint value = (uint)this.ReadUnsignedMediumLE();
            if ((value & 0x800000) != 0)
            {
                value |= 0xff000000;
            }

            return (int)value;
        }

        public virtual int ReadUnsignedMedium()
        {
            this.CheckReadableBytes0(3);
            int v = this._GetUnsignedMedium(this.readerIndex);
            this.readerIndex += 3;
            return v;
        }

        public virtual int ReadUnsignedMediumLE()
        {
            this.CheckReadableBytes0(3);
            int v = this._GetUnsignedMediumLE(this.readerIndex);
            this.readerIndex += 3;
            return v;
        }

        public virtual int ReadInt()
        {
            this.CheckReadableBytes0(4);
            int v = this._GetInt(this.readerIndex);
            this.readerIndex += 4;
            return v;
        }

        public virtual int ReadIntLE()
        {
            this.CheckReadableBytes0(4);
            int v = this._GetIntLE(this.readerIndex);
            this.readerIndex += 4;
            return v;
        }

        public virtual long ReadLong()
        {
            this.CheckReadableBytes0(8);
            long v = this._GetLong(this.readerIndex);
            this.readerIndex += 8;
            return v;
        }

        public virtual long ReadLongLE()
        {
            this.CheckReadableBytes0(8);
            long v = this._GetLongLE(this.readerIndex);
            this.readerIndex += 8;
            return v;
        }

        public virtual IByteBuffer ReadBytes(int length)
        {
            this.CheckReadableBytes(length);
            if (0u >= (uint)length)
            {
                return Unpooled.Empty;
            }

            IByteBuffer buf = this.Allocator.Buffer(length, this.MaxCapacity);
            buf.WriteBytes(this, this.readerIndex, length);
            this.readerIndex += length;
            return buf;
        }

        public virtual IByteBuffer ReadSlice(int length)
        {
            this.CheckReadableBytes(length);
            IByteBuffer slice = this.Slice(this.readerIndex, length);
            this.readerIndex += length;
            return slice;
        }

        public virtual IByteBuffer ReadRetainedSlice(int length)
        {
            this.CheckReadableBytes(length);
            IByteBuffer slice = this.RetainedSlice(this.readerIndex, length);
            this.readerIndex += length;
            return slice;
        }

        public virtual IByteBuffer ReadBytes(byte[] destination, int dstIndex, int length)
        {
            this.CheckReadableBytes(length);
            this.GetBytes(this.readerIndex, destination, dstIndex, length);
            this.readerIndex += length;
            return this;
        }

        public virtual IByteBuffer ReadBytes(byte[] dst)
        {
            this.ReadBytes(dst, 0, dst.Length);
            return this;
        }

        public virtual IByteBuffer ReadBytes(IByteBuffer dst, int length)
        {
            if (CheckBounds) { CheckWritableBounds(dst, length); }

            this.ReadBytes(dst, dst.WriterIndex, length);
            dst.SetWriterIndex(dst.WriterIndex + length);
            return this;
        }

        public virtual IByteBuffer ReadBytes(IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckReadableBytes(length);
            this.GetBytes(this.readerIndex, dst, dstIndex, length);
            this.readerIndex += length;
            return this;
        }

        public virtual IByteBuffer ReadBytes(Stream destination, int length)
        {
            this.CheckReadableBytes(length);
            this.GetBytes(this.readerIndex, destination, length);
            this.readerIndex += length;
            return this;
        }

        public virtual IByteBuffer SkipBytes(int length)
        {
            this.CheckReadableBytes(length);
            this.readerIndex += length;
            return this;
        }

        public virtual IByteBuffer WriteBoolean(bool value)
        {
            this.WriteByte(value ? 1 : 0);
            return this;
        }

        public virtual IByteBuffer WriteByte(int value)
        {
            this.EnsureWritable0(1);
            this._SetByte(this.writerIndex++, value);
            return this;
        }

        public virtual IByteBuffer WriteShort(int value)
        {
            this.EnsureWritable0(2);
            int writerIdx = this.writerIndex;
            this._SetShort(writerIdx, value);
            this.writerIndex = writerIdx + 2;
            return this;
        }

        public virtual IByteBuffer WriteShortLE(int value)
        {
            this.EnsureWritable0(2);
            int writerIdx = this.writerIndex;
            this._SetShortLE(writerIdx, value);
            this.writerIndex = writerIdx + 2;
            return this;
        }

        public virtual IByteBuffer WriteMedium(int value)
        {
            this.EnsureWritable0(3);
            int writerIdx = this.writerIndex;
            this._SetMedium(writerIdx, value);
            this.writerIndex = writerIdx + 3;
            return this;
        }

        public virtual IByteBuffer WriteMediumLE(int value)
        {
            this.EnsureWritable0(3);
            int writerIdx = this.writerIndex;
            this._SetMediumLE(writerIdx, value);
            this.writerIndex = writerIdx + 3;
            return this;
        }

        public virtual IByteBuffer WriteInt(int value)
        {
            this.EnsureWritable0(4);
            int writerIdx = this.writerIndex;
            this._SetInt(writerIdx, value);
            this.writerIndex = writerIdx + 4;
            return this;
        }

        public virtual IByteBuffer WriteIntLE(int value)
        {
            this.EnsureWritable0(4);
            int writerIdx = this.writerIndex;
            this._SetIntLE(writerIdx, value);
            this.writerIndex = writerIdx + 4;
            return this;
        }

        public virtual IByteBuffer WriteLong(long value)
        {
            this.EnsureWritable0(8);
            int writerIdx = this.writerIndex;
            this._SetLong(writerIdx, value);
            this.writerIndex = writerIdx + 8;
            return this;
        }

        public virtual IByteBuffer WriteLongLE(long value)
        {
            this.EnsureWritable0(8);
            int writerIdx = this.writerIndex;
            this._SetLongLE(writerIdx, value);
            this.writerIndex = writerIdx + 8;
            return this;
        }

        public virtual IByteBuffer WriteBytes(byte[] src, int srcIndex, int length)
        {
            this.EnsureWritable(length);
            int writerIdx = this.writerIndex;
            this.SetBytes(writerIdx, src, srcIndex, length);
            this.writerIndex = writerIdx + length;
            return this;
        }

        public virtual IByteBuffer WriteBytes(byte[] src)
        {
            this.WriteBytes(src, 0, src.Length);
            return this;
        }

        public virtual IByteBuffer WriteBytes(IByteBuffer src, int length)
        {
            if (CheckBounds) { CheckReadableBounds(src, length); }

            this.WriteBytes(src, src.ReaderIndex, length);
            src.SetReaderIndex(src.ReaderIndex + length);
            return this;
        }

        public virtual IByteBuffer WriteBytes(IByteBuffer src, int srcIndex, int length)
        {
            this.EnsureWritable(length);
            int writerIdx = this.writerIndex;
            this.SetBytes(writerIdx, src, srcIndex, length);
            this.writerIndex = writerIdx + length;
            return this;
        }

        public virtual async Task WriteBytesAsync(Stream stream, int length, CancellationToken cancellationToken)
        {
            this.EnsureWritable(length);
            if (this.WritableBytes < length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);
            }

            int writerIdx = this.writerIndex;
            int wrote = await this.SetBytesAsync(writerIdx, stream, length, cancellationToken);

            Debug.Assert(writerIdx == this.writerIndex);
            this.writerIndex = writerIdx + wrote;
        }

        public virtual IByteBuffer WriteZero(int length)
        {
            if (0u >= (uint)length)
            {
                return this;
            }

            this.EnsureWritable(length);
            int wIndex = this.writerIndex;
            this.CheckIndex0(wIndex, length);

            int nLong = length.RightUShift(3);
            int nBytes = length & 7;
            for (int i = nLong; i > 0; i--)
            {
                this._SetLong(wIndex, 0);
                wIndex += 8;
            }
            if (nBytes == 4)
            {
                this._SetInt(wIndex, 0);
                wIndex += 4;
            }
            else if (nBytes < 4)
            {
                for (int i = nBytes; i > 0; i--)
                {
                    this._SetByte(wIndex, 0);
                    wIndex++;
                }
            }
            else
            {
                this._SetInt(wIndex, 0);
                wIndex += 4;
                for (int i = nBytes - 4; i > 0; i--)
                {
                    this._SetByte(wIndex, 0);
                    wIndex++;
                }
            }

            this.writerIndex = wIndex;
            return this;
        }

        public virtual int WriteCharSequence(ICharSequence sequence, Encoding encoding)
        {
            int writerIdx = this.writerIndex;
            int written = this.SetCharSequence0(writerIdx, sequence, encoding, true);
            this.writerIndex = writerIdx + written;
            return written;
        }

        public virtual int WriteString(string value, Encoding encoding)
        {
            int writerIdx = this.writerIndex;
            int written = this.SetString0(writerIdx, value, encoding, true);
            this.writerIndex = writerIdx + written;
            return written;
        }

        public abstract IByteBuffer Copy(int index, int length);

        public virtual IByteBuffer Duplicate()
        {
            this.EnsureAccessible();
            return new UnpooledDuplicatedByteBuffer(this);
        }

        public virtual IByteBuffer RetainedDuplicate() => (IByteBuffer)this.Duplicate().Retain();

        public virtual IByteBuffer Slice() => this.Slice(this.readerIndex, this.ReadableBytes);

        public virtual IByteBuffer RetainedSlice() => (IByteBuffer)this.Slice().Retain();

        public virtual IByteBuffer Slice(int index, int length)
        {
            this.EnsureAccessible();
            return new UnpooledSlicedByteBuffer(this, index, length);
        }

        public virtual IByteBuffer RetainedSlice(int index, int length) => (IByteBuffer)this.Slice(index, length).Retain();

        public virtual int ForEachByte(int index, int length, IByteProcessor processor)
        {
            this.CheckIndex(index, length);
            return this.ForEachByteAsc0(index, length, processor);
        }

        public virtual int ForEachByteDesc(int index, int length, IByteProcessor processor)
        {
            this.CheckIndex(index, length);
            return this.ForEachByteDesc0(index, length, processor);
        }

        public override int GetHashCode() => ByteBufferUtil.HashCode(this);

        public sealed override bool Equals(object o) => this.Equals(o as IByteBuffer);

        public virtual bool Equals(IByteBuffer buffer) =>
            ReferenceEquals(this, buffer) || buffer is object && ByteBufferUtil.Equals(this, buffer);

        public virtual int CompareTo(IByteBuffer that) => ByteBufferUtil.Compare(this, that);

        public override string ToString()
        {
            if (0u >= (uint)this.ReferenceCount)
            {
                return StringUtil.SimpleClassName(this) + "(freed)";
            }

            var buf = StringBuilderManager.Allocate()
                .Append(StringUtil.SimpleClassName(this))
                .Append("(ridx: ").Append(this.readerIndex)
                .Append(", widx: ").Append(this.writerIndex)
                .Append(", cap: ").Append(this.Capacity);
            if (this.MaxCapacity != int.MaxValue)
            {
                buf.Append('/').Append(this.MaxCapacity);
            }

            IByteBuffer unwrapped = this.Unwrap();
            if (unwrapped is object)
            {
                buf.Append(", unwrapped: ").Append(unwrapped);
            }
            buf.Append(')');
            return StringBuilderManager.ReturnAndFree(buf);
        }

        protected void CheckIndex(int index) => this.CheckIndex(index, 1);

        protected internal void CheckIndex(int index, int fieldLength)
        {
            this.EnsureAccessible();
            this.CheckIndex0(index, fieldLength);
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        protected void CheckIndex0(int index, int fieldLength)
        {
            if (CheckBounds) { CheckRangeBounds(ExceptionArgument.index, index, fieldLength, this.Capacity); }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        protected void CheckSrcIndex(int index, int length, int srcIndex, int srcCapacity)
        {
            this.CheckIndex(index, length);
            if (CheckBounds) { CheckRangeBounds(ExceptionArgument.srcIndex, srcIndex, length, srcCapacity); }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        protected void CheckDstIndex(int index, int length, int dstIndex, int dstCapacity)
        {
            this.CheckIndex(index, length);
            if (CheckBounds) { CheckRangeBounds(ExceptionArgument.dstIndex, dstIndex, length, dstCapacity); }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        protected void CheckDstIndex(int length, int dstIndex, int dstCapacity)
        {
            this.CheckReadableBytes(length);
            if (CheckBounds) { CheckRangeBounds(ExceptionArgument.dstIndex, dstIndex, length, dstCapacity); }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        protected void CheckReadableBytes(int minimumReadableBytes)
        {
            if ((uint)minimumReadableBytes > SharedConstants.TooBigOrNegative) // < 0
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_MinimumReadableBytes(minimumReadableBytes);
            }

            this.CheckReadableBytes0(minimumReadableBytes);
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        protected void CheckNewCapacity(int newCapacity)
        {
            this.EnsureAccessible();
            if (CheckBounds && (/*newCapacity < 0 || */(uint)newCapacity > (uint)this.MaxCapacity))
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_Capacity(newCapacity, this.MaxCapacity);
            }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        void CheckReadableBytes0(int minimumReadableBytes)
        {
            this.EnsureAccessible();
            if (CheckBounds)
            {
                CheckMinReadableBounds(minimumReadableBytes, this.readerIndex, this.writerIndex, this);
            }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        protected void EnsureAccessible()
        {
            if (CheckAccessible && !this.IsAccessible)
            {
                ThrowHelper.ThrowIllegalReferenceCountException(0);
            }
        }

        protected void SetIndex0(int readerIdx, int writerIdx)
        {
            this.readerIndex = readerIdx;
            this.writerIndex = writerIdx;
        }

        protected void DiscardMarks()
        {
            this.markedReaderIndex = this.markedWriterIndex = 0;
        }

        public abstract bool IsSingleIoBuffer { get; }

        public abstract int IoBufferCount { get; }

        public abstract ArraySegment<byte> GetIoBuffer(int index, int length);

        public abstract ArraySegment<byte>[] GetIoBuffers(int index, int length);

        public abstract bool HasArray { get; }

        public abstract byte[] Array { get; }

        public abstract int ArrayOffset { get; }

        public abstract bool HasMemoryAddress { get; }

        public abstract ref byte GetPinnableMemoryAddress();

        public abstract IntPtr AddressOfPinnedMemory();

        public abstract IByteBuffer Unwrap();

        public abstract bool IsDirect { get; }

        public virtual bool IsAccessible => (uint)this.ReferenceCount > 0u ? true : false;

        public abstract int ReferenceCount { get; }

        public abstract IReferenceCounted Retain();

        public abstract IReferenceCounted Retain(int increment);

        public abstract IReferenceCounted Touch();

        public abstract IReferenceCounted Touch(object hint);

        public abstract bool Release();

        public abstract bool Release(int decrement);
    }
}