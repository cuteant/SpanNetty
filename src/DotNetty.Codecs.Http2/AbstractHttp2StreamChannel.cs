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
    using System.Diagnostics;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    internal interface IHasParentContext
    {
        IChannelHandlerContext Context { get; }
    }

    abstract partial class AbstractHttp2StreamChannel : DefaultAttributeMap, IHttp2StreamChannel
    {
        private static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<Http2MultiplexCodec>();

        private static readonly ChannelMetadata CodecMetadata = new ChannelMetadata(false, 16);
        private readonly Http2StreamChannelConfiguration _config;
        private readonly Http2ChannelUnsafe _channelUnsafe;
        private readonly IChannelId _channelId;
        private readonly IChannelPipeline _pipeline;
        private readonly DefaultHttp2FrameStream _stream;
        private readonly IPromise _closePromise;

        private int v_registered = SharedConstants.False;
        private bool InternalRegistered
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => SharedConstants.False < (uint)Volatile.Read(ref v_registered);
            set => Interlocked.Exchange(ref v_registered, value ? SharedConstants.True : SharedConstants.False);
        }

        // volatile
        private long v_totalPendingSize;
        private int v_unwritable;

        // Cached to reduce GC
        private Action<object> _fireChannelWritabilityChangedTask;

        private int v_outboundClosed = SharedConstants.False;
        internal bool OutboundClosed
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => SharedConstants.False < (uint)Volatile.Read(ref v_outboundClosed);
            set => Interlocked.Exchange(ref v_outboundClosed, value ? SharedConstants.True : SharedConstants.False);
        }

        private int _flowControlledBytes;

        /// <summary>
        /// This variable represents if a read is in progress for the current channel or was requested.
        /// Note that depending upon the <see cref="IRecvByteBufAllocator"/> behavior a read may extend beyond the
        /// <see cref="Http2ChannelUnsafe.BeginRead()"/> method scope. The <see cref="Http2ChannelUnsafe.BeginRead()"/> loop may
        /// drain all pending data, and then if the parent channel is reading this channel may still accept frames.
        /// </summary>
        private ReadStatus _readStatus = ReadStatus.Idle;

        private Deque<object> _inboundBuffer;

        /// <summary>
        /// <c>true</c> after the first HEADERS frame has been written
        /// </summary>
        private bool _firstFrameWritten;
        private bool _readCompletePending;

        public AbstractHttp2StreamChannel(DefaultHttp2FrameStream stream, int id, IChannelHandler inboundHandler, IHasParentContext parentContext)
        {
            if (parentContext is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.parentContext); }

            _stream = stream;
            stream.Attachment = this;

            _config = new Http2StreamChannelConfiguration(this);
            _channelUnsafe = new Http2ChannelUnsafe(this);
            _pipeline = new Http2ChannelPipeline(this);

            _closePromise = _pipeline.NewPromise();
            _channelId = new Http2StreamChannelId(parentContext.Context.Channel.Id, id); // Parent.Id

            if (inboundHandler is object)
            {
                // Add the handler to the pipeline now that we are registered.
                _ = _pipeline.AddLast(inboundHandler);
            }
        }

        public IHttp2FrameStream Stream => _stream;

        internal void StreamClosed()
        {
            _channelUnsafe.ReadEOS = true;
            // Attempt to drain any queued data from the queue and deliver it to the application before closing this
            // channel.
            _channelUnsafe.DoBeginRead();
        }

        public ChannelMetadata Metadata => CodecMetadata;

        public IChannelConfiguration Configuration => _config;

        [Obsolete("Please use IsOpen instead.")]
        public bool Open => IsOpen;

        public bool IsOpen
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => !_closePromise.IsCompleted;
        }

        [Obsolete("Please use IsActive instead.")]
        public bool Active => IsActive;

        public bool IsActive => IsOpen;

        public bool IsWritable => 0u >= (uint)Volatile.Read(ref v_unwritable);

        public IChannelId Id => _channelId;

        public IEventLoop EventLoop => Parent.EventLoop;

        public IChannel Parent => ParentContext.Channel;

        [Obsolete("Please use IsRegistered instead.")]
        public bool Registered => IsRegistered;

        public bool IsRegistered => InternalRegistered;

        public EndPoint LocalAddress => Parent.LocalAddress;

        public EndPoint RemoteAddress => Parent.RemoteAddress;

        public Task CloseCompletion => _closePromise.Task;

        public long BytesBeforeUnwritable
        {
            get
            {
                long bytes = _config.WriteBufferHighWaterMark - Volatile.Read(ref v_totalPendingSize);
                // If bytes is negative we know we are not writable, but if bytes is non-negative we have to check
                // writability. Note that totalPendingSize and isWritable() use different volatile variables that are not
                // synchronized together. totalPendingSize will be updated before isWritable().
                if (bytes > 0L)
                {
                    return IsWritable ? bytes : 0L;
                }
                return 0L;
            }
        }

        public long BytesBeforeWritable
        {
            get
            {
                long bytes = Volatile.Read(ref v_totalPendingSize) - _config.WriteBufferLowWaterMark;
                // If bytes is negative we know we are writable, but if bytes is non-negative we have to check writability.
                // Note that totalPendingSize and isWritable() use different volatile variables that are not synchronized
                // together. totalPendingSize will be updated before isWritable().
                if (bytes > 0L)
                {
                    return IsWritable ? 0L : bytes;
                }
                return 0L;
            }
        }

        public IChannelUnsafe Unsafe => _channelUnsafe;

        public IChannelPipeline Pipeline => _pipeline;

        public IByteBufferAllocator Allocator => _config.Allocator;

        public IChannel Read()
        {
            _ = _pipeline.Read();
            return this;
        }

        public IChannel Flush()
        {
            _ = _pipeline.Flush();
            return this;
        }

        public Task BindAsync(EndPoint localAddress)
        {
            return _pipeline.BindAsync(localAddress);
        }

        public Task ConnectAsync(EndPoint remoteAddress)
        {
            return _pipeline.ConnectAsync(remoteAddress);
        }

        public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            return _pipeline.ConnectAsync(remoteAddress, localAddress);
        }

        public Task DisconnectAsync()
        {
            return _pipeline.DisconnectAsync();
        }

        public Task DisconnectAsync(IPromise promise)
        {
            return _pipeline.DisconnectAsync(promise);
        }

        public Task CloseAsync()
        {
            return _pipeline.CloseAsync();
        }

        public Task CloseAsync(IPromise promise)
        {
            return _pipeline.CloseAsync(promise);
        }

        public Task DeregisterAsync()
        {
            return _pipeline.DeregisterAsync();
        }

        public Task DeregisterAsync(IPromise promise)
        {
            return _pipeline.DeregisterAsync(promise);
        }

        public Task WriteAndFlushAsync(object message)
        {
            return _pipeline.WriteAndFlushAsync(message);
        }

        public Task WriteAndFlushAsync(object message, IPromise promise)
        {
            return _pipeline.WriteAndFlushAsync(message, promise);
        }

        public Task WriteAsync(object message)
        {
            return _pipeline.WriteAsync(message);
        }

        public Task WriteAsync(object message, IPromise promise)
        {
            return _pipeline.WriteAsync(message, promise);
        }

        public IPromise NewPromise()
        {
            return _pipeline.NewPromise();
        }

        public IPromise NewPromise(object state)
        {
            return _pipeline.NewPromise(state);
        }

        public IPromise VoidPromise()
        {
            return _pipeline.VoidPromise();
        }

        public override int GetHashCode()
        {
            return _channelId.GetHashCode();
        }

        public int CompareTo(IChannel other)
        {
            if (ReferenceEquals(this, other)) { return 0; }
            return _channelId.CompareTo(other.Id);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj);
        }

        public bool Equals(IChannel other)
        {
            return ReferenceEquals(this, other);
        }

        public override string ToString()
        {
            return Parent.ToString() + "(H2 - " + _stream + ')';
        }

        /// <summary>
        /// Receive a read message. This does not notify handlers unless a read is in progress on the channel.
        /// </summary>
        /// <param name="frame"></param>
        internal void FireChildRead(IHttp2Frame frame)
        {
            Debug.Assert(EventLoop.InEventLoop);
            if (!IsActive)
            {
                _ = ReferenceCountUtil.Release(frame);
            }
            else if (_readStatus != ReadStatus.Idle)
            {
                // If a read is in progress or has been requested, there cannot be anything in the queue,
                // otherwise we would have drained it from the queue and processed it during the read cycle.
                Debug.Assert(_inboundBuffer is null || _inboundBuffer.IsEmpty);
                var allocHandle = _channelUnsafe.RecvBufAllocHandle;
                _channelUnsafe.DoRead0(frame, allocHandle);
                // We currently don't need to check for readEOS because the parent channel and child channel are limited
                // to the same EventLoop thread. There are a limited number of frame types that may come after EOS is
                // read (unknown, reset) and the trade off is less conditionals for the hot path (headers/data) at the
                // cost of additional readComplete notifications on the rare path.
                if (allocHandle.ContinueReading())
                {
                    MaybeAddChannelToReadCompletePendingQueue();
                }
                else
                {
                    _channelUnsafe.NotifyReadComplete(allocHandle, true);
                }
            }
            else
            {
                if (_inboundBuffer is null)
                {
                    _inboundBuffer = new Deque<object>(4);
                }
                _inboundBuffer.AddLast​(frame);
            }
        }

        internal void FireChildReadComplete()
        {
            Debug.Assert(EventLoop.InEventLoop);
            Debug.Assert(_readStatus != ReadStatus.Idle || !_readCompletePending);
            _channelUnsafe.NotifyReadComplete(_channelUnsafe.RecvBufAllocHandle, false);
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        private void MaybeAddChannelToReadCompletePendingQueue()
        {
            if (!_readCompletePending)
            {
                _readCompletePending = true;
                AddChannelToReadCompletePendingQueue();
            }
        }

        protected virtual void Flush0(IChannelHandlerContext ctx)
        {
            _ = ctx.Flush();
        }

        protected virtual Task InternalWriteAsync(IChannelHandlerContext ctx, object msg)
        {
            var promise = ctx.NewPromise();
            _ = ctx.WriteAsync(msg, promise);
            return promise.Task;
        }

        protected abstract bool IsParentReadInProgress { get; }
        protected abstract void AddChannelToReadCompletePendingQueue();
        protected abstract IChannelHandlerContext ParentContext { get; }
    }
}
