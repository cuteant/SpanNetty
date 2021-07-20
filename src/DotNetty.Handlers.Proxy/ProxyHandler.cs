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

using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Common.Concurrency;
using DotNetty.Common.Internal.Logging;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;

namespace DotNetty.Handlers.Proxy
{
    public abstract class ProxyHandler : ChannelDuplexHandler
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ProxyHandler>();
     
        /// <summary>
        /// The default connect timeout: 10 seconds.
        /// </summary>
        static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromMilliseconds(10000);

        /// <summary>
        /// A string that signifies 'no authentication' or 'anonymous'.
        /// </summary>
        protected const string AuthNone = "none";

        private readonly EndPoint _proxyAddress;
        private readonly TaskCompletionSource<IChannel> _connectPromise = new TaskCompletionSource<IChannel>();

        private volatile EndPoint _destinationAddress;
        private TimeSpan _connectTimeout = DefaultConnectTimeout;

        private IChannelHandlerContext _ctx;
        private PendingWriteQueue _pendingWrites;
        private bool _finished;
        private bool _suppressChannelReadComplete;
        private bool _flushedPrematurely;

        private IScheduledTask _connectTimeoutFuture;

        protected ProxyHandler(EndPoint proxyAddress)
        {
            _proxyAddress = proxyAddress ?? throw new ArgumentNullException(nameof(proxyAddress));
        }

        /// <summary>
        /// Returns the name of the proxy protocol in use.
        /// </summary>
        public abstract string Protocol { get; }

        /// <summary>
        /// Returns the name of the authentication scheme in use.
        /// </summary>
        public abstract string AuthScheme { get; }

        /// <summary>
        /// Returns the address of the proxy server.
        /// </summary>
        public EndPoint ProxyAddress => _proxyAddress;

        /// <summary>
        /// Returns the address of the destination to connect to via the proxy server.
        /// </summary>
        public EndPoint DestinationAddress => _destinationAddress;

        /// <summary>
        /// Returns {@code true} if and only if the connection to the destination has been established successfully.
        /// </summary>
        public bool Connected => _connectPromise.Task.Status == TaskStatus.RanToCompletion;

        /// <summary>
        /// Returns a {@link Future} that is notified when the connection to the destination has been established
        /// or the connection attempt has failed.
        /// </summary>
        public Task<IChannel> ConnectFuture => _connectPromise.Task;

        /// <summary>
        /// Connect timeout.  If the connection attempt to the destination does not finish within
        /// the timeout, the connection attempt will be failed.
        /// </summary>
        public TimeSpan ConnectTimeout
        {
            get => _connectTimeout;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    value = TimeSpan.Zero;
                }

                _connectTimeout = value;
            }
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            _ctx = ctx;

            AddCodec(ctx);

            if (ctx.Channel.IsActive)
            {
                // channelActive() event has been fired already, which means channelActive() will
                // not be invoked. We have to initialize here instead.
                SendInitialMessage(ctx);
            }
            else
            {
                // channelActive() event has not been fired yet.  channelOpen() will be invoked
                // and initialization will occur there.
            }
        }

        /// <summary>
        /// Adds the codec handlers required to communicate with the proxy server.
        /// </summary>
        protected abstract void AddCodec(IChannelHandlerContext ctx);

        /// <summary>
        /// Removes the encoders added in {@link #addCodec(IChannelHandlerContext)}.
        /// </summary>
        protected abstract void RemoveEncoder(IChannelHandlerContext ctx);

        /// <summary>
        /// Removes the decoders added in {@link #addCodec(IChannelHandlerContext)}.
        /// </summary>
        protected abstract void RemoveDecoder(IChannelHandlerContext ctx);

        public override Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress)
        {
            if (_destinationAddress != null)
            {
                return TaskUtil.FromException(new ConnectionPendingException());
            }

            _destinationAddress = remoteAddress;

            return _ctx.ConnectAsync(_proxyAddress, localAddress);
        }

        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            SendInitialMessage(ctx);
            ctx.FireChannelActive();
        }

        /// <summary>
        /// Sends the initial message to be sent to the proxy server. This method also starts a timeout task which marks
        /// the {@link #connectPromise} as failure if the connection attempt does not success within the timeout.
        /// </summary>
        void SendInitialMessage(IChannelHandlerContext ctx)
        {
            var connectTimeout = _connectTimeout;
            if (connectTimeout > TimeSpan.Zero)
            {
                _connectTimeoutFuture = ctx.Executor.Schedule(ConnectTimeout, connectTimeout);
            }

            object initialMessage = NewInitialMessage(ctx);
            if (initialMessage != null)
            {
                SendToProxyServer(initialMessage);
            }

            ReadIfNeeded(ctx);

            void ConnectTimeout()
            {
                if (!_connectPromise.Task.IsCompleted)
                {
                    SetConnectFailure(new ProxyConnectException(ExceptionMessage("timeout")));
                }
            }
        }

        /// <summary>
        /// Returns a new message that is sent at first time when the connection to the proxy server has been established.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns>the initial message, or {@code null} if the proxy server is expected to send the first message instead</returns>
        protected abstract object NewInitialMessage(IChannelHandlerContext ctx);
        
        /// <summary>
        /// Sends the specified message to the proxy server.  Use this method to send a response to the proxy server in
        /// {@link #handleResponse(IChannelHandlerContext, object)}.
        /// </summary>
        protected void SendToProxyServer(object msg)
        {
            _ctx.WriteAndFlushAsync(msg).ContinueWith(OnCompleted, TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);

            void OnCompleted(Task future)
            {
                SetConnectFailure(future.Exception);
            }
        }

        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            if (_finished)
            {
                ctx.FireChannelInactive();
            }
            else
            {
                // Disconnected before connected to the destination.
                SetConnectFailure(new ProxyConnectException(ExceptionMessage("disconnected")));
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            if (_finished)
            {
                ctx.FireExceptionCaught(cause);
            }
            else
            {
                // Exception was raised before the connection attempt is finished.
                SetConnectFailure(cause);
            }
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            if (_finished)
            {
                // Received a message after the connection has been established; pass through.
                _suppressChannelReadComplete = false;
                ctx.FireChannelRead(msg);
            }
            else
            {
                _suppressChannelReadComplete = true;
                Exception cause = null;
                try
                {
                    bool done = HandleResponse(ctx, msg);
                    if (done)
                    {
                        SetConnectSuccess();
                    }
                }
                catch (Exception t)
                {
                    cause = t;
                }
                finally
                {
                    ReferenceCountUtil.Release(msg);
                    if (cause != null)
                    {
                        SetConnectFailure(cause);
                    }
                }
            }
        }
        
        /// <summary>
        /// expected from the proxy server
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="response"></param>
        /// <returns>
        /// {@code true} if the connection to the destination has been established,
        /// {@code false} if the connection to the destination has not been established and more messages are expected from the proxy server
        /// </returns>
        protected abstract bool HandleResponse(IChannelHandlerContext ctx, object response);

        void SetConnectSuccess()
        {
            _finished = true;

            CancelConnectTimeoutFuture();

            if (!_connectPromise.Task.IsCompleted)
            {
                bool removedCodec = true;

                removedCodec &= SafeRemoveEncoder();

                _ctx.FireUserEventTriggered(
                    new ProxyConnectionEvent(Protocol, AuthScheme, _proxyAddress, _destinationAddress));

                removedCodec &= SafeRemoveDecoder();

                if (removedCodec)
                {
                    WritePendingWrites();

                    if (_flushedPrematurely)
                    {
                        _ctx.Flush();
                    }

                    _connectPromise.TrySetResult(_ctx.Channel);
                }
                else
                {
                    // We are at inconsistent state because we failed to remove all codec handlers.
                    Exception cause = new ProxyConnectException(
                        "failed to remove all codec handlers added by the proxy handler; bug?");
                    FailPendingWritesAndClose(cause);
                }
            }
        }

        bool SafeRemoveDecoder()
        {
            try
            {
                RemoveDecoder(_ctx);
                return true;
            }
            catch (Exception e)
            {
                Logger.Warn("Failed to remove proxy decoders:", e);
            }

            return false;
        }

        bool SafeRemoveEncoder()
        {
            try
            {
                RemoveEncoder(_ctx);
                return true;
            }
            catch (Exception e)
            {
                Logger.Warn("Failed to remove proxy encoders:", e);
            }

            return false;
        }

        void SetConnectFailure(Exception cause)
        {
            _finished = true;

            CancelConnectTimeoutFuture();

            if (!_connectPromise.Task.IsCompleted)
            {
                if (!(cause is ProxyConnectException))
                {
                    cause = new ProxyConnectException(ExceptionMessage(cause.ToString()), cause);
                }

                SafeRemoveDecoder();
                SafeRemoveEncoder();
                FailPendingWritesAndClose(cause);
            }
        }

        void FailPendingWritesAndClose(Exception cause)
        {
            FailPendingWrites(cause);

            _connectPromise.TrySetException(cause);

            _ctx.FireExceptionCaught(cause);

            _ctx.CloseAsync();
        }

        void CancelConnectTimeoutFuture()
        {
            if (_connectTimeoutFuture != null)
            {
                _connectTimeoutFuture.Cancel();
                _connectTimeoutFuture = null;
            }
        }

        /// <summary>
        /// Decorates the specified exception message with the common information such as the current protocol,
        /// authentication scheme, proxy address, and destination address.
        /// </summary>
        protected string ExceptionMessage(string msg)
        {
            if (msg == null)
            {
                msg = "";
            }

            StringBuilder buf = new StringBuilder(128 + msg.Length)
                .Append(Protocol)
                .Append(", ")
                .Append(AuthScheme)
                .Append(", ")
                .Append(_proxyAddress)
                .Append(" => ")
                .Append(_destinationAddress);

            if (!string.IsNullOrEmpty(msg))
            {
                buf.Append(", ").Append(msg);
            }

            return buf.ToString();
        }

        public override void ChannelReadComplete(IChannelHandlerContext ctx)
        {
            if (_suppressChannelReadComplete)
            {
                _suppressChannelReadComplete = false;

                ReadIfNeeded(ctx);
            }
            else
            {
                ctx.FireChannelReadComplete();
            }
        }

        public override void Write(IChannelHandlerContext context, object message, IPromise promise)
        {
            if (_finished)
            {
                WritePendingWrites();
                base.Write(context, message, promise);
            }
            else
            {
                AddPendingWrite(_ctx, message, promise);
            }
        }

        public override void Flush(IChannelHandlerContext context)
        {
            if (_finished)
            {
                WritePendingWrites();
                _ctx.Flush();
            }
            else
            {
                _flushedPrematurely = true;
            }
        }

        static void ReadIfNeeded(IChannelHandlerContext ctx)
        {
            if (!ctx.Channel.Configuration.IsAutoRead)
            {
                ctx.Read();
            }
        }

        void WritePendingWrites()
        {
            if (_pendingWrites != null)
            {
                _pendingWrites.RemoveAndWriteAllAsync();
                _pendingWrites = null;
            }
        }

        void FailPendingWrites(Exception cause)
        {
            if (_pendingWrites != null)
            {
                _pendingWrites.RemoveAndFailAll(cause);
                _pendingWrites = null;
            }
        }

        void AddPendingWrite(IChannelHandlerContext ctx, object msg, IPromise promise)
        {
            PendingWriteQueue pendingWrites = _pendingWrites;
            if (pendingWrites == null)
            {
                _pendingWrites = pendingWrites = new PendingWriteQueue(ctx);
            }

            pendingWrites.Add(msg, promise);
        }

        protected IEventExecutor Executor
        {
            get
            {
                if (_ctx == null)
                {
                    throw new Exception("Should not reach here");
                }

                return _ctx.Executor;
            }
        }
    }
}