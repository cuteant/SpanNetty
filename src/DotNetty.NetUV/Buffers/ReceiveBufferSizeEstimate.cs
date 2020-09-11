/*
 * Copyright (c) Johnny Z. All rights reserved.
 *
 *   https://github.com/StormHub/NetUV
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com)
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Buffers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using DotNetty.Common.Utilities;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.NetUV;

    internal sealed class ReceiveBufferSizeEstimate
    {
        private static readonly IInternalLogger Log = InternalLoggerFactory.GetInstance<ReceiveBufferSizeEstimate>();
        private const int DefaultMinimum = 64;
        private const int DefaultInitial = 1024;
        private const int DefaultMaximum = 65536;
        private const int IndexIncrement = 4;
        private const int IndexDecrement = 1;
        private static readonly int[] SizeTable;

        static ReceiveBufferSizeEstimate()
        {
            var sizeTable = new List<int>();
            for (int i = 16; i < 512; i += 16)
            {
                sizeTable.Add(i);
            }

            for (int i = 512; i > 0; i <<= 1)
            {
                sizeTable.Add(i);
            }

            SizeTable = sizeTable.ToArray();
        }

        private static int GetSizeTableIndex(int size)
        {
            for (int low = 0, high = SizeTable.Length - 1; ;)
            {
                if (high < low)
                {
                    return low;
                }
                if (high == low)
                {
                    return high;
                }

                int mid = (low + high).RightUShift(1);
                int a = SizeTable[mid];
                int b = SizeTable[mid + 1];
                if (size > b)
                {
                    low = mid + 1;
                }
                else if (size < a)
                {
                    high = mid - 1;
                }
                else if (size == a)
                {
                    return mid;
                }
                else
                {
                    return mid + 1;
                }
            }
        }

        private readonly int _minIndex;
        private readonly int _maxIndex;
        private int _index;
        private bool _decreaseNow;
        private int _receiveBufferSize;

        public ReceiveBufferSizeEstimate(int minimum = DefaultMinimum, int initial = DefaultInitial, int maximum = DefaultMaximum)
        {
            if ((uint)(minimum - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(minimum, ExceptionArgument.minimum); }
            if (initial < minimum) { ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.initial); }
            if (initial > maximum) { ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.maximum); }

            int min = GetSizeTableIndex(minimum);
            if (SizeTable[min] < minimum)
            {
                _minIndex = min + 1;
            }
            else
            {
                _minIndex = min;
            }

            int max = GetSizeTableIndex(maximum);
            if (SizeTable[max] > maximum)
            {
                _maxIndex = max - 1;
            }
            else
            {
                _maxIndex = max;
            }

            _index = GetSizeTableIndex(initial);
            _receiveBufferSize = SizeTable[_index];
        }

        internal IByteBuffer Allocate(PooledByteBufferAllocator allocator)
        {
            Debug.Assert(allocator is object);

#if DEBUG
            if (Log.DebugEnabled)
            {
                Log.Debug("{} allocate, estimated size = {}", nameof(ReceiveBufferSizeEstimate), _receiveBufferSize);
            }
#endif

            return allocator.Buffer(_receiveBufferSize);
        }

        internal void Record(int actualReadBytes)
        {
            if (actualReadBytes <= SizeTable[Math.Max(0, _index - IndexDecrement - 1)])
            {
                if (_decreaseNow)
                {
                    _index = Math.Max(_index - IndexDecrement, _minIndex);
                    _receiveBufferSize = SizeTable[_index];
                    _decreaseNow = false;
                }
                else
                {
                    _decreaseNow = true;
                }
            }
            else if (actualReadBytes >= _receiveBufferSize)
            {
                _index = Math.Min(_index + IndexIncrement, _maxIndex);
                _receiveBufferSize = SizeTable[_index];
                _decreaseNow = false;
            }

#if DEBUG
            if (Log.DebugEnabled)
            {
                Log.Debug("{} record actual size = {}, next size = {}", nameof(ReceiveBufferSizeEstimate), actualReadBytes, _receiveBufferSize);
            }
#endif
        }
    }
}
