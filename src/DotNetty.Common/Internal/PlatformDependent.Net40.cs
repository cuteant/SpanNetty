// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
#if NET40
namespace DotNetty.Common.Internal
{
    using System;

    public static partial class PlatformDependent
    {
        internal static readonly bool Is64BitProcess = IntPtr.Size == sizeof(UInt64);
        private const int Zero = 0;

        public static void CopyMemory(byte[] src, int srcIndex, byte[] dst, int dstIndex, int length)
        {
            if (length <= Zero) { return; }

            Array.Copy(src, srcIndex, dst, dstIndex, length);
        }

        public static unsafe void CopyMemory(byte* srcPtr, byte* dstPtr, int length)
        {
            if (length <= Zero) { return; }

            const int u32Size = sizeof(UInt32);
            const int u64Size = sizeof(UInt64);

            byte* srcEndPtr = srcPtr + length;

            if (Is64BitProcess)
            {
                // 64-bit            
                const int u128Size = sizeof(UInt64) * 2;
                while (srcPtr + u128Size <= srcEndPtr)
                {
                    *(UInt64*)dstPtr = *(UInt64*)srcPtr;
                    dstPtr += u64Size;
                    srcPtr += u64Size;
                    *(UInt64*)dstPtr = *(UInt64*)srcPtr;
                    dstPtr += u64Size;
                    srcPtr += u64Size;
                }
                if (srcPtr + u64Size <= srcEndPtr)
                {
                    *(UInt64*)dstPtr ^= *(UInt64*)srcPtr;
                    dstPtr += u64Size;
                    srcPtr += u64Size;
                }
            }
            else
            {
                // 32-bit
                while (srcPtr + u64Size <= srcEndPtr)
                {
                    *(UInt32*)dstPtr = *(UInt32*)srcPtr;
                    dstPtr += u32Size;
                    srcPtr += u32Size;
                    *(UInt32*)dstPtr = *(UInt32*)srcPtr;
                    dstPtr += u32Size;
                    srcPtr += u32Size;
                }
            }

            if (srcPtr + u32Size <= srcEndPtr)
            {
                *(UInt32*)dstPtr = *(UInt32*)srcPtr;
                dstPtr += u32Size;
                srcPtr += u32Size;
            }

            if (srcPtr + sizeof(UInt16) <= srcEndPtr)
            {
                *(UInt16*)dstPtr = *(UInt16*)srcPtr;
                dstPtr += sizeof(UInt16);
                srcPtr += sizeof(UInt16);
            }

            if (srcPtr + 1 <= srcEndPtr)
            {
                *dstPtr = *srcPtr;
            }
        }

        public static unsafe void CopyMemory(byte* src, byte[] dst, int dstIndex, int length)
        {
            if (length <= Zero) { return; }

            fixed (byte* destination = &dst[dstIndex])
                CopyMemory(destination, src, length);
        }

        public static unsafe void CopyMemory(byte[] src, int srcIndex, byte* dst, int length)
        {
            if (length <= Zero) { return; }

            fixed (byte* source = &src[srcIndex])
                CopyMemory(dst, source, length);
        }

        public static void Clear(byte[] src, int srcIndex, int length)
        {
            if (length <= Zero) { return; }

            Array.Clear(src, srcIndex, length);
        }

        public static unsafe void SetMemory(byte* src, int length, byte value)
        {
            if (length <= Zero) { return; }

            const int Gap = 16;

            if (length <= Gap * 2)
            {
                while (length > 0)
                {
                    *src = value;
                    length--;
                    src++;
                }
                return;
            }

            int aval = Gap;
            length -= Gap;
            var srcCopy = src;
            do
            {
                *src = value;
                src++;
                --aval;
            } while (aval > 0);

            aval = Gap;
            while (true)
            {
                CopyMemory(srcCopy, src, aval);
                src += aval;
                length -= aval;
                aval *= 2;
                if (length <= aval)
                {
                    CopyMemory(srcCopy, src, length);
                    break;
                }
            }
        }

        public static void SetMemory(byte[] src, int srcIndex, int length, byte value)
        {
            if (length <= Zero) { return; }

            const int Gap = 16;
            int i = srcIndex;

            if (length <= Gap * 2)
            {
                while (length > 0)
                {
                    src[i] = value;
                    length--;
                    i++;
                }
                return;
            }
            int aval = Gap;
            length -= Gap;

            do
            {
                src[i] = value;
                i++;
                --aval;
            } while (aval > 0);

            aval = Gap;
            while (true)
            {
                Array.Copy(src, srcIndex, src, i, aval);
                i += aval;
                length -= aval;
                aval *= 2;
                if (length <= aval)
                {
                    Array.Copy(src, srcIndex, src, i, length);
                    break;
                }
            }
        }
    }
}
#endif
