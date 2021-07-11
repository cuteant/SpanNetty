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
 * Copyright (c) The DotNetty Project (Microsoft). All rights reserved.
 *
 *   https://github.com/azure/dotnetty
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Handlers.Tls
{
    using System;
    using System.IO;
    using System.Net.Security;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using DotNetty.Codecs;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public sealed partial class TlsHandler : ByteToMessageDecoder
    {
        private const int c_unencryptedWriteBatchSize = 14 * 1024;

        private static readonly Exception s_channelClosedException = new IOException("Channel is closed");

        private readonly bool _isServer;
        private readonly ServerTlsSettings _serverSettings;
        private readonly ClientTlsSettings _clientSettings;
        private readonly X509Certificate _serverCertificate;
#if NETCOREAPP_2_0_GREATER || NETSTANDARD_2_0_GREATER
        private readonly Func<IChannelHandlerContext, string, X509Certificate2> _serverCertificateSelector;
        private readonly Func<IChannelHandlerContext, string, X509CertificateCollection, X509Certificate, string[], X509Certificate2> _userCertSelector;
#endif

        private readonly SslStream _sslStream;
        private readonly MediationStream _mediationStream;
        private readonly DefaultPromise _closeFuture;

        private SslHandlerCoalescingBufferQueue _pendingUnencryptedWrites;

        // TOOD
        private TimeSpan _closeNotifyFlushTimeout = TimeSpan.FromMilliseconds(3000);
        private TimeSpan _closeNotifyReadTimeout = TimeSpan.Zero;
        private bool _outboundClosed;

        private IChannelHandlerContext v_capturedContext;
        private IChannelHandlerContext CapturedContext
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => Volatile.Read(ref v_capturedContext);
            set => Interlocked.Exchange(ref v_capturedContext, value);
        }

        private int v_state;
        private int State
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => Volatile.Read(ref v_state);
            set => Interlocked.Exchange(ref v_state, value);
        }

        public TlsHandler(TlsSettings settings)
          : this(stream => CreateSslStream(settings, stream), settings)
        {
        }

        public TlsHandler(Func<TlsSettings, Stream, SslStream> sslStreamFactory, TlsSettings settings)
          : this(stream => sslStreamFactory(settings, stream), settings)
        {
        }

        public TlsHandler(Func<Stream, SslStream> sslStreamFactory, TlsSettings settings)
        {
            if (sslStreamFactory is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.sslStreamFactory); }
            if (settings is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.settings); }

            _serverSettings = settings as ServerTlsSettings;
            if (_serverSettings is object)
            {
                _isServer = true;

                // capture the certificate now so it can't be switched after validation
                _serverCertificate = _serverSettings.Certificate;
#if NETCOREAPP_2_0_GREATER || NETSTANDARD_2_0_GREATER
                _serverCertificateSelector = _serverSettings.ServerCertificateSelector;
                if (_serverCertificate is null && _serverCertificateSelector is null)
                {
                    ThrowHelper.ThrowArgumentException_ServerCertificateRequired();
                }
#else
                if (_serverCertificate is null)
                {
                    ThrowHelper.ThrowArgumentException_ServerCertificateRequired();
                }
#endif
            }
            _clientSettings = settings as ClientTlsSettings;
#if NETCOREAPP_2_0_GREATER || NETSTANDARD_2_0_GREATER
            if (_clientSettings is object)
            {
                _userCertSelector = _clientSettings.UserCertSelector;
            }
#endif
            _closeFuture = new DefaultPromise();
            _mediationStream = new MediationStream(this);
            _sslStream = sslStreamFactory(_mediationStream);
        }

        // using workaround mentioned here: https://github.com/dotnet/corefx/issues/4510
        public X509Certificate2 LocalCertificate => _sslStream.LocalCertificate as X509Certificate2 ?? new X509Certificate2(_sslStream.LocalCertificate?.Export(X509ContentType.Cert));

        public X509Certificate2 RemoteCertificate => _sslStream.RemoteCertificate as X509Certificate2 ?? new X509Certificate2(_sslStream.RemoteCertificate?.Export(X509ContentType.Cert));

        public bool IsServer => _isServer;

#if NETCOREAPP_2_0_GREATER || NETSTANDARD_2_0_GREATER
        public SslApplicationProtocol NegotiatedApplicationProtocol => _sslStream.NegotiatedApplicationProtocol;
#endif

        public override void ChannelActive(IChannelHandlerContext context)
        {
            _ = context.FireChannelActive();

            if (!_isServer)
            {
                _ = EnsureAuthenticated(context);
            }
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            // Make sure to release SslStream,
            // and notify the handshake future if the connection has been closed during handshake.
            HandleFailure(s_channelClosedException, !_outboundClosed, State.HasAny(TlsHandlerState.AuthenticationStarted));

            // Ensure we always notify the sslClosePromise as well
            NotifyClosePromise(s_channelClosedException);

            base.ChannelInactive(context);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            if (IgnoreException(exception))
            {
                // Close the connection explicitly just in case the transport
                // did not close the connection automatically.
                if (context.Channel.IsActive)
                {
                    _ = context.CloseAsync();
                }
            }
            else
            {
                base.ExceptionCaught(context, exception);
            }
        }

        private bool IgnoreException(Exception t)
        {
            return t is ObjectDisposedException && _closeFuture.IsCompleted;
        }

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            base.HandlerAdded(context);
            CapturedContext = context;
            _pendingUnencryptedWrites = new SslHandlerCoalescingBufferQueue(this, context.Channel, 16);
            if (context.Channel.IsActive && !_isServer)
            {
                // todo: support delayed initialization on an existing/active channel if in client mode
                _ = EnsureAuthenticated(context);
            }
        }

        protected override void HandlerRemovedInternal(IChannelHandlerContext context)
        {
            var pendingUnencryptedWrites = _pendingUnencryptedWrites;
            _pendingUnencryptedWrites = null;
            if (!pendingUnencryptedWrites.IsEmpty())
            {
                // Check if queue is not empty first because create a new ChannelException is expensive
                pendingUnencryptedWrites.ReleaseAndFailAll(GetChannelException_Write_has_failed());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ChannelException GetChannelException_Write_has_failed()
        {
            return new ChannelException("Write has failed due to TlsHandler being removed from channel pipeline.");
        }

        //public override void Disconnect(IChannelHandlerContext context, IPromise promise)
        //{
        //    CloseOutboundAndChannel(context, promise, true);
        //}

        public override void Close(IChannelHandlerContext context, IPromise promise)
        {
            //CloseOutboundAndChannel(context, promise, false);
            _ = _closeFuture.TryComplete();
            _sslStream.Dispose();
            base.Close(context, promise);
        }

        private void NotifyClosePromise(Exception cause)
        {
            if (cause is null)
            {
                if (_closeFuture.TryComplete())
                {
                    _ = CapturedContext.FireUserEventTriggered(TlsCloseCompletionEvent.Success);
                }
            }
            else
            {
                if (_closeFuture.TrySetException(cause))
                {
                    _ = CapturedContext.FireUserEventTriggered(new TlsCloseCompletionEvent(cause));
                }
            }
        }

        private void HandleFailure(Exception cause, bool closeInbound = true, bool notify = true)
        {
            try
            {
                // Release all resources such as internal buffers that SSLEngine
                // is managing.
                _outboundClosed = true;
                _mediationStream.Dispose();
                if (closeInbound)
                {
                    try
                    {
                        _sslStream.Dispose();
                    }
                    catch (Exception)
                    {
                        // todo: evaluate following:
                        // only log in Debug mode as it most likely harmless and latest chrome still trigger
                        // this all the time.
                        //
                        // See https://github.com/netty/netty/issues/1340
                        //string msg = ex.Message;
                        //if (msg is null || !msg.contains("possible truncation attack"))
                        //{
                        //    //Logger.Debug("{} SSLEngine.closeInbound() raised an exception.", ctx.channel(), e);
                        //}
                    }
                }
                _pendingSslStreamReadBuffer?.SafeRelease();
                _pendingSslStreamReadBuffer = null;
                _pendingSslStreamReadFuture = null;

                NotifyHandshakeFailure(cause, notify);
            }
            finally
            {
                // Ensure we remove and fail all pending writes in all cases and so release memory quickly.
                _pendingUnencryptedWrites?.ReleaseAndFailAll(cause);
            }
        }

        #region not yet support

        //private void CloseOutboundAndChannel(IChannelHandlerContext context, IPromise promise, bool disconnect)
        //{
        //    _outboundClosed = true;

        //    if (!context.Channel.Active)
        //    {
        //        if (disconnect)
        //        {
        //            context.DisconnectAsync(promise);
        //        }
        //        else
        //        {
        //            context.CloseAsync(promise);
        //        }
        //        return;
        //    }

        //    var closeNotifyPromise = context.NewPromise();

        //    try
        //    {
        //        Flush(context, closeNotifyPromise);
        //    }
        //    finally
        //    {
        //        // It's important that we do not pass the original ChannelPromise to safeClose(...) as when flush(....)
        //        // throws an Exception it will be propagated to the AbstractChannelHandlerContext which will try
        //        // to fail the promise because of this. This will then fail as it was already completed by safeClose(...).
        //        // We create a new ChannelPromise and try to notify the original ChannelPromise
        //        // once it is complete. If we fail to do so we just ignore it as in this case it was failed already
        //        // because of a propagated Exception.
        //        //
        //        // See https://github.com/netty/netty/issues/5931
        //        SafeClose(context, closeNotifyPromise.Task, context.NewPromise());
        //    }
        //}

        //private void SafeClose(IChannelHandlerContext ctx, Task flushFuture, IPromise promise)
        //{
        //    if (!ctx.Channel.Active)
        //    {
        //        _sslStream.Dispose();
        //        ctx.CloseAsync(promise);
        //        return;
        //    }

        //    IScheduledTask timeoutFuture = null;
        //    if (!flushFuture.IsCompleted)
        //    {
        //        if (_closeNotifyFlushTimeout > TimeSpan.Zero)
        //        {
        //            timeoutFuture = ctx.Executor.Schedule(ScheduledForceCloseConnectionAction, Tuple.Create(ctx, flushFuture, promise, _sslStream), _closeNotifyFlushTimeout);
        //        }
        //        // Close the connection if close_notify is sent in time.
        //        flushFuture.ContinueWith(CloseConnectionAction, Tuple.Create(ctx, promise, timeoutFuture, this), TaskContinuationOptions.ExecuteSynchronously);
        //    }
        //    else
        //    {
        //        InternalCloseConnection(flushFuture, Tuple.Create(ctx, promise, timeoutFuture, this));
        //    }
        //}

        //private static readonly Action<object> ScheduledForceCloseConnectionAction = ScheduledForceCloseConnection;
        //private static void ScheduledForceCloseConnection(object s)
        //{
        //    var wrapped = (Tuple<IChannelHandlerContext, Task, IPromise, SslStream>)s;
        //    // May be done in the meantime as cancel(...) is only best effort.
        //    if (!wrapped.Item2.IsCompleted)
        //    {
        //        wrapped.Item4.Dispose();

        //        var ctx = wrapped.Item1;
        //        s_logger.Warn("{} Last write attempt timed out; force-closing the connection.", ctx.Channel);
        //        AddCloseListener(ctx.CloseAsync(ctx.NewPromise()), wrapped.Item3);
        //    }
        //}

        //private static readonly Action<Task, object> CloseConnectionAction = InternalCloseConnection;
        //private static void InternalCloseConnection(Task t, object s)
        //{
        //    var wrapped = (Tuple<IChannelHandlerContext, IPromise, IScheduledTask, TlsHandler>)s;

        //    wrapped.Item3?.Cancel();

        //    var ctx = wrapped.Item1;
        //    var promise = wrapped.Item2;
        //    var owner = wrapped.Item4;
        //    var closeNotifyReadTimeout = owner._closeNotifyReadTimeout;
        //    if (closeNotifyReadTimeout <= TimeSpan.Zero)
        //    {
        //        owner._sslStream.Dispose();
        //        // Trigger the close in all cases to make sure the promise is notified
        //        // See https://github.com/netty/netty/issues/2358
        //        AddCloseListener(ctx.CloseAsync(ctx.NewPromise()), promise);
        //    }
        //    else
        //    {
        //        owner._sslStream.Dispose();
        //        AddCloseListener(ctx.CloseAsync(ctx.NewPromise()), promise);

        //        // TODO notifyClosure from Unwraps inbound SSL records
        //        //var sslClosePromise = owner._closeFuture;
        //        //IScheduledTask closeNotifyReadTimeoutFuture = null;
        //        //if (!sslClosePromise.IsCompleted)
        //        //{
        //        //    closeNotifyReadTimeoutFuture = ctx.Executor.Schedule(ScheduledForceCloseConnection0Action, Tuple.Create(ctx, sslClosePromise, promise, owner), closeNotifyReadTimeout);
        //        //}
        //        //// Do the close once the we received the close_notify.
        //        //sslClosePromise.Task.ContinueWith(t =>
        //        //{
        //        //    closeNotifyReadTimeoutFuture?.Cancel();

        //        //    owner._sslStream.Dispose();
        //        //    AddCloseListener(ctx.CloseAsync(ctx.NewPromise()), promise);
        //        //}, TaskContinuationOptions.ExecuteSynchronously);
        //    }
        //}

        //private static readonly Action<object> ScheduledForceCloseConnection0Action = ScheduledForceCloseConnection0;
        //private static void ScheduledForceCloseConnection0(object s)
        //{
        //    var wrapped = (Tuple<IChannelHandlerContext, DefaultPromise, IPromise, TlsHandler>)s;
        //    // May be done in the meantime as cancel(...) is only best effort.
        //    if (!wrapped.Item2.IsCompleted)
        //    {
        //        var owner = wrapped.Item4;
        //        owner._sslStream.Dispose();

        //        var ctx = wrapped.Item1;
        //        s_logger.Warn("{} did not receive close_notify in {}ms; force-closing the connection.", ctx.Channel, owner._closeNotifyReadTimeout);
        //        AddCloseListener(ctx.CloseAsync(ctx.NewPromise()), wrapped.Item3);
        //    }
        //}

        //private static void AddCloseListener(Task future, IPromise promise)
        //{
        //    // We notify the promise in the ChannelPromiseNotifier as there is a "race" where the close(...) call
        //    // by the timeoutFuture and the close call in the flushFuture listener will be called. Because of
        //    // this we need to use trySuccess() and tryFailure(...) as otherwise we can cause an
        //    // IllegalStateException.
        //    // Also we not want to log if the notification happens as this is expected in some cases.
        //    // See https://github.com/netty/netty/issues/5598
        //    future.LinkOutcome(promise);
        //}

        #endregion
    }
}