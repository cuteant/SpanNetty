
namespace DotNetty.Buffers
{
    using DotNetty.Common;

    public sealed class UnreleasableByteBuffer : WrappedByteBuffer
    {
        public UnreleasableByteBuffer(IByteBuffer buf)
            : base(buf is UnreleasableByteBuffer unreleasable ? unreleasable.Unwrap() : buf)
        {
        }

        public override IByteBuffer AsReadOnly() => Buf.IsReadOnly ? this : new UnreleasableByteBuffer(Buf.AsReadOnly());

        public override IByteBuffer ReadSlice(int length) => new UnreleasableByteBuffer(Buf.ReadSlice(length));

        // We could call buf.readSlice(..), and then call buf.release(). However this creates a leak in unit tests
        // because the release method on UnreleasableByteBuf will never allow the leak record to be cleaned up.
        // So we just use readSlice(..) because the end result should be logically equivalent.
        public override IByteBuffer ReadRetainedSlice(int length) => ReadSlice(length);

        public override IByteBuffer Slice() => new UnreleasableByteBuffer(Buf.Slice());

        // We could call buf.retainedSlice(), and then call buf.release(). However this creates a leak in unit tests
        // because the release method on UnreleasableByteBuf will never allow the leak record to be cleaned up.
        // So we just use slice() because the end result should be logically equivalent.
        public override IByteBuffer RetainedSlice() => Slice();

        public override IByteBuffer Slice(int index, int length) => new UnreleasableByteBuffer(Buf.Slice(index, length));

        // We could call buf.retainedSlice(..), and then call buf.release(). However this creates a leak in unit tests
        // because the release method on UnreleasableByteBuf will never allow the leak record to be cleaned up.
        // So we just use slice(..) because the end result should be logically equivalent.
        public override IByteBuffer RetainedSlice(int index, int length) => Slice(index, length);

        public override IByteBuffer Duplicate() => new UnreleasableByteBuffer(Buf.Duplicate());

        // We could call buf.retainedDuplicate(), and then call buf.release(). However this creates a leak in unit tests
        // because the release method on UnreleasableByteBuf will never allow the leak record to be cleaned up.
        // So we just use duplicate() because the end result should be logically equivalent.
        public override IByteBuffer RetainedDuplicate() => Duplicate();

        public override IReferenceCounted Retain() => this;

        public override IReferenceCounted Retain(int increment) => this;

        public override IReferenceCounted Touch() => this;

        public override IReferenceCounted Touch(object hint) => this;

        public override bool Release() => false;

        public override bool Release(int decrement) => false;
    }
}
