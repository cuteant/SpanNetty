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
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using DotNetty.Common.Internal;

    sealed class EmptyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        static readonly NotImplementedException Error = new NotImplementedException();

        public static readonly EmptyDictionary<TKey, TValue> Instance = new EmptyDictionary<TKey, TValue>();

        private EmptyDictionary() { }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return Enumerable.Empty<KeyValuePair<TKey, TValue>>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(KeyValuePair<TKey, TValue> item) { }

        public void Clear() { }

        public bool Contains(KeyValuePair<TKey, TValue> item) => false;

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) { }

        public bool Remove(KeyValuePair<TKey, TValue> item) => false;

        public int Count => 0;

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value) { }

        public bool ContainsKey(TKey key) => false;

        public bool Remove(TKey key) => false;

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default;
            return false;
        }

        public TValue this[TKey key]
        {
            get => default;
            set { }
        }

        public ICollection<TKey> Keys => EmptyArray<TKey>.Instance;

        public ICollection<TValue> Values => EmptyArray<TValue>.Instance;
    }
}