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
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Internal;

    sealed class MaxCapacityQueue<T>
    {
        private readonly Deque<T> _queue;
        private readonly int _maxCapacity;

        public MaxCapacityQueue(int maxCapacity)
        {
            _queue = new Deque<T>(8);
            _maxCapacity = maxCapacity;
        }

        public bool TryEnqueue(T item)
        {
            var queue = _queue;
            if ((uint)_maxCapacity > (uint)queue.Count)
            {
                queue.AddLast​(item);
                return true;
            }
            return false;
        }

        public bool TryDequeue(out T result) => _queue.TryRemoveFirst(out result);

        public void Clear() => _queue.Clear();
    }
}
