// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    partial class ByteBufferUtil
    {
        /// <summary>
        /// Compares the two specified buffers as described in {@link ByteBuf#compareTo(ByteBuf)}.
        /// This method is useful when implementing a new buffer type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare(IByteBuffer bufferA, IByteBuffer bufferB)
        {
            if (bufferA.IsSingleIoBuffer && bufferB.IsSingleIoBuffer)
            {
                var spanA = bufferA.GetReadableSpan();
                var spanB = bufferB.GetReadableSpan();
                return SpanHelpers.SequenceCompareTo(ref MemoryMarshal.GetReference(spanA), spanA.Length, ref MemoryMarshal.GetReference(spanB), spanB.Length);
            }
            return CompareSlow(bufferA, bufferB);
        }

        private static int CompareSlow(IByteBuffer bufferA, IByteBuffer bufferB)
        {
            int aLen = bufferA.ReadableBytes;
            int bLen = bufferB.ReadableBytes;
            int minLength = Math.Min(aLen, bLen);
            int uintCount = minLength.RightUShift(2);
            int byteCount = minLength & 3;

            int aIndex = bufferA.ReaderIndex;
            int bIndex = bufferB.ReaderIndex;

            if (uintCount > 0)
            {
                int uintCountIncrement = uintCount << 2;
                int res = CompareUint(bufferA, bufferB, aIndex, bIndex, uintCountIncrement);
                if (res != 0)
                {
                    return res;
                }

                aIndex += uintCountIncrement;
                bIndex += uintCountIncrement;
            }

            for (int aEnd = aIndex + byteCount; aIndex < aEnd; ++aIndex, ++bIndex)
            {
                int comp = bufferA.GetByte(aIndex) - bufferB.GetByte(bIndex);
                if (comp != 0)
                {
                    return comp;
                }
            }

            return aLen - bLen;
        }

        static int CompareUint(IByteBuffer bufferA, IByteBuffer bufferB, int aIndex, int bIndex, int uintCountIncrement)
        {
            for (int aEnd = aIndex + uintCountIncrement; aIndex < aEnd; aIndex += 4, bIndex += 4)
            {
                long va = bufferA.GetUnsignedInt(aIndex);
                long vb = bufferB.GetUnsignedInt(bIndex);
                if (va > vb)
                {
                    return 1;
                }
                if (va < vb)
                {
                    return -1;
                }
            }
            return 0;
        }
    }
}
