namespace DotNetty.Suite.Tests.Transport
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Transport.Bootstrapping;

    public class TestsuitePermutation
    {
        public static List<IByteBufferAllocator> Allocator()
        {
            List<IByteBufferAllocator> allocators = new List<IByteBufferAllocator>();
            allocators.Add(UnpooledByteBufferAllocator.Default);
            allocators.Add(PooledByteBufferAllocator.Default);
            allocators.Add(ArrayPooledByteBufferAllocator.Default);
            return allocators;
        }
    }

    public interface IClientBootstrapFactory
    {
        Bootstrap NewInstance();
    }

    public interface IServerBootstrapFactory
    {
        ServerBootstrap NewInstance();
    }
}