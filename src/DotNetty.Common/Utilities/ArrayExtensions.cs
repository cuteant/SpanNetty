// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using CuteAnt;
    using DotNetty.Common.Internal;

    /// <summary>
    ///     Extension methods used for slicing byte arrays
    /// </summary>
    public static class ArrayExtensions
    {
        public static readonly byte[] ZeroBytes = EmptyArray<byte>.Instance;

        public static T[] Slice<T>(this T[] array, int length) => Slice(array, 0, length);

        public static T[] Slice<T>(this T[] array, int index, int length)
        {
            if (array is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array); }
            if ((uint)(index + length) > (uint)array.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_Slice(index, length, array.Length);
            }
#if !NET40
            T[] result;
            Span<T> destSpan = result = new T[length];
            array.AsSpan(index, length).CopyTo(destSpan);
            return result;
#else
            var result = new T[length];
            Array.Copy(array, index, result, 0, length);
            return result;
#endif
        }

        public static void SetRange<T>(this T[] array, int index, T[] src) => SetRange(array, index, src, 0, src.Length);

        public static void SetRange<T>(this T[] array, int index, T[] src, int srcIndex, int srcLength)
        {
            if (array is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array); }
            if (src is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }

#if !NET40
            Span<T> srcSpan = src.AsSpan(srcIndex, srcLength);
            srcSpan.CopyTo(array.AsSpan(index));
#else
            if ((uint)(index + srcLength) > (uint)array.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_SetRange_Index(index, srcLength, array.Length);
            }
            if ((uint)(srcIndex + srcLength) > (uint)src.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_SetRange_SrcIndex(srcIndex, srcLength, src.Length);
            }
            Array.Copy(src, srcIndex, array, index, srcLength);
#endif
        }

        public static void Fill<T>(this T[] array, T value)
        {
#if !NET40
            Span<T> span = array;
            span.Fill(value);
#else
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = value;
            }
#endif
        }

        public static void Fill<T>(this T[] array, int offset, int count, T value)
        {
            if (MathUtil.IsOutOfBounds(offset, count, array.Length))
            {
                ThrowHelper.ThrowIndexOutOfRangeException_Index(offset, count, array.Length);
            }

#if !NET40
            Span<T> span = array.AsSpan(offset, count);
            span.Fill(value);
#else
            for (int i = offset; i < count + offset; i++)
            {
                array[i] = value;
            }
#endif
        }

        /// <summary>
        ///     Merge the byte arrays into one byte array.
        /// </summary>
        public static byte[] CombineBytes(this byte[][] arrays)
        {
            long newlength = 0;
            foreach (byte[] array in arrays)
            {
                newlength += array.Length;
            }

            var mergedArray = new byte[newlength];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, mergedArray, offset, array.Length);
                offset += array.Length;
            }

            return mergedArray;
        }
    }
}