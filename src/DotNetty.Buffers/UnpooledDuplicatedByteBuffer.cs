// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    partial class UnpooledDuplicatedByteBuffer : AbstractDerivedByteBuffer
    {
        readonly AbstractByteBuffer buffer;

        public UnpooledDuplicatedByteBuffer(AbstractByteBuffer buffer)
            : this(buffer, buffer.ReaderIndex, buffer.WriterIndex)
        {
        }

        internal UnpooledDuplicatedByteBuffer(AbstractByteBuffer buffer, int readerIndex, int writerIndex)
            : base(buffer.MaxCapacity)
        {
            switch (buffer)
            {
                case UnpooledDuplicatedByteBuffer duplicated:
                    this.buffer = duplicated.buffer;
                    break;

                case AbstractPooledDerivedByteBuffer _:
                    this.buffer = (AbstractByteBuffer)buffer.Unwrap();
                    break;

                case AbstractArrayPooledDerivedByteBuffer _:
                    this.buffer = (AbstractByteBuffer)buffer.Unwrap();
                    break;

                default:
                    this.buffer = buffer;
                    break;
            }

            this.SetIndex0(readerIndex, writerIndex);
            this.MarkIndex(); // Mark read and writer index
        }

        [MethodImpl(InlineMethod.Value)]
        public sealed override IByteBuffer Unwrap() => this.buffer;//this.UnwrapCore();

        public sealed override IByteBuffer Copy(int index, int length) => this.Unwrap().Copy(index, length);

        [MethodImpl(InlineMethod.Value)]
        protected AbstractByteBuffer UnwrapCore() => this.buffer;

        public sealed override IByteBufferAllocator Allocator => this.Unwrap().Allocator;

        public sealed override bool IsDirect => this.Unwrap().IsDirect;

        public sealed override int Capacity => this.Unwrap().Capacity;

        public sealed override IByteBuffer AdjustCapacity(int newCapacity) => this.Unwrap().AdjustCapacity(newCapacity);

        public sealed override bool IsSingleIoBuffer => this.Unwrap().IsSingleIoBuffer;

        public sealed override int IoBufferCount => this.Unwrap().IoBufferCount;

        public sealed override bool HasArray => this.Unwrap().HasArray;

        public sealed override byte[] Array => this.Unwrap().Array;

        public sealed override int ArrayOffset => this.Unwrap().ArrayOffset;

        public sealed override bool HasMemoryAddress => this.Unwrap().HasMemoryAddress;

        public sealed override ref byte GetPinnableMemoryAddress() => ref this.Unwrap().GetPinnableMemoryAddress();

        public sealed override IntPtr AddressOfPinnedMemory() => this.Unwrap().AddressOfPinnedMemory();

        protected internal sealed override byte _GetByte(int index) => this.UnwrapCore()._GetByte(index);

        protected internal sealed override short _GetShort(int index) => this.UnwrapCore()._GetShort(index);

        protected internal sealed override short _GetShortLE(int index) => this.UnwrapCore()._GetShortLE(index);

        protected internal sealed override int _GetUnsignedMedium(int index) => this.UnwrapCore()._GetUnsignedMedium(index);

        protected internal sealed override int _GetUnsignedMediumLE(int index) => this.UnwrapCore()._GetUnsignedMediumLE(index);

        protected internal sealed override int _GetInt(int index) => this.UnwrapCore()._GetInt(index);

        protected internal sealed override int _GetIntLE(int index) => this.UnwrapCore()._GetIntLE(index);

        protected internal sealed override long _GetLong(int index) => this.UnwrapCore()._GetLong(index);

        protected internal sealed override long _GetLongLE(int index) => this.UnwrapCore()._GetLongLE(index);

        public sealed override IByteBuffer GetBytes(int index, IByteBuffer destination, int dstIndex, int length) { this.Unwrap().GetBytes(index, destination, dstIndex, length); return this; }

        public sealed override IByteBuffer GetBytes(int index, byte[] destination, int dstIndex, int length) { this.Unwrap().GetBytes(index, destination, dstIndex, length); return this; }

        public sealed override IByteBuffer GetBytes(int index, Stream destination, int length) { this.Unwrap().GetBytes(index, destination, length); return this; }

        protected internal sealed override void _SetByte(int index, int value) => this.UnwrapCore()._SetByte(index, value);

        protected internal sealed override void _SetShort(int index, int value) => this.UnwrapCore()._SetShort(index, value);

        protected internal sealed override void _SetShortLE(int index, int value) => this.UnwrapCore()._SetShortLE(index, value);

        protected internal sealed override void _SetMedium(int index, int value) => this.UnwrapCore()._SetMedium(index, value);

        protected internal sealed override void _SetMediumLE(int index, int value) => this.UnwrapCore()._SetMediumLE(index, value);

        public sealed override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length) { this.Unwrap().SetBytes(index, src, srcIndex, length); return this; }

        public sealed override Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken) => this.Unwrap().SetBytesAsync(index, src, length, cancellationToken);

        public sealed override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length) { this.Unwrap().SetBytes(index, src, srcIndex, length);return this; }

        protected internal sealed override void _SetInt(int index, int value) => this.UnwrapCore()._SetInt(index, value);

        protected internal sealed override void _SetIntLE(int index, int value) => this.UnwrapCore()._SetIntLE(index, value);

        protected internal sealed override void _SetLong(int index, long value) => this.UnwrapCore()._SetLong(index, value);

        protected internal sealed override void _SetLongLE(int index, long value) => this.UnwrapCore()._SetLongLE(index, value);

        public sealed override int ForEachByte(int index, int length, IByteProcessor processor) => this.Unwrap().ForEachByte(index, length, processor);

        public sealed override int ForEachByteDesc(int index, int length, IByteProcessor processor) => this.Unwrap().ForEachByteDesc(index, length, processor);
    }
}
