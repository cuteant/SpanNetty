/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
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

    internal sealed class LongPriorityQueue
    {
        private const int DefaultCapacity = 9;
        public const long NO_VALUE = -1L;

        private long[] _array = new long[DefaultCapacity];
        private int _size;

        public void Offer(long handle)
        {
            if (0ul >= (ulong)(NO_VALUE - handle))
            {
                ThrowHelper.ThrowArgumentException_NoValueCannotBeAdded();
            }
            _size++;
            if (_size == _array.Length)
            {
                // Grow queue capacity.
                Array.Resize(ref _array, 2 * _array.Length);
            }
            _array[_size] = handle;
            Lift(_size);
        }

        public void Remove(long value)
        {
            for (int i = 1; i <= _size; i++)
            {
                if (_array[i] == value)
                {
                    _array[i] = _array[_size--];
                    Lift(i);
                    Sink(i);
                    return;
                }
            }
        }

        public long Peek()
        {
            if (0u >= (uint)_size)
            {
                return NO_VALUE;
            }
            return _array[1];
        }

        public long Poll()
        {
            if (0u >= (uint)_size)
            {
                return NO_VALUE;
            }
            long val = _array[1];
            _array[1] = _array[_size];
            _array[_size] = 0;
            _size--;
            Sink(1);
            return val;
        }

        public bool IsEmpty()
        {
            return 0u >= (uint)_size;
        }

        private void Lift(int index)
        {
            int parentIndex;
            while (index > 1 && Subord(parentIndex = index >> 1, index))
            {
                Swap(index, parentIndex);
                index = parentIndex;
            }
        }

        private void Sink(int index)
        {
            int child;
            while ((child = index << 1) <= _size)
            {
                if (child < _size && Subord(child, child + 1))
                {
                    child++;
                }
                if (!Subord(index, child))
                {
                    break;
                }
                Swap(index, child);
                index = child;
            }
        }

        private bool Subord(int a, int b)
        {
            return _array[a] > _array[b];
        }

        private void Swap(int a, int b)
        {
            long value = _array[a];
            _array[a] = _array[b];
            _array[b] = value;
        }
    }
}
