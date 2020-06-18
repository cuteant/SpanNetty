// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Pool
{
    using System.Threading;

    public sealed class CountingChannelPoolHandler : IChannelPoolHandler
    {
        private int _channelCount;
        private int _acquiredCount;
        private int _releasedCount;
        
        public int ChannelCount => Volatile.Read(ref _channelCount);

        public int AcquiredCount => Volatile.Read(ref _acquiredCount);

        public int ReleasedCount => Volatile.Read(ref _releasedCount);

        public void ChannelCreated(IChannel ch) => Interlocked.Increment(ref _channelCount);

        public void ChannelReleased(IChannel ch) => Interlocked.Increment(ref _releasedCount);

        public void ChannelAcquired(IChannel ch) => Interlocked.Increment(ref _acquiredCount);
    }
}
