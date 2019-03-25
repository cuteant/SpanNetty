// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable 420

namespace DotNetty.Buffers
{
    using System.Runtime.CompilerServices;
    using System.Threading;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    public abstract class AbstractReferenceCountedByteBuffer : AbstractByteBuffer
    {
        // even => "real" refcount is (refCnt >>> 1); odd => "real" refcount is 0
        int referenceCount = 2;

        protected AbstractReferenceCountedByteBuffer(int maxCapacity)
            : base(maxCapacity)
        {
        }

        public override int ReferenceCount => RealRefCnt(Volatile.Read(ref this.referenceCount));

        /// <summary>
        /// An unsafe operation intended for use by a subclass that sets the reference count of the buffer directly
        /// </summary>
        protected internal void SetReferenceCount(int newRefCnt) => Interlocked.Exchange(ref this.referenceCount, newRefCnt << 1); // overflow OK here

        public override IReferenceCounted Retain() => this.Retain0(1);

        public override IReferenceCounted Retain(int increment)
        {
            if (increment <= 0) { ThrowHelper.ThrowArgumentException_Positive(increment, ExceptionArgument.increment); }

            return this.Retain0(increment);
        }

        IReferenceCounted Retain0(int increment)
        {
            // all changes to the raw count are 2x the "real" change
            int adjustedIncrement = increment << 1; // overflow OK here
            int oldRef = this.GetAndAddRefCnt(adjustedIncrement);
            if ((oldRef & 1) != 0)
            {
                ThrowHelper.ThrowIllegalReferenceCountException(0, increment);
            }
            // don't pass 0!
            if ((oldRef <= 0 && oldRef + adjustedIncrement >= 0) ||
                (oldRef >= 0 && oldRef + adjustedIncrement < oldRef))
            {
                // overflow case
                this.GetAndAddRefCnt(-adjustedIncrement);
                ThrowIllegalRawReferenceCountException(oldRef, increment);
            }
            return this;
        }

        public override IReferenceCounted Touch() => this;

        public override IReferenceCounted Touch(object hint) => this;

        public override bool Release() => this.Release0(1);

        public override bool Release(int decrement)
        {
            if (decrement <= 0) { ThrowHelper.ThrowArgumentException_Positive(decrement, ExceptionArgument.decrement); }

            return this.Release0(decrement);
        }

        bool Release0(int decrement)
        {
            int rawCnt = Volatile.Read(ref this.referenceCount);
            int realCnt = ToLiveRealCnt(rawCnt, decrement);
            if (decrement == realCnt)
            {
                if (this.CompareAndSetRefCnt(rawCnt, 1))
                {
                    this.Deallocate();
                    return true;
                }
                return this.RetryRelease0(decrement);
            }
            return this.ReleaseNonFinal0(decrement, rawCnt, realCnt);
        }

        bool ReleaseNonFinal0(int decrement, int rawCnt, int realCnt)
        {
            if (decrement < realCnt
                    // all changes to the raw count are 2x the "real" change
                    && this.CompareAndSetRefCnt(rawCnt, rawCnt - (decrement << 1)))
            {
                return false;
            }
            return RetryRelease0(decrement);
        }

        bool RetryRelease0(int decrement)
        {
            var sw = new SpinWait();
            int rawCnt = Volatile.Read(ref this.referenceCount);
            int oldRawCnt;
            while (true)
            {
                oldRawCnt = rawCnt;
                int realCnt = ToLiveRealCnt(rawCnt, decrement);
                if (decrement == realCnt)
                {
                    rawCnt = Interlocked.CompareExchange(ref this.referenceCount, 1, oldRawCnt);
                    if (rawCnt == oldRawCnt)
                    {
                        this.Deallocate();
                        return true;
                    }
                }
                else if (decrement < realCnt)
                {
                    // all changes to the raw count are 2x the "real" change
                    rawCnt = Interlocked.CompareExchange(ref this.referenceCount, rawCnt - (decrement << 1), oldRawCnt);
                    if (rawCnt == oldRawCnt)
                    {
                        return false;
                    }
                }
                else
                {
                    ThrowHelper.ThrowIllegalReferenceCountException(realCnt, -decrement);
                }
                sw.SpinOnce(); // this benefits throughput under high contention
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        int GetAndAddRefCnt(int delta)
        {
            var refCnt = Volatile.Read(ref this.referenceCount);
            int oldRefCnt;
            do
            {
                oldRefCnt = refCnt;
                refCnt = Interlocked.CompareExchange(ref this.referenceCount, refCnt + delta, oldRefCnt);
            } while (refCnt != oldRefCnt);
            return oldRefCnt;
        }

        [MethodImpl(InlineMethod.Value)]
        bool CompareAndSetRefCnt(int expect, int update)
        {
            return expect == Interlocked.CompareExchange(ref this.referenceCount, update, expect);
        }

        [MethodImpl(InlineMethod.Value)]
        static int RealRefCnt(int rawCnt)
        {
            return (rawCnt & 1) != 0 ? 0 : rawCnt.RightUShift(1);
        }

        /// <summary>
        /// Like <see cref="RealRefCnt(int)"/> but throws if refCnt == 0
        /// </summary>
        /// <param name="rawCnt"></param>
        /// <param name="decrement"></param>
        /// <returns></returns>
        [MethodImpl(InlineMethod.Value)]
        static int ToLiveRealCnt(int rawCnt, int decrement)
        {
            if (0u >= (uint)(rawCnt & 1))
            {
                return rawCnt.RightUShift(1);
            }
            // odd rawCnt => already deallocated
            return ThrowIllegalReferenceCountException(-decrement);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int ThrowIllegalReferenceCountException(int increment)
        {
            throw GetIllegalReferenceCountException();

            IllegalReferenceCountException GetIllegalReferenceCountException()
            {
                return new IllegalReferenceCountException(0, increment);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowIllegalRawReferenceCountException(int count, int increment)
        {
            throw GetIllegalReferenceCountException();

            IllegalReferenceCountException GetIllegalReferenceCountException()
            {
                return new IllegalReferenceCountException(RealRefCnt(count), increment);
            }
        }

        protected internal abstract void Deallocate();
    }
}