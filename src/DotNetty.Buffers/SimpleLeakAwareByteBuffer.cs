// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System.Diagnostics;
    using DotNetty.Common;

    class SimpleLeakAwareByteBuffer : WrappedByteBuffer
    {
        protected readonly IResourceLeakTracker Leak;
        private readonly IByteBuffer _trackedByteBuf;

        internal SimpleLeakAwareByteBuffer(IByteBuffer wrapped, IByteBuffer trackedByteBuf, IResourceLeakTracker leak)
            : base(wrapped)
        {
            if (trackedByteBuf is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.trackedByteBuf); }
            if (leak is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.leak); }

            _trackedByteBuf = trackedByteBuf;
            Leak = leak;
        }

        internal SimpleLeakAwareByteBuffer(IByteBuffer wrapped, IResourceLeakTracker leak)
            : this(wrapped, wrapped, leak)
        {
        }

        public override IByteBuffer Slice() => NewSharedLeakAwareByteBuffer(base.Slice());

        public override IByteBuffer RetainedSlice() => UnwrappedDerived(base.RetainedSlice());

        public override IByteBuffer RetainedSlice(int index, int length) => UnwrappedDerived(base.RetainedSlice(index, length));

        public override IByteBuffer RetainedDuplicate() => UnwrappedDerived(base.RetainedDuplicate());

        public override IByteBuffer ReadRetainedSlice(int length) => UnwrappedDerived(base.ReadRetainedSlice(length));

        public override IByteBuffer Slice(int index, int length) => NewSharedLeakAwareByteBuffer(base.Slice(index, length));

        public override IByteBuffer Duplicate() => NewSharedLeakAwareByteBuffer(base.Duplicate());

        public override IByteBuffer ReadSlice(int length) => NewSharedLeakAwareByteBuffer(base.ReadSlice(length));

        public override IByteBuffer AsReadOnly() => NewSharedLeakAwareByteBuffer(base.AsReadOnly());

        public override IReferenceCounted Touch() => this;

        public override IReferenceCounted Touch(object hint) => this;

        public override bool Release()
        {
            if (base.Release())
            {
                CloseLeak();
                return true;
            }

            return false;
        }

        public override bool Release(int decrement)
        {
            if (base.Release(decrement))
            {
                CloseLeak();
                return true;
            }
            return false;
        }

        void CloseLeak()
        {
            // Close the ResourceLeakTracker with the tracked ByteBuf as argument. This must be the same that was used when
            // calling DefaultResourceLeak.track(...).
            bool closed = Leak.Close(_trackedByteBuf);
            Debug.Assert(closed);
        }

        IByteBuffer UnwrappedDerived(IByteBuffer derived)
        {
            if (derived is AbstractPooledDerivedByteBuffer buffer)
            {
                // Update the parent to point to this buffer so we correctly close the ResourceLeakTracker.
                buffer.Parent(this);

                IResourceLeakTracker newLeak = AbstractByteBuffer.LeakDetector.Track(buffer);
                if (newLeak is null)
                {
                    // No leak detection, just return the derived buffer.
                    return derived;
                }

                return NewLeakAwareByteBuffer(buffer, newLeak);
            }

            return NewSharedLeakAwareByteBuffer(derived);
        }

        SimpleLeakAwareByteBuffer NewSharedLeakAwareByteBuffer(IByteBuffer wrapped) => NewLeakAwareByteBuffer(wrapped, _trackedByteBuf, Leak);

        SimpleLeakAwareByteBuffer NewLeakAwareByteBuffer(IByteBuffer wrapped, IResourceLeakTracker leakTracker) => NewLeakAwareByteBuffer(wrapped, wrapped, leakTracker);

        protected virtual SimpleLeakAwareByteBuffer NewLeakAwareByteBuffer(IByteBuffer buf, IByteBuffer trackedBuf, IResourceLeakTracker leakTracker) =>
            new SimpleLeakAwareByteBuffer(buf, trackedBuf, leakTracker);
    }
}