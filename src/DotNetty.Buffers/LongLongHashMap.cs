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
    using DotNetty.Common.Utilities;

    internal sealed class LongLongHashMap
    {
        private const int MASK_TEMPLATE = ~1;
        private int _mask;
        private long[] _array;
        private int _maxProbe;
        private long _zeroVal;
        private readonly long _emptyVal;

        public LongLongHashMap(long emptyVal)
        {
            _emptyVal = emptyVal;
            _zeroVal = emptyVal;
            int initialSize = 32;
            _array = new long[initialSize];
            _mask = initialSize - 1;
            ComputeMaskAndProbe();
        }

        public long Put(long key, long value)
        {
            if (0ul >= (ulong)key)
            {
                long prev = _zeroVal;
                _zeroVal = value;
                return prev;
            }

            for (; ; )
            {
                int index = Index(key);
                for (int i = 0; i < _maxProbe; i++)
                {
                    long existing = _array[index];
                    if (existing == key || 0ul >= (ulong)existing)
                    {
                        long prev = 0ul >= (ulong)existing ? _emptyVal : _array[index + 1];
                        _array[index] = key;
                        _array[index + 1] = value;
                        for (; i < _maxProbe; i++)
                        { // Nerf any existing misplaced entries.
                            index = index + 2 & _mask;
                            if (_array[index] == key)
                            {
                                _array[index] = 0;
                                prev = _array[index + 1];
                                break;
                            }
                        }
                        return prev;
                    }
                    index = index + 2 & _mask;
                }
                Expand(); // Grow array and re-hash.
            }
        }

        public void Remove(long key)
        {
            if (0ul >= (ulong)key)
            {
                _zeroVal = _emptyVal;
                return;
            }
            int index = Index(key);
            for (int i = 0; i < _maxProbe; i++)
            {
                long existing = _array[index];
                if (existing == key)
                {
                    _array[index] = 0;
                    break;
                }
                index = index + 2 & _mask;
            }
        }

        public long Get(long key)
        {
            if (0ul >= (ulong)key)
            {
                return _zeroVal;
            }
            int index = Index(key);
            for (int i = 0; i < _maxProbe; i++)
            {
                long existing = _array[index];
                if (existing == key)
                {
                    return _array[index + 1];
                }
                index = index + 2 & _mask;
            }
            return _emptyVal;
        }

        private int Index(long key)
        {
            // Hash with murmur64, and mask.
            key ^= key.RightUShift(33);
            key *= -49064778989728563L; // 0xff51afd7ed558ccdL
            key ^= key.RightUShift(33);
            key *= -4265267296055464877L; // 0xc4ceb9fe1a85ec53L
            key ^= key.RightUShift(33);
            return (int)key & _mask;
        }

        private void Expand()
        {
            long[] prev = _array;
            _array = new long[prev.Length * 2];
            ComputeMaskAndProbe();
            for (int i = 0; i < prev.Length; i += 2)
            {
                long key = prev[i];
                if (key != 0)
                {
                    long val = prev[i + 1];
                    Put(key, val);
                }
            }
        }

        private void ComputeMaskAndProbe()
        {
            int length = _array.Length;
            _mask = length - 1 & MASK_TEMPLATE;
            _maxProbe = (int)Math.Log(length);
        }
    }
}
