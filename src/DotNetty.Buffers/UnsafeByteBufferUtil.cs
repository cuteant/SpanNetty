// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using DotNetty.Common.Internal;

    static unsafe partial class UnsafeByteBufferUtil
    {
        const byte Zero = 0;

        [MethodImpl(InlineMethod.Value)]
        internal static void SetZero(byte[] array, int index, int length)
        {
            //if (0u >= (uint)length)
            //{
            //    return;
            //}
            PlatformDependent.SetMemory(array, index, length, Zero);
        }

        internal static IByteBuffer Copy(AbstractByteBuffer buf, byte* addr, int index, int length)
        {
            IByteBuffer copy = buf.Allocator.DirectBuffer(length, buf.MaxCapacity);
            if (0u >= (uint)length) { return copy; }

            if (copy.HasMemoryAddress)
            {
                IntPtr ptr = copy.AddressOfPinnedMemory();
                if (ptr != IntPtr.Zero)
                {
                    PlatformDependent.CopyMemory(addr, (byte*)ptr, length);
                }
                else
                {
                    fixed (byte* dst = &copy.GetPinnableMemoryAddress())
                    {
                        PlatformDependent.CopyMemory(addr, dst, length);
                    }
                }
                copy.SetIndex(0, length);
            }
            else
            {
                copy.WriteBytes(buf, index, length);
            }
            return copy;
        }

        //internal static int SetBytes(AbstractByteBuffer buf, byte* addr, int index, Stream input, int length)
        //{
        //    IByteBuffer tmpBuf = buf.Allocator.HeapBuffer(length);
        //    try
        //    {
        //        int readTotal = 0;
        //        int readBytes;
        //        byte[] tmp = tmpBuf.Array;
        //        int offset = tmpBuf.ArrayOffset;
        //        do
        //        {
        //            readBytes = input.Read(tmp, offset + readTotal, length - readTotal);
        //            readTotal += readBytes;
        //        }
        //        while (readBytes > 0 && readTotal < length);

        //        //if (readTotal > 0)
        //        //{
        //        PlatformDependent.CopyMemory(tmp, offset, addr, readTotal);
        //        //}

        //        return readTotal;
        //    }
        //    finally
        //    {
        //        tmpBuf.Release();
        //    }
        //}

        internal static void GetBytes(AbstractByteBuffer buf, byte* addr, int index, IByteBuffer dst, int dstIndex, int length)
        {
            //if (null == dst) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }

            //if (MathUtil.IsOutOfBounds(dstIndex, length, dst.Capacity))
            //{
            //    ThrowHelper.ThrowIndexOutOfRangeException_DstIndex(dstIndex);
            //}
            if (0u >= (uint)length) { return; }

            if (dst.HasMemoryAddress)
            {
                IntPtr ptr = dst.AddressOfPinnedMemory();
                if (ptr != IntPtr.Zero)
                {
                    PlatformDependent.CopyMemory(addr, (byte*)(ptr + dstIndex), length);
                }
                else
                {
                    fixed (byte* destination = &dst.GetPinnableMemoryAddress())
                    {
                        PlatformDependent.CopyMemory(addr, destination + dstIndex, length);
                    }
                }
                return;
            }

            GetBytes0(buf, addr, index, dst, dstIndex, length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void GetBytes0(AbstractByteBuffer buf, byte* addr, int index, IByteBuffer dst, int dstIndex, int length)
        {
            if (dst.HasArray)
            {
                PlatformDependent.CopyMemory(addr, dst.Array, dst.ArrayOffset + dstIndex, length);
            }
            else
            {
                dst.SetBytes(dstIndex, buf, index, length);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void GetBytes(byte* addr, byte[] dst, int dstIndex, int length)
        {
            //if (null == dst) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }

            //if (MathUtil.IsOutOfBounds(dstIndex, length, dst.Length))
            //{
            //    ThrowHelper.ThrowIndexOutOfRangeException_DstIndex(dstIndex);
            //}
            //if (length != 0)
            //{
            PlatformDependent.CopyMemory(addr, dst, dstIndex, length);
            //}
        }

        internal static void SetBytes(AbstractByteBuffer buf, byte* addr, int index, IByteBuffer src, int srcIndex, int length)
        {
            //if (null == src) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }

            //if (MathUtil.IsOutOfBounds(srcIndex, length, src.Capacity))
            //{
            //    ThrowHelper.ThrowIndexOutOfRangeException_SrcIndex(srcIndex);
            //}
            if (0u >= (uint)length) { return; }

            if (src.HasMemoryAddress)
            {
                IntPtr ptr = src.AddressOfPinnedMemory();
                if (ptr != IntPtr.Zero)
                {
                    PlatformDependent.CopyMemory((byte*)(ptr + srcIndex), addr, length);
                }
                else
                {
                    fixed (byte* source = &src.GetPinnableMemoryAddress())
                    {
                        PlatformDependent.CopyMemory(source + srcIndex, addr, length);
                    }
                }
                return;
            }

            SetBytes0(buf, addr, index, src, srcIndex, length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetBytes0(AbstractByteBuffer buf, byte* addr, int index, IByteBuffer src, int srcIndex, int length)
        {
            if (src.HasArray)
            {
                PlatformDependent.CopyMemory(src.Array, src.ArrayOffset + srcIndex, addr, length);
            }
            else
            {
                src.GetBytes(srcIndex, buf, index, length);
            }
        }

        // No need to check length zero, the calling method already done it
        [MethodImpl(InlineMethod.Value)]
        internal static void SetBytes(byte* addr, byte[] src, int srcIndex, int length) =>
                PlatformDependent.CopyMemory(src, srcIndex, addr, length);

        internal static void GetBytes(AbstractByteBuffer buf, byte* addr, int index, Stream output, int length)
        {
            if (length != 0)
            {
                IByteBuffer tmpBuf = buf.Allocator.HeapBuffer(length);
                try
                {
                    byte[] tmp = tmpBuf.Array;
                    int offset = tmpBuf.ArrayOffset;
                    PlatformDependent.CopyMemory(addr, tmp, offset, length);
                    output.Write(tmp, offset, length);
                }
                finally
                {
                    tmpBuf.Release();
                }
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetZero(byte* addr, int length)
        {
            //if (0u >= (uint)length)
            //{
            //    return;
            //}
            PlatformDependent.SetMemory(addr, length, Zero);
        }

        [MethodImpl(InlineMethod.Value)]
        internal static string GetString(byte* src, int length, Encoding encoding)
        {
#if NET40 || NET451
            int charCount = encoding.GetCharCount(src, length);
            char* chars = stackalloc char[charCount];
            encoding.GetChars(src, length, chars, charCount);
            return new string(chars, 0, charCount);
#else
            return encoding.GetString(src, length);
#endif
        }

        internal static UnpooledUnsafeDirectByteBuffer NewUnsafeDirectByteBuffer(IByteBufferAllocator alloc, int initialCapacity, int maxCapacity) =>
            new UnpooledUnsafeDirectByteBuffer(alloc, initialCapacity, maxCapacity);
    }
}