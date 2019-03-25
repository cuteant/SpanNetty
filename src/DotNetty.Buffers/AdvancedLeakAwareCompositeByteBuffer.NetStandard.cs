// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;
    using static AdvancedLeakAwareByteBuffer;

    partial class AdvancedLeakAwareCompositeByteBuffer
    {
        public override ReadOnlyMemory<byte> UnreadMemory
        {
            get
            {
                RecordLeakNonRefCountingOperation(this.Leak);
                return base.UnreadMemory;
            }
        }

        public override ReadOnlyMemory<byte> GetReadableMemory(int index, int count)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.GetReadableMemory(index, count);
        }

        public override ReadOnlySpan<byte> UnreadSpan
        {
            get
            {
                RecordLeakNonRefCountingOperation(this.Leak);
                return base.UnreadSpan;
            }
        }

        public override ReadOnlySpan<byte> GetReadableSpan(int index, int count)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.GetReadableSpan(index, count);
        }

        public override ReadOnlySequence<byte> UnreadSequence
        {
            get
            {
                RecordLeakNonRefCountingOperation(this.Leak);
                return base.UnreadSequence;
            }
        }

        public override ReadOnlySequence<byte> GetSequence(int index, int count)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.GetSequence(index, count);
        }

        public override Memory<byte> FreeMemory
        {
            get
            {
                RecordLeakNonRefCountingOperation(this.Leak);
                return base.FreeMemory;
            }
        }

        public override Memory<byte> GetMemory(int sizeHintt = 0)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.GetMemory(sizeHintt);
        }
        public override Memory<byte> GetMemory(int index, int count)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.GetMemory(index, count);
        }

        public override Span<byte> FreeSpan
        {
            get
            {
                RecordLeakNonRefCountingOperation(this.Leak);
                return base.FreeSpan;
            }
        }

        public override Span<byte> GetSpan(int sizeHintt = 0)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.GetSpan(sizeHintt);
        }
        public override Span<byte> GetSpan(int index, int count)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.GetSpan(index, count);
        }

        public override int GetBytes(int index, Span<byte> destination)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.GetBytes(index, destination);
        }
        public override int GetBytes(int index, Memory<byte> destination)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.GetBytes(index, destination);
        }

        public override int ReadBytes(Span<byte> destination)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.ReadBytes(destination);
        }
        public override int ReadBytes(Memory<byte> destination)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.ReadBytes(destination);
        }

        public override IByteBuffer SetBytes(int index, ReadOnlySpan<byte> src)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.SetBytes(index, src);
        }
        public override IByteBuffer SetBytes(int index, ReadOnlyMemory<byte> src)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.SetBytes(index, src);
        }

        public override IByteBuffer WriteBytes(ReadOnlySpan<byte> src)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.WriteBytes(src);
        }
        public override IByteBuffer WriteBytes(ReadOnlyMemory<byte> src)
        {
            RecordLeakNonRefCountingOperation(this.Leak);
            return base.WriteBytes(src);
        }
    }
}
#endif