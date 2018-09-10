// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Threading;
    using DotNetty.Buffers;

    /// <summary>
    ///     Default implementation of <see cref="IMaxMessagesRecvByteBufAllocator" /> which respects
    ///     <see cref="IChannelConfiguration.AutoRead" />
    ///     and also prevents overflow.
    /// </summary>
    public abstract class DefaultMaxMessagesRecvByteBufAllocator : IMaxMessagesRecvByteBufAllocator
    {
        int maxMessagesPerRead;
        int respectMaybeMoreData = Constants.True;

        protected DefaultMaxMessagesRecvByteBufAllocator()
            : this(1)
        {
        }

        protected DefaultMaxMessagesRecvByteBufAllocator(int maxMessagesPerRead)
        {
            this.MaxMessagesPerRead = maxMessagesPerRead;
        }

        public int MaxMessagesPerRead
        {
            get { return Volatile.Read(ref this.maxMessagesPerRead); }
            set
            {
                if (value <= 0) { ThrowHelper.ThrowArgumentException_Positive(value, ExceptionArgument.value); }
                Interlocked.Exchange(ref this.maxMessagesPerRead, value);
            }
        }

        public bool RespectMaybeMoreData
        {
            get => Constants.True == Volatile.Read(ref this.respectMaybeMoreData);
            set => Interlocked.Exchange(ref this.respectMaybeMoreData, value ? Constants.True : Constants.False);
        }

        public abstract IRecvByteBufAllocatorHandle NewHandle();

        /// <summary>Focuses on enforcing the maximum messages per read condition for <see cref="ContinueReading" />.</summary>
        protected abstract class MaxMessageHandle<T> : IRecvByteBufAllocatorHandle
            where T : IMaxMessagesRecvByteBufAllocator
        {
            protected readonly T Owner;
            IChannelConfiguration config;
            int maxMessagePerRead;
            bool respectMaybeMoreData;
            int totalMessages;
            int totalBytesRead;
            int lastBytesRead;

            protected MaxMessageHandle(T owner)
            {
                this.Owner = owner;
            }

            public abstract int Guess();

            /// <summary>Only <see cref="M:IChannelConfiguration.MaxMessagesPerRead" /> is used.</summary>
            public void Reset(IChannelConfiguration config)
            {
                this.config = config;
                this.maxMessagePerRead = this.Owner.MaxMessagesPerRead;
                this.respectMaybeMoreData = this.Owner.RespectMaybeMoreData;
                this.totalMessages = this.totalBytesRead = 0;
            }

            public IByteBuffer Allocate(IByteBufferAllocator alloc) => alloc.Buffer(this.Guess());

            public void IncMessagesRead(int amt) => this.totalMessages += amt;

            public virtual int LastBytesRead
            {
                get { return this.lastBytesRead; }
                set
                {
                    this.lastBytesRead = value;
                    if (value > 0)
                    {
                        this.totalBytesRead += value;
                    }
                }
            }

            public virtual bool ContinueReading()
            {
                return this.config.AutoRead
                    && (!this.respectMaybeMoreData || this.AttemptedBytesRead == this.lastBytesRead)
                    && this.totalMessages < this.maxMessagePerRead
                    && this.totalBytesRead < int.MaxValue;
            }

            public virtual void ReadComplete()
            {
            }

            public virtual int AttemptedBytesRead { get; set; }

            protected int TotalBytesRead() => this.totalBytesRead >= 0 ? this.totalBytesRead : int.MaxValue;
        }
    }
}