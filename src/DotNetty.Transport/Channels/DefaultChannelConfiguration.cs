// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Threading;
    using DotNetty.Buffers;

    /// <summary>
    ///     Shared configuration for SocketAsyncChannel. Provides access to pre-configured resources like ByteBuf allocator and
    ///     IO buffer pools
    /// </summary>
    public class DefaultChannelConfiguration : IChannelConfiguration
    {
        static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(30);

        IByteBufferAllocator allocator = ByteBufferUtil.DefaultAllocator;
        IRecvByteBufAllocator recvByteBufAllocator = FixedRecvByteBufAllocator.Default;
        IMessageSizeEstimator messageSizeEstimator = DefaultMessageSizeEstimator.Default;

        int autoRead = SharedConstants.True;
        int autoClose = SharedConstants.True;
        int writeSpinCount = 16;
        int writeBufferHighWaterMark = 64 * 1024;
        int writeBufferLowWaterMark = 32 * 1024;
        long connectTimeout = DefaultConnectTimeout.Ticks;

        protected readonly IChannel Channel;

        public DefaultChannelConfiguration(IChannel channel)
            : this(channel, new AdaptiveRecvByteBufAllocator())
        {
        }

        public DefaultChannelConfiguration(IChannel channel, IRecvByteBufAllocator allocator)
        {
            if (channel is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.channel); }

            this.Channel = channel;
            if (allocator is IMaxMessagesRecvByteBufAllocator maxMessagesAllocator)
            {
                maxMessagesAllocator.MaxMessagesPerRead = channel.Metadata.DefaultMaxMessagesPerRead;
            }
            else if (allocator is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.allocator);
            }
            this.RecvByteBufAllocator = allocator;
        }

        public virtual T GetOption<T>(ChannelOption<T> option)
        {
            if (option is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.option); }

            if (ChannelOption.ConnectTimeout.Equals(option))
            {
                return (T)(object)this.ConnectTimeout; // no boxing will happen, compiler optimizes away such casts
            }
            if (ChannelOption.WriteSpinCount.Equals(option))
            {
                return (T)(object)this.WriteSpinCount;
            }
            if (ChannelOption.Allocator.Equals(option))
            {
                return (T)this.Allocator;
            }
            if (ChannelOption.RcvbufAllocator.Equals(option))
            {
                return (T)this.RecvByteBufAllocator;
            }
            if (ChannelOption.AutoRead.Equals(option))
            {
                return (T)(object)this.AutoRead;
            }
            if (ChannelOption.AutoClose.Equals(option))
            {
                return (T)(object)this.AutoClose;
            }
            if (ChannelOption.WriteBufferHighWaterMark.Equals(option))
            {
                return (T)(object)this.WriteBufferHighWaterMark;
            }
            if (ChannelOption.WriteBufferLowWaterMark.Equals(option))
            {
                return (T)(object)this.WriteBufferLowWaterMark;
            }
            if (ChannelOption.MessageSizeEstimator.Equals(option))
            {
                return (T)this.MessageSizeEstimator;
            }
            if (ChannelOption.MaxMessagesPerRead.Equals(option))
            {
                if (RecvByteBufAllocator is IMaxMessagesRecvByteBufAllocator)
                {
                    return (T)(object)this.MaxMessagesPerRead;
                }
            }
            return default;
        }

        public bool SetOption(ChannelOption option, object value) => option.Set(this, value);

        public virtual bool SetOption<T>(ChannelOption<T> option, T value)
        {
            this.Validate(option, value);

            if (ChannelOption.ConnectTimeout.Equals(option))
            {
                this.ConnectTimeout = (TimeSpan)(object)value;
            }
            else if (ChannelOption.WriteSpinCount.Equals(option))
            {
                this.WriteSpinCount = (int)(object)value;
            }
            else if (ChannelOption.Allocator.Equals(option))
            {
                this.Allocator = (IByteBufferAllocator)value;
            }
            else if (ChannelOption.RcvbufAllocator.Equals(option))
            {
                this.RecvByteBufAllocator = (IRecvByteBufAllocator)value;
            }
            else if (ChannelOption.AutoRead.Equals(option))
            {
                this.AutoRead = (bool)(object)value;
            }
            else if (ChannelOption.AutoClose.Equals(option))
            {
                this.AutoClose = (bool)(object)value;
            }
            else if (ChannelOption.WriteBufferHighWaterMark.Equals(option))
            {
                this.WriteBufferHighWaterMark = (int)(object)value;
            }
            else if (ChannelOption.WriteBufferLowWaterMark.Equals(option))
            {
                this.WriteBufferLowWaterMark = (int)(object)value;
            }
            else if (ChannelOption.MessageSizeEstimator.Equals(option))
            {
                this.MessageSizeEstimator = (IMessageSizeEstimator)value;
            }
            else if (ChannelOption.MaxMessagesPerRead.Equals(option))
            {
                if (RecvByteBufAllocator is IMaxMessagesRecvByteBufAllocator)
                {
                    this.MaxMessagesPerRead = (int)(object)value;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        protected virtual void Validate<T>(ChannelOption<T> option, T value)
        {
            if (option is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.option); }
            option.Validate(value);
        }

        public TimeSpan ConnectTimeout
        {
            get { return new TimeSpan(Volatile.Read(ref this.connectTimeout)); }
            set
            {
                if (value < TimeSpan.Zero) { ThrowHelper.ThrowArgumentException_MustBeGreaterThanZero(value, ExceptionArgument.value); }
                Interlocked.Exchange(ref this.connectTimeout, value.Ticks);
            }
        }

        public IByteBufferAllocator Allocator
        {
            get { return Volatile.Read(ref this.allocator); }
            set
            {
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                Interlocked.Exchange(ref this.allocator, value);
            }
        }

        public IRecvByteBufAllocator RecvByteBufAllocator
        {
            get { return Volatile.Read(ref this.recvByteBufAllocator); }
            set
            {
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                Interlocked.Exchange(ref this.recvByteBufAllocator, value);
            }
        }

        public virtual IMessageSizeEstimator MessageSizeEstimator
        {
            get { return Volatile.Read(ref this.messageSizeEstimator); }
            set
            {
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                Interlocked.Exchange(ref this.messageSizeEstimator, value);
            }
        }

        public bool AutoRead
        {
            get { return SharedConstants.True == Volatile.Read(ref this.autoRead); }
            set
            {
#pragma warning disable 420 // atomic exchange is ok
                bool oldAutoRead = SharedConstants.True == Interlocked.Exchange(ref this.autoRead, value ? SharedConstants.True : SharedConstants.False);
#pragma warning restore 420
                if (value && !oldAutoRead)
                {
                    this.Channel.Read();
                }
                else if (!value && oldAutoRead)
                {
                    this.AutoReadCleared();
                }
            }
        }

        protected virtual void AutoReadCleared()
        {
        }

        public bool AutoClose
        {
            get { return SharedConstants.True == Volatile.Read(ref this.autoClose); }
            set { Interlocked.Exchange(ref this.autoClose, value ? SharedConstants.True : SharedConstants.False); }
        }

        public virtual int WriteBufferHighWaterMark
        {
            get { return Volatile.Read(ref this.writeBufferHighWaterMark); }
            set
            {
                if (value < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(value, ExceptionArgument.value); }
                if (value < Volatile.Read(ref this.writeBufferLowWaterMark)) { ThrowHelper.ThrowArgumentOutOfRangeException(); }

                Interlocked.Exchange(ref this.writeBufferHighWaterMark, value);
            }
        }

        public virtual int WriteBufferLowWaterMark
        {
            get { return Volatile.Read(ref this.writeBufferLowWaterMark); }
            set
            {
                if (value < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(value, ExceptionArgument.value); }
                if (value > Volatile.Read(ref this.writeBufferHighWaterMark)) { ThrowHelper.ThrowArgumentOutOfRangeException(); }

                Interlocked.Exchange(ref this.writeBufferLowWaterMark, value);
            }
        }

        public int WriteSpinCount
        {
            get { return Volatile.Read(ref this.writeSpinCount); }
            set
            {
                if (value < 1) { ThrowHelper.ThrowArgumentException_PositiveOrOne(value, ExceptionArgument.value); }

                Interlocked.Exchange(ref this.writeSpinCount, value);
            }
        }

        public int MaxMessagesPerRead
        {
            get { return ((IMaxMessagesRecvByteBufAllocator)RecvByteBufAllocator).MaxMessagesPerRead; }
            set { ((IMaxMessagesRecvByteBufAllocator)RecvByteBufAllocator).MaxMessagesPerRead = value; }
        }
    }
}