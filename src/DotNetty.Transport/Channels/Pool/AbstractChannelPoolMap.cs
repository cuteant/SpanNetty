// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    public abstract class AbstractChannelPoolMap<TKey, TPool> : IChannelPoolMap<TKey, TPool>
        where TPool : IChannelPool
    {
        readonly ConcurrentDictionary<TKey, TPool> map;

        public AbstractChannelPoolMap()
        {
            this.map = new ConcurrentDictionary<TKey, TPool>();
        }

        public AbstractChannelPoolMap(IEqualityComparer<TKey> comparer)
        {
            this.map = new ConcurrentDictionary<TKey, TPool>(comparer);
        }

        public TPool Get(TKey key)
        {
            if (key is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key); }

            if (!this.map.TryGetValue(key, out TPool pool))
            {
                pool = this.NewPool(key);
                TPool old = this.map.GetOrAdd(key, pool);
                if (!ReferenceEquals(old, pool))
                {
                    // We need to destroy the newly created pool as we not use it.
                    pool.Dispose();
                    pool = old;
                }
            }

            return pool;
        }

        /// <summary>
        /// Removes the <see cref="IChannelPool"/> from this <see cref="AbstractChannelPoolMap{TKey, TPool}"/>.
        /// </summary>
        /// <param name="key">The key to remove. Must not be null.</param>
        /// <returns><c>true</c> if removed, otherwise <c>false</c>.</returns>
        public bool Remove(TKey key)
        {
            if (key is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key); }
            if (this.map.TryRemove(key, out TPool pool))
            {
                pool.Dispose();
                return true;
            }
            return false;
        }

        /*public final Iterator<Entry<K, P>> iterator() {
            return new ReadOnlyIterator<Entry<K, P>>(this.map.entrySet().iterator());
        }*/

        /// <summary>
        /// Returns the number of <see cref="IChannelPool"/>s currently in this <see cref="AbstractChannelPoolMap{TKey, TPool}"/>.
        /// </summary>
        public int Count => this.map.Count;

        /// <summary>
        /// Returns <c>true</c> if the <see cref="AbstractChannelPoolMap{TKey, TPool}"/> is empty, otherwise <c>false</c>.
        /// </summary>
        public bool IsEmpty => 0u >= (uint)this.map.Count;

        public bool Contains(TKey key)
        {
            if (key is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key); }
            return this.map.ContainsKey(key);
        }

        /// <summary>
        /// Called once a new <see cref="IChannelPool"/> needs to be created as none exists yet for the <paramref name="key"/>.
        /// </summary>
        /// <param name="key">The <typeparamref name="TKey"/> to create a new <typeparamref name="TPool"/> for.</param>
        /// <returns>The new <typeparamref name="TPool"/> corresponding to the given <typeparamref name="TKey"/>.</returns>
        protected abstract TPool NewPool(TKey key);

        public void Dispose()
        {
            foreach (TKey key in this.map.Keys)
                this.Remove(key);
        }
    }
}
