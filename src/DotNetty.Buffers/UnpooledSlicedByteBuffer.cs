// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    partial class UnpooledSlicedByteBuffer : AbstractUnpooledSlicedByteBuffer
    {
        internal UnpooledSlicedByteBuffer(AbstractByteBuffer buffer, int index, int length)
            : base(buffer, index, length)
        {
        }

        public sealed override int Capacity => MaxCapacity;

        protected internal sealed override byte _GetByte(int index) => UnwrapCore()._GetByte(Idx(index));

        protected internal sealed override short _GetShort(int index) => UnwrapCore()._GetShort(Idx(index));

        protected internal sealed override short _GetShortLE(int index) => UnwrapCore()._GetShortLE(Idx(index));

        protected internal sealed override int _GetUnsignedMedium(int index) => UnwrapCore()._GetUnsignedMedium(Idx(index));

        protected internal sealed override int _GetUnsignedMediumLE(int index) => UnwrapCore()._GetUnsignedMediumLE(Idx(index));

        protected internal sealed override int _GetInt(int index) => UnwrapCore()._GetInt(Idx(index));

        protected internal sealed override int _GetIntLE(int index) => UnwrapCore()._GetIntLE(Idx(index));

        protected internal sealed override long _GetLong(int index) => UnwrapCore()._GetLong(Idx(index));

        protected internal sealed override long _GetLongLE(int index) => UnwrapCore()._GetLongLE(Idx(index));

        protected internal sealed override void _SetByte(int index, int value) => UnwrapCore()._SetByte(Idx(index), value);

        protected internal sealed override void _SetShort(int index, int value) => UnwrapCore()._SetShort(Idx(index), value);

        protected internal sealed override void _SetShortLE(int index, int value) => UnwrapCore()._SetShortLE(Idx(index), value);

        protected internal sealed override void _SetMedium(int index, int value) => UnwrapCore()._SetMedium(Idx(index), value);

        protected internal sealed override void _SetMediumLE(int index, int value) => UnwrapCore()._SetMediumLE(Idx(index), value);

        protected internal sealed override void _SetInt(int index, int value) => UnwrapCore()._SetInt(Idx(index), value);

        protected internal sealed override void _SetIntLE(int index, int value) => UnwrapCore()._SetIntLE(Idx(index), value);

        protected internal sealed override void _SetLong(int index, long value) => UnwrapCore()._SetLong(Idx(index), value);

        protected internal sealed override void _SetLongLE(int index, long value) => UnwrapCore()._SetLongLE(Idx(index), value);
    }
}
