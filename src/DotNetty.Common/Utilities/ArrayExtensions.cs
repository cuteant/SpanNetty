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
        public static readonly byte[] ZeroBytes = new byte[0];

        public static T[] Slice<T>(this T[] array, int length)
        {
            if (null == array) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array); }

            if (length > array.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_Slice(length, array.Length);
            }
            return Slice(array, 0, length);
        }

        public static T[] Slice<T>(this T[] array, int index, int length)
        {
            if (null == array) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array); }

            if (index + length > array.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_Slice(index, length, array.Length);
            }
            var result = new T[length];
            Array.Copy(array, index, result, 0, length);
            return result;
        }

        public static void SetRange<T>(this T[] array, int index, T[] src) => SetRange(array, index, src, 0, src.Length);

        public static void SetRange<T>(this T[] array, int index, T[] src, int srcIndex, int srcLength)
        {
            if (null == array) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array); }
            if (null == src) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.src); }

            if (index + srcLength > array.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_SetRange_Index(index, srcLength, array.Length);
            }
            if (srcIndex + srcLength > src.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_SetRange_SrcIndex(srcIndex, srcLength, src.Length);
            }

            Array.Copy(src, srcIndex, array, index, srcLength);
        }

        public static void Fill<T>(this T[] array, T value)
        {
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = value;
            }
        }

        public static void Fill<T>(this T[] array, int offset, int count, T value)
        {
            if (MathUtil.IsOutOfBounds(offset, count, array.Length))
            {
                ThrowHelper.ThrowIndexOutOfRangeException_Index(offset, count, array.Length);
            }

            for (int i = offset; i < count + offset; i++)
            {
                array[i] = value;
            }
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