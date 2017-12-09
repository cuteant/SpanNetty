// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    /// <summary>Allows to map {@link ChannelPool} implementations to a specific key.</summary>
    /// <typeparam name="TKey">the type of the key</typeparam>
    /// <typeparam name="TPool">the type of the channel pool</typeparam>
    public interface IChannelPoolMap<TKey, TPool>
        where TPool : IChannelPool
    {
        /**
         * Return the {@link ChannelPool} for the {@code code}. This will never return {@code null},
         * but create a new {@link ChannelPool} if non exists for they requested {@code key}.
         *
         * Please note that {@code null} keys are not allowed.
         */
        TPool Get(TKey key);

        /**
         * Returns {@code true} if a {@link ChannelPool} exists for the given {@code key}.
         *
         * Please note that {@code null} keys are not allowed.
         */
        bool Contains(TKey key);
    }
}