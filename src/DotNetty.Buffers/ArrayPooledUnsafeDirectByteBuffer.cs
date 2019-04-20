using System;
using System.Buffers;
using DotNetty.Common;
#if !NET40
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace DotNetty.Buffers
{
    unsafe partial class ArrayPooledUnsafeDirectByteBuffer : ArrayPooledByteBuffer
    {
        static readonly ThreadLocalPool<ArrayPooledUnsafeDirectByteBuffer> Recycler = new ThreadLocalPool<ArrayPooledUnsafeDirectByteBuffer>(handle => new ArrayPooledUnsafeDirectByteBuffer(handle, 0));

        internal static ArrayPooledUnsafeDirectByteBuffer NewInstance(ArrayPooledByteBufferAllocator allocator, ArrayPool<byte> arrayPool, byte[] buffer, int length, int maxCapacity)
        {
            var buf = Recycler.Take();
            buf.Reuse(allocator, arrayPool, buffer, length, maxCapacity);
            return buf;
        }

        internal ArrayPooledUnsafeDirectByteBuffer(ThreadLocalPool.Handle recyclerHandle, int maxCapacity)
            : base(recyclerHandle, maxCapacity)
        {
        }

        public sealed override bool IsDirect => true;

        protected internal sealed override byte _GetByte(int index) => this.Memory[index];

        protected internal sealed override void _SetByte(int index, int value) => this.Memory[index] = unchecked((byte)value);

#if !NET40
        protected internal sealed override short _GetShort(int index)
        {
            return UnsafeByteBufferUtil.GetShort(ref this.Memory[0], index);
        }

        protected internal sealed override short _GetShortLE(int index)
        {
            return UnsafeByteBufferUtil.GetShortLE(ref this.Memory[0], index);
        }

        protected internal sealed override int _GetUnsignedMedium(int index)
        {
            return UnsafeByteBufferUtil.GetUnsignedMedium(ref this.Memory[0], index);
        }

        protected internal sealed override int _GetUnsignedMediumLE(int index)
        {
            return UnsafeByteBufferUtil.GetUnsignedMediumLE(ref this.Memory[0], index);
        }

        protected internal sealed override int _GetInt(int index)
        {
            return UnsafeByteBufferUtil.GetInt(ref this.Memory[0], index);
        }

        protected internal sealed override int _GetIntLE(int index)
        {
            return UnsafeByteBufferUtil.GetIntLE(ref this.Memory[0], index);
        }

        protected internal sealed override long _GetLong(int index)
        {
            return UnsafeByteBufferUtil.GetLong(ref this.Memory[0], index);
        }

        protected internal sealed override long _GetLongLE(int index)
        {
            return UnsafeByteBufferUtil.GetLongLE(ref this.Memory[0], index);
        }

        public sealed override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            if (null == dst) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }
            this.CheckDstIndex(index, length, dstIndex, dst.Capacity);
            fixed (byte* addr = &this.Addr(index))
            {
                UnsafeByteBufferUtil.GetBytes(this, addr, index, dst, dstIndex, length);
                return this;
            }
        }

        public sealed override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            if (null == dst) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            fixed (byte* addr = &this.Addr(index))
            {
                UnsafeByteBufferUtil.GetBytes(addr, dst, dstIndex, length);
                return this;
            }
        }

        protected internal sealed override void _SetShort(int index, int value)
        {
            UnsafeByteBufferUtil.SetShort(ref this.Memory[0], index, value);
        }

        protected internal sealed override void _SetShortLE(int index, int value)
        {
            UnsafeByteBufferUtil.SetShortLE(ref this.Memory[0], index, value);
        }

        protected internal sealed override void _SetMedium(int index, int value)
        {
            UnsafeByteBufferUtil.SetMedium(ref this.Memory[0], index, value);
        }

        protected internal sealed override void _SetMediumLE(int index, int value)
        {
            UnsafeByteBufferUtil.SetMediumLE(ref this.Memory[0], index, value);
        }

        protected internal sealed override void _SetInt(int index, int value)
        {
            UnsafeByteBufferUtil.SetInt(ref this.Memory[0], index, value);
        }

        protected internal sealed override void _SetIntLE(int index, int value)
        {
            UnsafeByteBufferUtil.SetIntLE(ref this.Memory[0], index, value);
        }

        protected internal sealed override void _SetLong(int index, long value)
        {
            UnsafeByteBufferUtil.SetLong(ref this.Memory[0], index, value);
        }

        protected internal sealed override void _SetLongLE(int index, long value)
        {
            UnsafeByteBufferUtil.SetLongLE(ref this.Memory[0], index, value);
        }

        public sealed override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            if (null == src) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
            this.CheckSrcIndex(index, length, srcIndex, src.Capacity);
            fixed (byte* addr = &this.Addr(index))
            {
                UnsafeByteBufferUtil.SetBytes(this, addr, index, src, srcIndex, length);
                return this;
            }
        }

        public sealed override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            if (null == src) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
            this.CheckSrcIndex(index, length, srcIndex, src.Length);
            if (length != 0)
            {
                fixed (byte* addr = &this.Addr(index))
                {
                    UnsafeByteBufferUtil.SetBytes(addr, src, srcIndex, length);
                    return this;
                }
            }
            return this;
        }

        public sealed override IByteBuffer GetBytes(int index, Stream output, int length)
        {
            if (null == output) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.output); }
            this.CheckIndex(index, length);
            //fixed (byte* addr = &this.Addr(index))
            //{
            //    UnsafeByteBufferUtil.GetBytes(this, addr, index, output, length);
            //    return this;
            //}
            // UnsafeByteBufferUtil.GetBytes 多一遍内存拷贝，最终还是调用 stream.write，没啥必要
#if NETCOREAPP
            output.Write(new ReadOnlySpan<byte>(this.Memory, index, length));
#else
            output.Write(this.Memory, index, length);
#endif
            return this;
        }

        public sealed override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            if (null == src) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }
            this.CheckIndex(index, length);
            //int read;
            //fixed (byte* addr = &this.Addr(index))
            //{
            //    read = UnsafeByteBufferUtil.SetBytes(this, addr, index, src, length);
            //    return Task.FromResult(read);
            //}
            int readTotal = 0;
            int read;
            do
            {
#if NETCOREAPP
                read = src.Read(new Span<byte>(this.Memory, index + readTotal, length - readTotal));
#else
                read = src.Read(this.Memory, index + readTotal, length - readTotal);
#endif
                readTotal += read;
            }
            while (read > 0 && readTotal < length);

            return Task.FromResult(readTotal);
        }

        public sealed override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* addr = &this.Addr(index))
                return UnsafeByteBufferUtil.Copy(this, addr, index, length);
        }

        [MethodImpl(InlineMethod.Value)]
        ref byte Addr(int index) => ref this.Memory[index];

        public sealed override IByteBuffer SetZero(int index, int length)
        {
            this.CheckIndex(index, length);
            fixed (byte* addr = &this.Addr(index))
            {
                UnsafeByteBufferUtil.SetZero(addr, length);
                return this;
            }
        }

        public sealed override IByteBuffer WriteZero(int length)
        {
            if (0u >= (uint)length) { return this; }

            this.EnsureWritable(length);
            int wIndex = this.WriterIndex;
            this.CheckIndex0(wIndex, length);
            fixed (byte* addr = &this.Addr(wIndex))
            {
                UnsafeByteBufferUtil.SetZero(addr, length);
            }
            this.SetWriterIndex(wIndex + length);

            return this;
        }
#endif
    }
}
