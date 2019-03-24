#if !NET40

using System;
using System.Buffers;

namespace DotNetty.Buffers
{
    /// <summary>A memory-owner that provides direct access to the root reference</summary>
    public interface IPoolMemoryOwner<T> : IMemoryOwner<T>
    {
        /// <summary>The root reference of the block, or a null-pointer if the data should not be considered pinned</summary>
        IntPtr Origin { get; }

        T[] Array { get; }

        int Offset { get; }

        /// <summary>Gets the size of the data</summary>
        int Length { get; }
    }
}

#endif