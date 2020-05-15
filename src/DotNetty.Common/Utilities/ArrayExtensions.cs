// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
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

            T[] result;
            Span<T> destSpan = result = new T[length];
            array.AsSpan(index, length).CopyTo(destSpan);
            return result;
        }

        public static void SetRange<T>(this T[] array, int index, T[] src) => SetRange(array, index, src, 0, src.Length);

        public static void SetRange<T>(this T[] array, int index, T[] src, int srcIndex, int srcLength)
        {
            if (array is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array); }
            if (src is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }

            Span<T> srcSpan = src.AsSpan(srcIndex, srcLength);
            srcSpan.CopyTo(array.AsSpan(index));
        }

        public static void Fill<T>(this T[] array, T value)
        {
            Span<T> span = array;
            span.Fill(value);
        }

        public static void Fill<T>(this T[] array, int offset, int count, T value)
        {
            if (MathUtil.IsOutOfBounds(offset, count, array.Length))
            {
                ThrowHelper.ThrowIndexOutOfRangeException_Index(offset, count, array.Length);
            }

            Span<T> span = array.AsSpan(offset, count);
            span.Fill(value);
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