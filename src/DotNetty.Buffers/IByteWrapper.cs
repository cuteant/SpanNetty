using System;

namespace DotNetty.Buffers
{
    // support passing arrays of other types instead of having to copy to a ByteBuf[] first
    interface IByteWrapper<T>
    {
        IByteBuffer Wrap(T bytes);

        bool IsEmpty(T bytes);
    }

    sealed class ByteArrayWrapper : IByteWrapper<byte[]>
    {
        public static readonly ByteArrayWrapper Instance = new ByteArrayWrapper();

        private ByteArrayWrapper() { }

        public bool IsEmpty(byte[] bytes)
        {
            return 0u >= (uint)bytes.Length;
        }

        public IByteBuffer Wrap(byte[] bytes)
        {
            return Unpooled.WrappedBuffer(bytes);
        }
    }

    //sealed class ByteBufferWrapper : IByteWrapper<IByteBuffer>
    //{
    //    public static readonly ByteBufferWrapper Instance = new ByteBufferWrapper();

    //    private ByteBufferWrapper() { }

    //    public bool IsEmpty(IByteBuffer bytes)
    //    {
    //        return !bytes.Remaining();
    //    }

    //    public IByteBuffer Wrap(IByteBuffer bytes)
    //    {
    //        return Unpooled.WrappedBuffer(bytes);
    //    }
    //}
}
