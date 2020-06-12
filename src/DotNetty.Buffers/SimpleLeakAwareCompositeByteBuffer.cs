// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System.Diagnostics;
    using DotNetty.Common;

    class SimpleLeakAwareCompositeByteBuffer : WrappedCompositeByteBuffer
    {
        protected readonly IResourceLeakTracker Leak;

        internal SimpleLeakAwareCompositeByteBuffer(CompositeByteBuffer wrapped, IResourceLeakTracker leak) : base(wrapped)
        {
            if (leak is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.leak); }
            Leak = leak;
        }

        public override bool Release()
        {
            // Call unwrap() before just in case that super.release() will change the ByteBuf instance that is returned
            // by unwrap().
            IByteBuffer unwrapped = Unwrap();
            if (base.Release())
            {
                CloseLeak(unwrapped);
                return true;
            }

            return false;
        }

        public override bool Release(int decrement)
        {
            // Call unwrap() before just in case that super.release() will change the ByteBuf instance that is returned
            // by unwrap().
            IByteBuffer unwrapped = Unwrap();
            if (base.Release(decrement))
            {
                CloseLeak(unwrapped);
                return true;
            }

            return false;
        }

        void CloseLeak(IByteBuffer trackedByteBuf)
        {
            // Close the ResourceLeakTracker with the tracked ByteBuf as argument. This must be the same that was used when
            // calling DefaultResourceLeak.track(...).
            bool closed = Leak.Close(trackedByteBuf);
            Debug.Assert(closed);
        }

        public override IByteBuffer AsReadOnly() => NewLeakAwareByteBuffer(base.AsReadOnly());

        public override IByteBuffer Slice() => NewLeakAwareByteBuffer(base.Slice());

        public override IByteBuffer Slice(int index, int length) => NewLeakAwareByteBuffer(base.Slice(index, length));

        public override IByteBuffer Duplicate() => NewLeakAwareByteBuffer(base.Duplicate());

        public override IByteBuffer ReadSlice(int length) => NewLeakAwareByteBuffer(base.ReadSlice(length));

        public override IByteBuffer RetainedSlice() => NewLeakAwareByteBuffer(base.RetainedSlice());

        public override IByteBuffer RetainedSlice(int index, int length) => NewLeakAwareByteBuffer(base.RetainedSlice(index, length));

        public override IByteBuffer RetainedDuplicate() => NewLeakAwareByteBuffer(base.RetainedDuplicate());

        public override IByteBuffer ReadRetainedSlice(int length) => NewLeakAwareByteBuffer(base.ReadRetainedSlice(length));

        SimpleLeakAwareByteBuffer NewLeakAwareByteBuffer(IByteBuffer wrapped) => NewLeakAwareByteBuffer(wrapped, Unwrap(), Leak);

        protected virtual SimpleLeakAwareByteBuffer NewLeakAwareByteBuffer(IByteBuffer wrapped, IByteBuffer trackedByteBuf, IResourceLeakTracker leakTracker) =>
            new SimpleLeakAwareByteBuffer(wrapped, trackedByteBuf, leakTracker);
    }
}
