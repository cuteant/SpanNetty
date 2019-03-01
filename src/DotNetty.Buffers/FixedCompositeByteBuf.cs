
namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using CuteAnt;
    using DotNetty.Common;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// <see cref="IByteBuffer"/> implementation which allows to wrap an array of <see cref="IByteBuffer"/> in a read-only mode.
    /// This is useful to write an array of <see cref="IByteBuffer"/>s.
    /// </summary>
    public sealed class FixedCompositeByteBuf : AbstractReferenceCountedByteBuffer
    {
        static readonly IByteBuffer[] Empty = { Unpooled.Empty };

        readonly int nioBufferCount;
        readonly int capacity;
        readonly IByteBufferAllocator allocator;
        readonly IByteBuffer[] buffers;
        readonly bool direct;

        public FixedCompositeByteBuf(IByteBufferAllocator allocator, params IByteBuffer[] buffers)
            : base(AbstractByteBufferAllocator.DefaultMaxCapacity)
        {
            if (null == buffers || 0u >= (uint)buffers.Length)
            {
                this.buffers = Empty;
                this.nioBufferCount = 1;
                this.capacity = 0;
                this.direct = false;
            }
            else
            {
                var b = buffers[0];
                this.buffers = buffers;
                var direct = true;
                int nioBufferCount = b.IoBufferCount;
                int capacity = b.ReadableBytes;
                for (int i = 1; i < buffers.Length; i++)
                {
                    b = buffers[i];
                    nioBufferCount += b.IoBufferCount;
                    capacity += b.ReadableBytes;
                    if (!b.IsDirect)
                    {
                        direct = false;
                    }
                }
                this.nioBufferCount = nioBufferCount;
                this.capacity = capacity;
                this.direct = direct;
            }
            this.SetIndex(0, this.capacity);
            this.allocator = allocator;
        }

        public override bool IsWritable() => false;

        public override bool IsWritable(int size) => false;

        public override int Capacity => this.capacity;

        public override int MaxCapacity => this.capacity;

        public override IByteBufferAllocator Allocator => this.allocator;

        public override IByteBuffer Unwrap() => null;

        public override bool IsDirect => this.direct;

        public override bool HasArray
        {
            get
            {
                switch (this.buffers.Length)
                {
                    case 0:
                        return true;
                    case 1:
                        return this.Buffer(0).HasArray;
                    default:
                        return false;
                }
            }
        }

        public override byte[] Array
        {
            get
            {
                switch (this.buffers.Length)
                {
                    case 0:
                        return ArrayExtensions.ZeroBytes;
                    case 1:
                        return this.Buffer(0).Array;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        public override int ArrayOffset
        {
            get
            {
                switch (this.buffers.Length)
                {
                    case 0:
                        return 0;
                    case 1:
                        return this.Buffer(0).ArrayOffset;
                    default:
                        throw new NotSupportedException();
                }
            }
        }

        public override bool HasMemoryAddress
        {
            get
            {
                switch (this.buffers.Length)
                {
                    case 1:
                        return this.Buffer(0).HasMemoryAddress;
                    default:
                        return false;
                }
            }
        }

        public override ref byte GetPinnableMemoryAddress()
        {
            switch (this.buffers.Length)
            {
                case 1:
                    return ref this.Buffer(0).GetPinnableMemoryAddress();
                default:
                    throw new NotSupportedException();
            }
        }

        public override IntPtr AddressOfPinnedMemory()
        {
            switch (this.buffers.Length)
            {
                case 1:
                    return this.Buffer(0).AddressOfPinnedMemory();
                default:
                    throw new NotSupportedException();
            }
        }

        public override int IoBufferCount => this.nioBufferCount;

        ComponentEntry FindComponent(int index)
        {
            int readable = 0;
            for (int i = 0; i < this.buffers.Length; i++)
            {
                var b = this.buffers[i];
                var comp = b as ComponentEntry;
                if (comp != null)
                {
                    b = comp.Buf;
                }
                readable += b.ReadableBytes;
                if (index < readable)
                {
                    if (comp == null)
                    {
                        // Create a new component and store it in the array so it not create a new object
                        // on the next access.
                        comp = new ComponentEntry(i, readable - b.ReadableBytes, b);
                        buffers[i] = comp;
                    }
                    return comp;
                }
            }

            return ThrowHelper.ThrowInvalidOperationException_ShouldNotReachHere<ComponentEntry>();
        }

        /// <summary>
        /// Return the <see cref="IByteBuffer"/> stored at the given index of the array.
        /// </summary>
        IByteBuffer Buffer(int idx)
        {
            var b = this.buffers[idx];
            return b is ComponentEntry comp ? comp.Buf : b;
        }

        public override byte GetByte(int index)
        {
            return this._GetByte(index);
        }

        protected internal override byte _GetByte(int index)
        {
            var c = this.FindComponent(index);
            return c.Buf.GetByte(index - c.Offset);
        }

        protected internal override short _GetShort(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 2 <= c.EndOffset)
            {
                return c.Buf.GetShort(index - c.Offset);
            }

            return (short)(this._GetByte(index) << 8 | this._GetByte(index + 1));
        }

        protected internal override short _GetShortLE(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 2 <= c.EndOffset)
            {
                return c.Buf.GetShortLE(index - c.Offset);
            }

            return (short)(this._GetByte(index) << 8 | this._GetByte(index + 1));
        }

        protected internal override int _GetUnsignedMedium(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 3 <= c.EndOffset)
            {
                return c.Buf.GetUnsignedMedium(index - c.Offset);
            }

            return (this._GetShort(index) & 0xffff) << 8 | this._GetByte(index + 2);
        }

        protected internal override int _GetUnsignedMediumLE(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 3 <= c.EndOffset)
            {
                return c.Buf.GetUnsignedMediumLE(index - c.Offset);
            }

            return (this._GetShortLE(index) & 0xffff) << 8 | this._GetByte(index + 2);
        }

        protected internal override int _GetInt(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 4 <= c.EndOffset)
            {
                return c.Buf.GetInt(index - c.Offset);
            }

            return this._GetShort(index) << 16 | (ushort)this._GetShort(index + 2);
        }

        protected internal override int _GetIntLE(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 4 <= c.EndOffset)
            {
                return c.Buf.GetIntLE(index - c.Offset);
            }

            return (this._GetShortLE(index) << 16 | (ushort)this._GetShortLE(index + 2));
        }

        protected internal override long _GetLong(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 8 <= c.EndOffset)
            {
                return c.Buf.GetLong(index - c.Offset);
            }

            return (long)this._GetInt(index) << 32 | (uint)this._GetInt(index + 4);
        }

        protected internal override long _GetLongLE(int index)
        {
            ComponentEntry c = this.FindComponent(index);
            if (index + 8 <= c.EndOffset)
            {
                return c.Buf.GetLongLE(index - c.Offset);
            }

            return (this._GetIntLE(index) << 32 | this._GetIntLE(index + 4));
        }

        public override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            if (length == 0) { return this; }

            var c = this.FindComponent(index);
            int i = c.Index;
            int adjustment = c.Offset;
            var s = c.Buf;
            while (true)
            {
                int localLength = Math.Min(length, s.ReadableBytes - (index - adjustment));
                s.GetBytes(index - adjustment, dst, dstIndex, localLength);
                index += localLength;
                dstIndex += localLength;
                length -= localLength;
                adjustment += s.ReadableBytes;
                if (length <= 0) { break; }
                s = this.Buffer(++i);
            }
            return this;
        }

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Capacity);
            if (length == 0) { return this; }

            var c = this.FindComponent(index);
            int i = c.Index;
            int adjustment = c.Offset;
            var s = c.Buf;
            while (true)
            {
                int localLength = Math.Min(length, s.ReadableBytes - (index - adjustment));
                s.GetBytes(index - adjustment, dst, dstIndex, localLength);
                index += localLength;
                dstIndex += localLength;
                length -= localLength;
                adjustment += s.ReadableBytes;
                if (length <= 0) { break; }
                s = this.Buffer(++i);
            }
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            this.CheckIndex(index, length);
            if (length == 0) { return this; }

            var c = this.FindComponent(index);
            int i = c.Index;
            int adjustment = c.Offset;
            var s = c.Buf;
            while (true)
            {
                int localLength = Math.Min(length, s.ReadableBytes - (index - adjustment));
                s.GetBytes(index - adjustment, destination, localLength);
                index += localLength;
                length -= localLength;
                adjustment += s.ReadableBytes;
                if (length <= 0) { break; }
                s = this.Buffer(++i);
            }
            return this;
        }

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            var release = true;
            var buf = this.allocator.Buffer(length);
            try
            {
                buf.WriteBytes(this, index, length);
                release = false;
                return buf;
            }
            finally
            {
                if (release) { buf.Release(); }
            }
        }

        public override ArraySegment<byte> GetIoBuffer(int index, int length)
        {
            this.CheckIndex(index, length);

            if (this.buffers.Length == 1)
            {
                var buf = this.Buffer(0);
                if (buf.IoBufferCount == 1)
                {
                    return buf.GetIoBuffer(index, length);
                }
            }

            var merged = new byte[length];
            ArraySegment<byte>[] bufs = this.GetIoBuffers(index, length);

            int offset = 0;
            foreach (ArraySegment<byte> buf in bufs)
            {
                Debug.Assert(merged.Length - offset >= buf.Count);

                PlatformDependent.CopyMemory(buf.Array, buf.Offset, merged, offset, buf.Count);
                offset += buf.Count;
            }

            return new ArraySegment<byte>(merged);
        }

        public override ArraySegment<byte>[] GetIoBuffers(int index, int length)
        {
            this.CheckIndex(index, length);
            if (length == 0) { return EmptyArray<ArraySegment<byte>>.Instance; }

            var array = ThreadLocalList<ArraySegment<byte>>.NewInstance(this.nioBufferCount);
            try
            {
                var c = this.FindComponent(index);
                int i = c.Index;
                int adjustment = c.Offset;
                var s = c.Buf;
                for (; ; )
                {
                    int localLength = Math.Min(length, s.ReadableBytes - (index - adjustment));
                    switch (s.IoBufferCount)
                    {
                        case 0:
                            ThrowHelper.ThrowNotSupportedException();
                            break;
                        case 1:
                            array.Add(s.GetIoBuffer(index - adjustment, localLength));
                            break;
                        default:
                            array.AddRange(s.GetIoBuffers(index - adjustment, localLength));
                            break;
                    }

                    index += localLength;
                    length -= localLength;
                    adjustment += s.ReadableBytes;
                    if (length <= 0) { break; }
                    s = this.Buffer(++i);
                }

                return array.ToArray();
            }
            finally
            {
                array.Return();
            }
        }

        protected internal override void Deallocate()
        {
            for (int i = 0; i < this.buffers.Length; i++)
            {
                this.Buffer(i).Release();
            }
        }

        public override IByteBuffer AdjustCapacity(int newCapacity) => throw new ReadOnlyBufferException();

        public override IByteBuffer EnsureWritable(int minWritableBytes) => throw new ReadOnlyBufferException();

        public override int EnsureWritable(int minWritableBytes, bool force) => 1;

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

        sealed class ComponentEntry : WrappedByteBuffer
        {
            internal readonly int Index;
            internal readonly int Offset;
            internal readonly int EndOffset;

            public ComponentEntry(int index, int offset, IByteBuffer buf)
                : base(buf)
            {
                this.Index = index;
                this.Offset = offset;
                this.EndOffset = offset + buf.ReadableBytes;
            }
        }
    }
}
