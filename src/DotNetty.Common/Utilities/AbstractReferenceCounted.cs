
namespace DotNetty.Common.Utilities
{
    using System.Runtime.CompilerServices;
    using System.Threading;

    public abstract class AbstractReferenceCounted : IReferenceCounted
    {
        int referenceCount = 1;

        public int ReferenceCount => Volatile.Read(ref this.referenceCount);

        public IReferenceCounted Retain() => this.RetainCore(1);

        public IReferenceCounted Retain(int increment)
        {
            if (increment <= 0) { ThrowHelper.ThrowArgumentException_Positive(increment, ExceptionArgument.increment); }

            return this.RetainCore(increment);
        }

        protected virtual IReferenceCounted RetainCore(int increment)
        {
            var refCnt = Volatile.Read(ref this.referenceCount);
            int oldRefCnt;
            do
            {
                oldRefCnt = refCnt;
                int nextCount = refCnt + increment;

                // Ensure we don't resurrect (which means the refCnt was 0) and also that we encountered an overflow.
                if (nextCount <= increment) { ThrowIllegalReferenceCountException(refCnt, increment); }

                refCnt = Interlocked.CompareExchange(ref this.referenceCount, nextCount, refCnt);
            } while (refCnt != oldRefCnt);

            return this;
        }

        public IReferenceCounted Touch() => this.Touch(null);

        public abstract IReferenceCounted Touch(object hint);

        public bool Release() => this.ReleaseCore(1);

        public bool Release(int decrement)
        {
            if (decrement <= 0) { ThrowHelper.ThrowArgumentException_Positive(decrement, ExceptionArgument.decrement); }

            return this.ReleaseCore(decrement);
        }

        bool ReleaseCore(int decrement)
        {
            var refCnt = Volatile.Read(ref this.referenceCount);
            int oldRefCnt;
            do
            {
                oldRefCnt = refCnt;

                if (refCnt < decrement) { ThrowIllegalReferenceCountException(refCnt, decrement); }

                refCnt = Interlocked.CompareExchange(ref this.referenceCount, refCnt - decrement, refCnt);
            } while (refCnt != oldRefCnt);

            if (refCnt == decrement)
            {
                this.Deallocate();
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowIllegalReferenceCountException(int count, int increment)
        {
            throw GetIllegalReferenceCountException();

            IllegalReferenceCountException GetIllegalReferenceCountException()
            {
                return new IllegalReferenceCountException(count, increment);
            }
        }

        protected abstract void Deallocate();
    }
}
