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
        private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(30);

        private IByteBufferAllocator _allocator = ByteBufferUtil.DefaultAllocator;
        private IRecvByteBufAllocator _recvByteBufAllocator = FixedRecvByteBufAllocator.Default;
        private IMessageSizeEstimator _messageSizeEstimator = DefaultMessageSizeEstimator.Default;

        private int _autoRead = SharedConstants.True;
        private int _autoClose = SharedConstants.True;
        private int _writeSpinCount = 16;
        private int _writeBufferHighWaterMark = 64 * 1024;
        private int _writeBufferLowWaterMark = 32 * 1024;
        private long _connectTimeout = DefaultConnectTimeout.Ticks;

        protected readonly IChannel Channel;

        public DefaultChannelConfiguration(IChannel channel)
            : this(channel, new AdaptiveRecvByteBufAllocator())
        {
        }

        public DefaultChannelConfiguration(IChannel channel, IRecvByteBufAllocator allocator)
        {
            if (channel is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.channel); }

            Channel = channel;
            if (allocator is IMaxMessagesRecvByteBufAllocator maxMessagesAllocator)
            {
                maxMessagesAllocator.MaxMessagesPerRead = channel.Metadata.DefaultMaxMessagesPerRead;
            }
            else if (allocator is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.allocator);
            }
            RecvByteBufAllocator = allocator;
        }

        public virtual T GetOption<T>(ChannelOption<T> option)
        {
            if (option is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.option); }

            if (ChannelOption.ConnectTimeout.Equals(option))
            {
                return (T)(object)ConnectTimeout; // no boxing will happen, compiler optimizes away such casts
            }
            if (ChannelOption.WriteSpinCount.Equals(option))
            {
                return (T)(object)WriteSpinCount;
            }
            if (ChannelOption.Allocator.Equals(option))
            {
                return (T)Allocator;
            }
            if (ChannelOption.RcvbufAllocator.Equals(option))
            {
                return (T)RecvByteBufAllocator;
            }
            if (ChannelOption.AutoRead.Equals(option))
            {
                return (T)(object)AutoRead;
            }
            if (ChannelOption.AutoClose.Equals(option))
            {
                return (T)(object)AutoClose;
            }
            if (ChannelOption.WriteBufferHighWaterMark.Equals(option))
            {
                return (T)(object)WriteBufferHighWaterMark;
            }
            if (ChannelOption.WriteBufferLowWaterMark.Equals(option))
            {
                return (T)(object)WriteBufferLowWaterMark;
            }
            if (ChannelOption.MessageSizeEstimator.Equals(option))
            {
                return (T)MessageSizeEstimator;
            }
            if (ChannelOption.MaxMessagesPerRead.Equals(option))
            {
                if (RecvByteBufAllocator is IMaxMessagesRecvByteBufAllocator)
                {
                    return (T)(object)MaxMessagesPerRead;
                }
            }
            return default;
        }

        public bool SetOption(ChannelOption option, object value) => option.Set(this, value);

        public virtual bool SetOption<T>(ChannelOption<T> option, T value)
        {
            Validate(option, value);

            if (ChannelOption.ConnectTimeout.Equals(option))
            {
                ConnectTimeout = (TimeSpan)(object)value;
            }
            else if (ChannelOption.WriteSpinCount.Equals(option))
            {
                WriteSpinCount = (int)(object)value;
            }
            else if (ChannelOption.Allocator.Equals(option))
            {
                Allocator = (IByteBufferAllocator)value;
            }
            else if (ChannelOption.RcvbufAllocator.Equals(option))
            {
                RecvByteBufAllocator = (IRecvByteBufAllocator)value;
            }
            else if (ChannelOption.AutoRead.Equals(option))
            {
                AutoRead = (bool)(object)value;
            }
            else if (ChannelOption.AutoClose.Equals(option))
            {
                AutoClose = (bool)(object)value;
            }
            else if (ChannelOption.WriteBufferHighWaterMark.Equals(option))
            {
                WriteBufferHighWaterMark = (int)(object)value;
            }
            else if (ChannelOption.WriteBufferLowWaterMark.Equals(option))
            {
                WriteBufferLowWaterMark = (int)(object)value;
            }
            else if (ChannelOption.MessageSizeEstimator.Equals(option))
            {
                MessageSizeEstimator = (IMessageSizeEstimator)value;
            }
            else if (ChannelOption.MaxMessagesPerRead.Equals(option))
            {
                if (RecvByteBufAllocator is IMaxMessagesRecvByteBufAllocator)
                {
                    MaxMessagesPerRead = (int)(object)value;
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
            get { return new TimeSpan(Volatile.Read(ref _connectTimeout)); }
            set
            {
                if (value < TimeSpan.Zero) { ThrowHelper.ThrowArgumentException_MustBeGreaterThanZero(value, ExceptionArgument.value); }
                Interlocked.Exchange(ref _connectTimeout, value.Ticks);
            }
        }

        public IByteBufferAllocator Allocator
        {
            get { return Volatile.Read(ref _allocator); }
            set
            {
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                Interlocked.Exchange(ref _allocator, value);
            }
        }

        public IRecvByteBufAllocator RecvByteBufAllocator
        {
            get { return Volatile.Read(ref _recvByteBufAllocator); }
            set
            {
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                Interlocked.Exchange(ref _recvByteBufAllocator, value);
            }
        }

        public virtual IMessageSizeEstimator MessageSizeEstimator
        {
            get { return Volatile.Read(ref _messageSizeEstimator); }
            set
            {
                if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                Interlocked.Exchange(ref _messageSizeEstimator, value);
            }
        }

        public bool AutoRead
        {
            get { return SharedConstants.False < (uint)Volatile.Read(ref _autoRead); }
            set
            {
#pragma warning disable 420 // atomic exchange is ok
                bool oldAutoRead = SharedConstants.False < (uint)Interlocked.Exchange(ref _autoRead, value ? SharedConstants.True : SharedConstants.False);
#pragma warning restore 420
                if (value && !oldAutoRead)
                {
                    Channel.Read();
                }
                else if (!value && oldAutoRead)
                {
                    AutoReadCleared();
                }
            }
        }

        protected virtual void AutoReadCleared()
        {
        }

        public bool AutoClose
        {
            get { return SharedConstants.False < (uint)Volatile.Read(ref _autoClose); }
            set { Interlocked.Exchange(ref _autoClose, value ? SharedConstants.True : SharedConstants.False); }
        }

        public virtual int WriteBufferHighWaterMark
        {
            get { return Volatile.Read(ref _writeBufferHighWaterMark); }
            set
            {
                if (value < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(value, ExceptionArgument.value); }
                if (value < Volatile.Read(ref _writeBufferLowWaterMark)) { ThrowHelper.ThrowArgumentOutOfRangeException(); }

                Interlocked.Exchange(ref _writeBufferHighWaterMark, value);
            }
        }

        public virtual int WriteBufferLowWaterMark
        {
            get { return Volatile.Read(ref _writeBufferLowWaterMark); }
            set
            {
                if (value < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(value, ExceptionArgument.value); }
                if (value > Volatile.Read(ref _writeBufferHighWaterMark)) { ThrowHelper.ThrowArgumentOutOfRangeException(); }

                Interlocked.Exchange(ref _writeBufferLowWaterMark, value);
            }
        }

        public int WriteSpinCount
        {
            get { return Volatile.Read(ref _writeSpinCount); }
            set
            {
                if (value < 1) { ThrowHelper.ThrowArgumentException_PositiveOrOne(value, ExceptionArgument.value); }

                Interlocked.Exchange(ref _writeSpinCount, value);
            }
        }

        public int MaxMessagesPerRead
        {
            get { return ((IMaxMessagesRecvByteBufAllocator)RecvByteBufAllocator).MaxMessagesPerRead; }
            set { ((IMaxMessagesRecvByteBufAllocator)RecvByteBufAllocator).MaxMessagesPerRead = value; }
        }
    }
}