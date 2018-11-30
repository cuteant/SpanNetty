
namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// A derived buffer which forbids any write requests to its parent.  It is
    /// recommended to use <see cref="Unpooled.UnmodifiableBuffer(IByteBuffer)"/>
    /// instead of calling the constructor explicitly.
    /// </summary>
    public class ReadOnlyByteBuffer : AbstractDerivedByteBuffer
    {
        readonly IByteBuffer buffer;

        public ReadOnlyByteBuffer(IByteBuffer buffer)
            : base(buffer != null ? buffer.MaxCapacity : AbstractByteBufferAllocator.DefaultMaxCapacity)
        {
            if (null == buffer) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }

            switch (buffer)
            {
                case ReadOnlyByteBuffer _:
                case UnpooledDuplicatedByteBuffer _:
                    this.buffer = buffer.Unwrap();
                    break;

                default:
                    this.buffer = buffer;
                    break;
            }
            this.SetIndex(buffer.ReaderIndex, buffer.WriterIndex);
        }

        public override bool IsReadOnly => true;

        public override IByteBuffer AsReadOnly() => this;

        public override bool IsWritable() => false;

        public override bool IsWritable(int size) => false;

        public override int Capacity => this.Unwrap().Capacity;

        public override IByteBuffer AdjustCapacity(int newCapacity) => throw new ReadOnlyBufferException();

        public override IByteBuffer EnsureWritable(int minWritableBytes) => throw new ReadOnlyBufferException();

        public override int EnsureWritable(int minWritableBytes, bool force) => 1;

        public override IByteBuffer Unwrap() => this.buffer;

        public override IByteBufferAllocator Allocator => this.Unwrap().Allocator;

        public override bool IsDirect => this.Unwrap().IsDirect;

        public override bool HasArray => false;

        public override byte[] Array => throw new ReadOnlyBufferException();

        public override int ArrayOffset => throw new ReadOnlyBufferException();

        public override bool HasMemoryAddress => this.Unwrap().HasMemoryAddress;

        public override ref byte GetPinnableMemoryAddress() => ref this.Unwrap().GetPinnableMemoryAddress();

        public override IntPtr AddressOfPinnedMemory() => this.Unwrap().AddressOfPinnedMemory();

        public override IByteBuffer DiscardReadBytes() => throw new ReadOnlyBufferException();

        public override IByteBuffer DiscardSomeReadBytes() => throw new ReadOnlyBufferException();

        public override IByteBuffer SetBytes(int index, byte[] src)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int length)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            throw new ReadOnlyBufferException();
        }

        public override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetByte(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        protected internal override void _SetByte(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetInt(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        protected internal override void _SetInt(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetIntLE(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        protected internal override void _SetIntLE(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetLong(int index, long value)
        {
            throw new ReadOnlyBufferException();
        }

        protected internal override void _SetLong(int index, long value)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetLongLE(int index, long value)
        {
            throw new ReadOnlyBufferException();
        }

        protected internal override void _SetLongLE(int index, long value)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetMedium(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        protected internal override void _SetMedium(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetMediumLE(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        protected internal override void _SetMediumLE(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetShort(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        protected internal override void _SetShort(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetShortLE(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        protected internal override void _SetShortLE(int index, int value)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetBoolean(int index, bool value)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetChar(int index, char value)
        {
            throw new ReadOnlyBufferException();
        }

        public override int SetCharSequence(int index, ICharSequence sequence, Encoding encoding)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetDouble(int index, double value)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetFloat(int index, float value)
        {
            throw new ReadOnlyBufferException();
        }

        public override int SetString(int index, string value, Encoding encoding)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetUnsignedShort(int index, ushort value)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetUnsignedShortLE(int index, ushort value)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer SetZero(int index, int length)
        {
            throw new ReadOnlyBufferException();
        }

        public override IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length)
        {
            return this.Unwrap().GetBytes(index, destination, dstIndex, length);
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length)
        {
            return this.Unwrap().GetBytes(index, destination, dstIndex, length);
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            return this.Unwrap().GetBytes(index, destination, length);
        }

        public override byte GetByte(int index)
        {
            return this.Unwrap().GetByte(index);
        }

        protected internal override byte _GetByte(int index)
        {
            return this.Unwrap().GetByte(index);
        }

        public override int GetInt(int index)
        {
            return this.Unwrap().GetInt(index);
        }

        protected internal override int _GetInt(int index)
        {
            return this.Unwrap().GetInt(index);
        }

        public override int GetIntLE(int index)
        {
            return this.Unwrap().GetIntLE(index);
        }

        protected internal override int _GetIntLE(int index)
        {
            return this.Unwrap().GetIntLE(index);
        }

        public override long GetLong(int index)
        {
            return this.Unwrap().GetLong(index);
        }

        protected internal override long _GetLong(int index)
        {
            return this.Unwrap().GetLong(index);
        }

        public override long GetLongLE(int index)
        {
            return this.Unwrap().GetLongLE(index);
        }

        protected internal override long _GetLongLE(int index)
        {
            return this.Unwrap().GetLongLE(index);
        }

        public override short GetShort(int index)
        {
            return this.Unwrap().GetShort(index);
        }

        protected internal override short _GetShort(int index)
        {
            return this.Unwrap().GetShort(index);
        }

        public override short GetShortLE(int index)
        {
            return this.Unwrap().GetShortLE(index);
        }

        protected internal override short _GetShortLE(int index)
        {
            return this.Unwrap().GetShortLE(index);
        }

        public override int GetUnsignedMedium(int index)
        {
            return this.Unwrap().GetUnsignedMedium(index);
        }

        protected internal override int _GetUnsignedMedium(int index)
        {
            return this.Unwrap().GetUnsignedMedium(index);
        }

        public override int GetUnsignedMediumLE(int index)
        {
            return this.Unwrap().GetUnsignedMediumLE(index);
        }

        protected internal override int _GetUnsignedMediumLE(int index)
        {
            return this.Unwrap().GetUnsignedMediumLE(index);
        }

        public override string GetString(int index, int length, Encoding encoding)
        {
            return this.Unwrap().GetString(index, length, encoding);
        }

        public override int IoBufferCount => this.Unwrap().IoBufferCount;

        public override IByteBuffer Copy(int index, int length)
        {
            return this.Unwrap().Copy(index, length);
        }

        public override IByteBuffer Duplicate()
        {
            return new ReadOnlyByteBuffer(this);
        }

        public override IByteBuffer Slice(int index, int length)
        {
            return new ReadOnlyByteBuffer(this.Unwrap().Slice(index, length));
        }

        public override int ForEachByte(IByteProcessor processor)
        {
            return this.Unwrap().ForEachByte(processor);
        }

        public override int ForEachByte(int index, int length, IByteProcessor processor)
        {
            return this.Unwrap().ForEachByte(index, length, processor);
        }

        public override int ForEachByteDesc(IByteProcessor processor)
        {
            return this.Unwrap().ForEachByteDesc(processor);
        }

        public override int ForEachByteDesc(int index, int length, IByteProcessor processor)
        {
            return this.Unwrap().ForEachByteDesc(index, length, processor);
        }
    }
}
