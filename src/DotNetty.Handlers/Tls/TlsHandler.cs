// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Security;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;

    public sealed partial class TlsHandler : ByteToMessageDecoder
    {
        #region @@ Fields @@

        private static readonly IInternalLogger s_logger = InternalLoggerFactory.GetInstance<TlsHandler>();

        private const int c_fallbackReadBufferSize = 256;
        private const int c_unencryptedWriteBatchSize = 14 * 1024;

        private readonly bool _isServer;
        private readonly ServerTlsSettings _serverSettings;
        private readonly ClientTlsSettings _clientSettings;
        private readonly X509Certificate _serverCertificate;
        private readonly Func<IChannelHandlerContext, string, X509Certificate2> _serverCertificateSelector;
#if NETCOREAPP_2_0_GREATER
        private readonly bool _hasHttp2Protocol;
        private readonly Func<IChannelHandlerContext, string, X509CertificateCollection, X509Certificate, string[], X509Certificate2> _userCertSelector;
#endif

        public static readonly AttributeKey<SslStream> SslStreamAttrKey = AttributeKey<SslStream>.ValueOf("SSLSTREAM");

        private static readonly Exception s_channelClosedException = new IOException("Channel is closed");
#if !NET40
        private static readonly Action<Task, object> s_handshakeCompletionCallback = new Action<Task, object>(HandleHandshakeCompleted);
#endif

        private readonly SslStream _sslStream;
        private readonly MediationStream _mediationStream;
        private readonly TaskCompletionSource _closeFuture;

        private int _state;
        private int _packetLength;
        private IChannelHandlerContext _capturedContext;
        private BatchingPendingWriteQueue _pendingUnencryptedWrites;
        private Task _lastContextWriteTask;
        private bool _firedChannelRead;
        private IByteBuffer _pendingSslStreamReadBuffer;
        private Task<int> _pendingSslStreamReadFuture;

        #endregion

        #region @@ Constructors @@

        //public TlsHandler(TlsSettings settings)
        //  : this(stream => new SslStream(stream, true), settings)
        //{
        //}

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
                _serverCertificateSelector = _serverSettings.ServerCertificateSelector;
                if (_serverCertificate is null && _serverCertificateSelector is null)
                {
                    ThrowHelper.ThrowArgumentException_ServerCertificateRequired();
                }

#if NETCOREAPP_2_0_GREATER
                var serverApplicationProtocols = _serverSettings.ApplicationProtocols;
                if (serverApplicationProtocols is object)
                {
                    _hasHttp2Protocol = serverApplicationProtocols.Contains(SslApplicationProtocol.Http2);
                }
#endif

                // If a selector is provided then ignore the cert, it may be a default cert.
                if (_serverCertificateSelector is object)
                {
                    // SslStream doesn't allow both.
                    _serverCertificate = null;
                }
                else
                {
                    EnsureCertificateIsAllowedForServerAuth(ConvertToX509Certificate2(_serverCertificate));
                }
            }
            _clientSettings = settings as ClientTlsSettings;
#if NETCOREAPP_2_0_GREATER
            if (_clientSettings is object)
            {
                var clientApplicationProtocols = _clientSettings.ApplicationProtocols;
                _hasHttp2Protocol = clientApplicationProtocols is object && clientApplicationProtocols.Contains(SslApplicationProtocol.Http2);
                _userCertSelector = _clientSettings.UserCertSelector;
            }
#endif
            _closeFuture = new TaskCompletionSource();
            _mediationStream = new MediationStream(this);
            _sslStream = sslStreamFactory(_mediationStream);
        }

        public static TlsHandler Client(string targetHost) => new TlsHandler(new ClientTlsSettings(targetHost));

        public static TlsHandler Client(string targetHost, X509Certificate clientCertificate) => new TlsHandler(new ClientTlsSettings(targetHost, new List<X509Certificate> { clientCertificate }));

        public static TlsHandler Server(X509Certificate certificate) => new TlsHandler(new ServerTlsSettings(certificate));

        #endregion

        #region @@ Properties @@

        // using workaround mentioned here: https://github.com/dotnet/corefx/issues/4510
        public X509Certificate2 LocalCertificate => _sslStream.LocalCertificate as X509Certificate2 ?? new X509Certificate2(_sslStream.LocalCertificate?.Export(X509ContentType.Cert));

        public X509Certificate2 RemoteCertificate => _sslStream.RemoteCertificate as X509Certificate2 ?? new X509Certificate2(_sslStream.RemoteCertificate?.Export(X509ContentType.Cert));

        public bool IsServer => _isServer;

        private IChannelHandlerContext CapturedContext
        {
            [MethodImpl(InlineMethod.AggressiveInlining)]
            get => Volatile.Read(ref _capturedContext);
            set => Interlocked.Exchange(ref _capturedContext, value);
        }

        private int State
        {
            [MethodImpl(InlineMethod.AggressiveInlining)]
            get => Volatile.Read(ref _state);
            set => Interlocked.Exchange(ref _state, value);
        }

#if NETCOREAPP_2_0_GREATER
        public SslApplicationProtocol NegotiatedApplicationProtocol => _sslStream.NegotiatedApplicationProtocol;
#endif

        #endregion

        #region -- ChannelActive --

        public override void ChannelActive(IChannelHandlerContext context)
        {
            base.ChannelActive(context);

            if (!_isServer)
            {
                EnsureAuthenticated(context);
            }
        }

        #endregion

        #region -- ChannelInactive --

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            // Make sure to release SslStream,
            // and notify the handshake future if the connection has been closed during handshake.
            HandleFailure(s_channelClosedException);

            base.ChannelInactive(context);
        }

        #endregion

        #region -- ExceptionCaught --

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            if (IgnoreException(exception))
            {
                // Close the connection explicitly just in case the transport
                // did not close the connection automatically.
                if (context.Channel.Active)
                {
                    context.CloseAsync();
                }
            }
            else
            {
                base.ExceptionCaught(context, exception);
            }
        }

        #endregion

        #region ** IgnoreException **

        private bool IgnoreException(Exception t)
        {
            if (t is ObjectDisposedException && _closeFuture.Task.IsCompleted)
            {
                return true;
            }
            return false;
        }

        #endregion

        #region -- HandlerAdded --

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            base.HandlerAdded(context);
            CapturedContext = context;
            _pendingUnencryptedWrites = new BatchingPendingWriteQueue(context, c_unencryptedWriteBatchSize);
            if (context.Channel.Active && !_isServer)
            {
                // todo: support delayed initialization on an existing/active channel if in client mode
                EnsureAuthenticated(context);
            }
        }

        #endregion

        #region ++ HandlerRemovedInternal ++

        protected override void HandlerRemovedInternal(IChannelHandlerContext context)
        {
            if (!_pendingUnencryptedWrites.IsEmpty)
            {
                // Check if queue is not empty first because create a new ChannelException is expensive
                _pendingUnencryptedWrites.RemoveAndFailAll(new ChannelException("Write has failed due to TlsHandler being removed from channel pipeline."));
            }
        }

        #endregion

        #region ++ Decode ++

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            int startOffset = input.ReaderIndex;
            int endOffset = input.WriterIndex;
            int offset = startOffset;
            int totalLength = 0;

            List<int> packetLengths;
            // if we calculated the length of the current SSL record before, use that information.
            if (_packetLength > 0)
            {
                if (endOffset - startOffset < _packetLength)
                {
                    // input does not contain a single complete SSL record
                    return;
                }
                else
                {
                    packetLengths = new List<int>(4);
                    packetLengths.Add(_packetLength);
                    offset += _packetLength;
                    totalLength = _packetLength;
                    _packetLength = 0;
                }
            }
            else
            {
                packetLengths = new List<int>(4);
            }

            bool nonSslRecord = false;

            while (totalLength < TlsUtils.MAX_ENCRYPTED_PACKET_LENGTH)
            {
                int readableBytes = endOffset - offset;
                if (readableBytes < TlsUtils.SSL_RECORD_HEADER_LENGTH)
                {
                    break;
                }

                int encryptedPacketLength = TlsUtils.GetEncryptedPacketLength(input, offset);
                if (encryptedPacketLength == -1)
                {
                    nonSslRecord = true;
                    break;
                }

                Debug.Assert(encryptedPacketLength > 0);

                if (encryptedPacketLength > readableBytes)
                {
                    // wait until the whole packet can be read
                    _packetLength = encryptedPacketLength;
                    break;
                }

                int newTotalLength = totalLength + encryptedPacketLength;
                if (newTotalLength > TlsUtils.MAX_ENCRYPTED_PACKET_LENGTH)
                {
                    // Don't read too much.
                    break;
                }

                // 1. call unwrap with packet boundaries - call SslStream.ReadAsync only once.
                // 2. once we're through all the whole packets, switch to reading out using fallback sized buffer

                // We have a whole packet.
                // Increment the offset to handle the next packet.
                packetLengths.Add(encryptedPacketLength);
                offset += encryptedPacketLength;
                totalLength = newTotalLength;
            }

            if (totalLength > 0)
            {
                // The buffer contains one or more full SSL records.
                // Slice out the whole packet so unwrap will only be called with complete packets.
                // Also directly reset the packetLength. This is needed as unwrap(..) may trigger
                // decode(...) again via:
                // 1) unwrap(..) is called
                // 2) wrap(...) is called from within unwrap(...)
                // 3) wrap(...) calls unwrapLater(...)
                // 4) unwrapLater(...) calls decode(...)
                //
                // See https://github.com/netty/netty/issues/1534

                input.SkipBytes(totalLength);
                Unwrap(context, input, startOffset, totalLength, packetLengths, output);

                if (!_firedChannelRead)
                {
                    // Check first if firedChannelRead is not set yet as it may have been set in a
                    // previous decode(...) call.
                    _firedChannelRead = (uint)output.Count > 0u;
                }
            }

            if (nonSslRecord)
            {
                // Not an SSL/TLS packet
                var ex = new NotSslRecordException(
                    "not an SSL/TLS record: " + ByteBufferUtil.HexDump(input));
                input.SkipBytes(input.ReadableBytes);
                context.FireExceptionCaught(ex);
                HandleFailure(ex);
            }
        }

        #endregion

        #region -- ChannelReadComplete --

        public override void ChannelReadComplete(IChannelHandlerContext ctx)
        {
            // Discard bytes of the cumulation buffer if needed.
            DiscardSomeReadBytes();

            ReadIfNeeded(ctx);

            _firedChannelRead = false;
            ctx.FireChannelReadComplete();
        }

        #endregion

        #region ** ReadIfNeeded **

        private void ReadIfNeeded(IChannelHandlerContext ctx)
        {
            // if handshake is not finished yet, we need more data
            if (!ctx.Channel.Configuration.AutoRead && (!_firedChannelRead || !State.HasAny(TlsHandlerState.AuthenticationCompleted)))
            {
                // No auto-read used and no message was passed through the ChannelPipeline or the handshake was not completed
                // yet, which means we need to trigger the read to ensure we will not stall
                ctx.Read();
            }
        }

        #endregion

        #region ** Unwrap **

        /// <summary>Unwraps inbound SSL records.</summary>
        private void Unwrap(IChannelHandlerContext ctx, IByteBuffer packet, int offset, int length, List<int> packetLengths, List<object> output)
        {
            if (0u >= (uint)packetLengths.Count) { ThrowHelper.ThrowArgumentException(); }

            //bool notifyClosure = false; // todo: netty/issues/137
            bool pending = false;

            IByteBuffer outputBuffer = null;

            try
            {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
                ReadOnlyMemory<byte> inputIoBuffer = packet.GetReadableMemory(offset, length);
                _mediationStream.SetSource(inputIoBuffer);
#else
                ArraySegment<byte> inputIoBuffer = packet.GetIoBuffer(offset, length);
                _mediationStream.SetSource(inputIoBuffer.Array, inputIoBuffer.Offset);
#endif

                int packetIndex = 0;

                while (!EnsureAuthenticated(ctx))
                {
                    _mediationStream.ExpandSource(packetLengths[packetIndex]);
                    if (++packetIndex == packetLengths.Count)
                    {
                        return;
                    }
                }

                var currentReadFuture = _pendingSslStreamReadFuture;

                int outputBufferLength;

                if (currentReadFuture is object)
                {
                    // restoring context from previous read
                    Debug.Assert(_pendingSslStreamReadBuffer is object);

                    outputBuffer = _pendingSslStreamReadBuffer;
                    outputBufferLength = outputBuffer.WritableBytes;

                    _pendingSslStreamReadFuture = null;
                    _pendingSslStreamReadBuffer = null;
                }
                else
                {
                    outputBufferLength = 0;
                }

                // go through packets one by one (because SslStream does not consume more than 1 packet at a time)
                for (; packetIndex < packetLengths.Count; packetIndex++)
                {
                    int currentPacketLength = packetLengths[packetIndex];
                    _mediationStream.ExpandSource(currentPacketLength);

                    if (currentReadFuture is object)
                    {
                        // there was a read pending already, so we make sure we completed that first

                        if (!currentReadFuture.IsCompleted)
                        {
                            // we did feed the whole current packet to SslStream yet it did not produce any result -> move to the next packet in input

                            continue;
                        }

                        int read = currentReadFuture.Result;

                        if (0u >= (uint)read)
                        {
                            //Stream closed
                            return;
                        }

                        // Now output the result of previous read and decide whether to do an extra read on the same source or move forward
                        AddBufferToOutput(outputBuffer, read, output);

                        currentReadFuture = null;
                        outputBuffer = null;
                        if (0u >= (uint)_mediationStream.SourceReadableBytes)
                        {
                            // we just made a frame available for reading but there was already pending read so SslStream read it out to make further progress there

                            if (read < outputBufferLength)
                            {
                                // SslStream returned non-full buffer and there's no more input to go through ->
                                // typically it means SslStream is done reading current frame so we skip
                                continue;
                            }

                            // we've read out `read` bytes out of current packet to fulfil previously outstanding read
                            outputBufferLength = currentPacketLength - read;
                            if (outputBufferLength <= 0)
                            {
                                // after feeding to SslStream current frame it read out more bytes than current packet size
                                outputBufferLength = c_fallbackReadBufferSize;
                            }
                        }
                        else
                        {
                            // SslStream did not get to reading current frame so it completed previous read sync
                            // and the next read will likely read out the new frame
                            outputBufferLength = currentPacketLength;
                        }
                    }
                    else
                    {
                        // there was no pending read before so we estimate buffer of `currentPacketLength` bytes to be sufficient
                        outputBufferLength = currentPacketLength;
                    }

                    outputBuffer = ctx.Allocator.Buffer(outputBufferLength);
                    currentReadFuture = ReadFromSslStreamAsync(outputBuffer, outputBufferLength);
                }

                // read out the rest of SslStream's output (if any) at risk of going async
                // using FallbackReadBufferSize - buffer size we're ok to have pinned with the SslStream until it's done reading
                while (true)
                {
                    if (currentReadFuture is object)
                    {
                        if (!currentReadFuture.IsCompleted)
                        {
                            break;
                        }
                        int read = currentReadFuture.Result;
                        AddBufferToOutput(outputBuffer, read, output);
                    }
                    outputBuffer = ctx.Allocator.Buffer(c_fallbackReadBufferSize);
                    currentReadFuture = ReadFromSslStreamAsync(outputBuffer, c_fallbackReadBufferSize);
                }

                pending = true;
                _pendingSslStreamReadBuffer = outputBuffer;
                _pendingSslStreamReadFuture = currentReadFuture;
            }
            catch (Exception ex)
            {
                HandleFailure(ex);
                throw;
            }
            finally
            {
                _mediationStream.ResetSource();
                if (!pending && outputBuffer is object)
                {
                    if (outputBuffer.IsReadable())
                    {
                        output.Add(outputBuffer);
                    }
                    else
                    {
                        outputBuffer.SafeRelease();
                    }
                }
            }
        }

        #endregion

        #region **& AddBufferToOutput &**

        private static void AddBufferToOutput(IByteBuffer outputBuffer, int length, List<object> output)
        {
            Debug.Assert(length > 0);
            output.Add(outputBuffer.SetWriterIndex(outputBuffer.WriterIndex + length));
        }

        #endregion

        #region ** ReadFromSslStreamAsync **

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
        private Task<int> ReadFromSslStreamAsync(IByteBuffer outputBuffer, int outputBufferLength)
        {
            Memory<byte> outlet = outputBuffer.GetMemory(outputBuffer.WriterIndex, outputBufferLength);
            return _sslStream.ReadAsync(outlet).AsTask();
        }
#else
        private Task<int> ReadFromSslStreamAsync(IByteBuffer outputBuffer, int outputBufferLength)
        {
            ArraySegment<byte> outlet = outputBuffer.GetIoBuffer(outputBuffer.WriterIndex, outputBufferLength);
            return _sslStream.ReadAsync(outlet.Array, outlet.Offset, outlet.Count);
        }
#endif

        #endregion

        #region -- Read --

        public override void Read(IChannelHandlerContext context)
        {
            var oldState = State;
            if (!oldState.HasAny(TlsHandlerState.AuthenticationCompleted))
            {
                State = oldState | TlsHandlerState.ReadRequestedBeforeAuthenticated;
            }

            context.Read();
        }

        #endregion

        #region ** EnsureAuthenticated **

        private bool EnsureAuthenticated(IChannelHandlerContext ctx)
        {
            var oldState = State;
            if (!oldState.HasAny(TlsHandlerState.AuthenticationStarted))
            {
                State = oldState | TlsHandlerState.Authenticating;
                if (_isServer)
                {
#if NETCOREAPP_2_0_GREATER
                    // Adapt to the SslStream signature
                    ServerCertificateSelectionCallback selector = null;
                    if (_serverCertificateSelector is object)
                    {
                        X509Certificate LocalServerCertificateSelection(object sender, string name)
                        {
                            ctx.GetAttribute(SslStreamAttrKey).Set(_sslStream);
                            var cert = _serverCertificateSelector(ctx, name);
                            if (cert is object)
                            {
                                EnsureCertificateIsAllowedForServerAuth(cert);
                            }
                            return cert;
                        }
                        selector = new ServerCertificateSelectionCallback(LocalServerCertificateSelection);
                    }

                    var sslOptions = new SslServerAuthenticationOptions()
                    {
                        ServerCertificate = _serverCertificate,
                        ServerCertificateSelectionCallback = selector,
                        ClientCertificateRequired = _serverSettings.NegotiateClientCertificate,
                        EnabledSslProtocols = _serverSettings.EnabledProtocols,
                        CertificateRevocationCheckMode = _serverSettings.CheckCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
                        ApplicationProtocols = _serverSettings.ApplicationProtocols // ?? new List<SslApplicationProtocol>()
                    };
                    if (_hasHttp2Protocol)
                    {
                        // https://tools.ietf.org/html/rfc7540#section-9.2.1
                        sslOptions.AllowRenegotiation = false;
                    }
                    _sslStream.AuthenticateAsServerAsync(sslOptions, CancellationToken.None)
                              .ContinueWith(s_handshakeCompletionCallback, this, TaskContinuationOptions.ExecuteSynchronously);
#else
                    var serverCert = _serverCertificate;
                    if (_serverCertificateSelector is object)
                    {
                        ctx.GetAttribute(SslStreamAttrKey).Set(_sslStream);
                        var serverCert2 = _serverCertificateSelector(ctx, null);
                        if (serverCert2 is object)
                        {
                            EnsureCertificateIsAllowedForServerAuth(serverCert2);
                            serverCert = serverCert2;
                        }
                    }
#if NET40
                    _sslStream.BeginAuthenticateAsServer(serverCert,
                                                         _serverSettings.NegotiateClientCertificate,
                                                         _serverSettings.EnabledProtocols,
                                                         _serverSettings.CheckCertificateRevocation,
                                                         Server_HandleHandshakeCompleted,
                                                         this);
#else
                    _sslStream.AuthenticateAsServerAsync(serverCert,
                                                         _serverSettings.NegotiateClientCertificate,
                                                         _serverSettings.EnabledProtocols,
                                                         _serverSettings.CheckCertificateRevocation)
                              .ContinueWith(s_handshakeCompletionCallback, this, TaskContinuationOptions.ExecuteSynchronously);
#endif
#endif
                }
                else
                {
#if NETCOREAPP_2_0_GREATER
                    LocalCertificateSelectionCallback selector = null;
                    if (_userCertSelector is object)
                    {
                        X509Certificate LocalCertificateSelection(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
                        {
                            ctx.GetAttribute(SslStreamAttrKey).Set(_sslStream);
                            return _userCertSelector(ctx, targetHost, localCertificates, remoteCertificate, acceptableIssuers);
                        }
                        selector = new LocalCertificateSelectionCallback(LocalCertificateSelection);
                    }
                    var sslOptions = new SslClientAuthenticationOptions()
                    {
                        TargetHost = _clientSettings.TargetHost,
                        ClientCertificates = _clientSettings.X509CertificateCollection,
                        EnabledSslProtocols = _clientSettings.EnabledProtocols,
                        CertificateRevocationCheckMode = _clientSettings.CheckCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
                        LocalCertificateSelectionCallback = selector,
                        ApplicationProtocols = _clientSettings.ApplicationProtocols
                    };
                    if (_hasHttp2Protocol)
                    {
                        // https://tools.ietf.org/html/rfc7540#section-9.2.1
                        sslOptions.AllowRenegotiation = false;
                    }
                    _sslStream.AuthenticateAsClientAsync(sslOptions, CancellationToken.None)
                              .ContinueWith(s_handshakeCompletionCallback, this, TaskContinuationOptions.ExecuteSynchronously);
#elif NET40
                    _sslStream.BeginAuthenticateAsClient(_clientSettings.TargetHost,
                                                         _clientSettings.X509CertificateCollection,
                                                         _clientSettings.EnabledProtocols,
                                                         _clientSettings.CheckCertificateRevocation,
                                                         Client_HandleHandshakeCompleted,
                                                         this);
#else
                    _sslStream.AuthenticateAsClientAsync(_clientSettings.TargetHost,
                                                         _clientSettings.X509CertificateCollection,
                                                         _clientSettings.EnabledProtocols,
                                                         _clientSettings.CheckCertificateRevocation)
                              .ContinueWith(s_handshakeCompletionCallback, this, TaskContinuationOptions.ExecuteSynchronously);
#endif
                }
                return false;
            }

            return oldState.Has(TlsHandlerState.Authenticated);
        }

        #endregion

        #region **& HandleHandshakeCompleted &**

#if NET40
        private static void Client_HandleHandshakeCompleted(IAsyncResult result)
        {
            var self = (TlsHandler)result.AsyncState;
            int oldState;
            try
            {
                self._sslStream.EndAuthenticateAsClient(result);
            }
            catch (Exception ex)
            {
                // ReSharper disable once AssignNullToNotNullAttribute -- task.Exception will be present as task is faulted
                oldState = self.State;
                Debug.Assert(!oldState.HasAny(TlsHandlerState.Authenticated));
                self.HandleFailure(ex);
                return;
            }

            oldState = self.State;

            Debug.Assert(!oldState.HasAny(TlsHandlerState.AuthenticationCompleted));
            self.State = (oldState | TlsHandlerState.Authenticated) & ~(TlsHandlerState.Authenticating | TlsHandlerState.FlushedBeforeHandshake);

            var capturedContext = self.CapturedContext;
            capturedContext.FireUserEventTriggered(TlsHandshakeCompletionEvent.Success);

            if (oldState.Has(TlsHandlerState.ReadRequestedBeforeAuthenticated) && !capturedContext.Channel.Configuration.AutoRead)
            {
                capturedContext.Read();
            }

            if (oldState.Has(TlsHandlerState.FlushedBeforeHandshake))
            {
                self.Wrap(capturedContext);
                capturedContext.Flush();
            }
        }

        private static void Server_HandleHandshakeCompleted(IAsyncResult result)
        {
            var self = (TlsHandler)result.AsyncState;
            int oldState;
            try
            {
                self._sslStream.EndAuthenticateAsServer(result);
            }
            catch (Exception ex)
            {
                // ReSharper disable once AssignNullToNotNullAttribute -- task.Exception will be present as task is faulted
                oldState = self.State;
                Debug.Assert(!oldState.HasAny(TlsHandlerState.Authenticated));
                self.HandleFailure(ex);
                return;
            }

            oldState = self.State;

            Debug.Assert(!oldState.HasAny(TlsHandlerState.AuthenticationCompleted));
            self.State = (oldState | TlsHandlerState.Authenticated) & ~(TlsHandlerState.Authenticating | TlsHandlerState.FlushedBeforeHandshake);

            var capturedContext = self.CapturedContext;
            capturedContext.FireUserEventTriggered(TlsHandshakeCompletionEvent.Success);

            if (oldState.Has(TlsHandlerState.ReadRequestedBeforeAuthenticated) && !capturedContext.Channel.Configuration.AutoRead)
            {
                capturedContext.Read();
            }

            if (oldState.Has(TlsHandlerState.FlushedBeforeHandshake))
            {
                self.Wrap(capturedContext);
                capturedContext.Flush();
            }
        }
#else
        private static void HandleHandshakeCompleted(Task task, object state)
        {
            var self = (TlsHandler)state;
            if (task.IsCanceled || task.IsFaulted)
            {
                // ReSharper disable once AssignNullToNotNullAttribute -- task.Exception will be present as task is faulted
                var oldState = self.State;
                Debug.Assert(!oldState.HasAny(TlsHandlerState.Authenticated));
                self.HandleFailure(task.Exception);
            }
            else //if (task.IsCompleted)
            {
                var oldState = self.State;

                Debug.Assert(!oldState.HasAny(TlsHandlerState.AuthenticationCompleted));
                self.State = (oldState | TlsHandlerState.Authenticated) & ~(TlsHandlerState.Authenticating | TlsHandlerState.FlushedBeforeHandshake);

                var capturedContext = self.CapturedContext;
                capturedContext.FireUserEventTriggered(TlsHandshakeCompletionEvent.Success);

                if (oldState.Has(TlsHandlerState.ReadRequestedBeforeAuthenticated) && !capturedContext.Channel.Configuration.AutoRead)
                {
                    capturedContext.Read();
                }

                if (oldState.Has(TlsHandlerState.FlushedBeforeHandshake))
                {
                    self.Wrap(capturedContext);
                    capturedContext.Flush();
                }
            }
        }
#endif

        #endregion

        #region -- WriteAsync --

        public override void Write(IChannelHandlerContext context, object message, IPromise promise)
        {
            if (message is IByteBuffer)
            {
                _pendingUnencryptedWrites.Add(message, promise);
                return;
            }
            promise.TrySetException(ThrowHelper.GetUnsupportedMessageTypeException(message));
        }

        #endregion

        #region -- Flush --

        public override void Flush(IChannelHandlerContext context)
        {
            if (_pendingUnencryptedWrites.IsEmpty)
            {
                // It's important to NOT use a voidPromise here as the user
                // may want to add a ChannelFutureListener to the ChannelPromise later.
                //
                // See https://github.com/netty/netty/issues/3364
                _pendingUnencryptedWrites.Add(Unpooled.Empty, context.NewPromise());
            }

            if (!EnsureAuthenticated(context))
            {
                State |= TlsHandlerState.FlushedBeforeHandshake;
                return;
            }

            try
            {
                Wrap(context);
            }
            finally
            {
                // We may have written some parts of data before an exception was thrown so ensure we always flush.
                context.Flush();
            }
        }

        #endregion

        #region ** Wrap **

        private void Wrap(IChannelHandlerContext context)
        {
            Debug.Assert(context == CapturedContext);

            IByteBuffer buf = null;
            try
            {
                while (true)
                {
                    List<object> messages = _pendingUnencryptedWrites.Current;
                    if (messages is null || 0u >= (uint)messages.Count)
                    {
                        break;
                    }

                    if (messages.Count == 1)
                    {
                        buf = (IByteBuffer)messages[0];
                    }
                    else
                    {
                        buf = context.Allocator.Buffer((int)_pendingUnencryptedWrites.CurrentSize);
                        foreach (IByteBuffer buffer in messages)
                        {
                            buffer.ReadBytes(buf, buffer.ReadableBytes);
                            buffer.Release();
                        }
                    }
                    buf.ReadBytes(_sslStream, buf.ReadableBytes); // this leads to FinishWrap being called 0+ times
                    buf.Release();

                    var promise = _pendingUnencryptedWrites.Remove();
                    Task task = _lastContextWriteTask;
                    if (task is object)
                    {
                        task.LinkOutcome(promise);
                        _lastContextWriteTask = null;
                    }
                    else
                    {
                        promise.TryComplete();
                    }
                }
            }
            catch (Exception ex)
            {
                buf.SafeRelease();
                HandleFailure(ex);
                throw;
            }
        }

        #endregion

        #region ** FinishWrap **

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
        private void FinishWrap(ReadOnlySpan<byte> buffer, IPromise promise)
        {
            IByteBuffer output;
            var capturedContext = CapturedContext;
            if (buffer.IsEmpty)
            {
                output = Unpooled.Empty;
            }
            else
            {
                var bufLen = buffer.Length;
                output = capturedContext.Allocator.Buffer(bufLen);
                buffer.CopyTo(output.FreeSpan);
                output.Advance(bufLen);
            }

            _lastContextWriteTask = capturedContext.WriteAsync(output, promise);
        }
#endif

        private void FinishWrap(byte[] buffer, int offset, int count, IPromise promise)
        {
            IByteBuffer output;
            var capturedContext = CapturedContext;
            if (0u >= (uint)count)
            {
                output = Unpooled.Empty;
            }
            else
            {
                output = capturedContext.Allocator.Buffer(count);
                output.WriteBytes(buffer, offset, count);
            }

            _lastContextWriteTask = capturedContext.WriteAsync(output, promise);
        }

        #endregion

        #region ** FinishWrapNonAppDataAsync **

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
        private Task FinishWrapNonAppDataAsync(ReadOnlyMemory<byte> buffer, IPromise promise)
        {
            var capturedContext = CapturedContext;
            var future = capturedContext.WriteAndFlushAsync(Unpooled.WrappedBuffer(buffer.ToArray()), promise);
            this.ReadIfNeeded(capturedContext);
            return future;
        }
#endif

        private Task FinishWrapNonAppDataAsync(byte[] buffer, int offset, int count, IPromise promise)
        {
            var capturedContext = CapturedContext;
            var future = capturedContext.WriteAndFlushAsync(Unpooled.WrappedBuffer(buffer, offset, count), promise);
            this.ReadIfNeeded(capturedContext);
            return future;
        }

        #endregion

        #region -- CloseAsync --

        public override void Close(IChannelHandlerContext context, IPromise promise)
        {
            _closeFuture.TryComplete();
            _sslStream.Dispose();
            base.Close(context, promise);
        }

        #endregion

        #region ** HandleFailure **

        private void HandleFailure(Exception cause)
        {
            // Release all resources such as internal buffers that SSLEngine
            // is managing.

            _mediationStream.Dispose();
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
            _pendingSslStreamReadBuffer?.SafeRelease();
            _pendingSslStreamReadBuffer = null;
            _pendingSslStreamReadFuture = null;

            NotifyHandshakeFailure(cause);
            _pendingUnencryptedWrites.RemoveAndFailAll(cause);
        }

        #endregion

        #region ** NotifyHandshakeFailure **

        private void NotifyHandshakeFailure(Exception cause)
        {
            var oldState = State;
            if (!oldState.HasAny(TlsHandlerState.AuthenticationCompleted))
            {
                // handshake was not completed yet => TlsHandler react to failure by closing the channel
                State = (oldState | TlsHandlerState.FailedAuthentication) & ~TlsHandlerState.Authenticating;
                var capturedContext = CapturedContext;
                capturedContext.FireUserEventTriggered(new TlsHandshakeCompletionEvent(cause));
                this.Close(capturedContext, capturedContext.NewPromise());
            }
        }

        #endregion

        #region ** class MediationStream **

        private sealed partial class MediationStream : Stream
        {
            private readonly TlsHandler _owner;
            private int _inputOffset;
            private int _inputLength;
            private TaskCompletionSource<int> _readCompletionSource;

            public MediationStream(TlsHandler owner)
            {
                _owner = owner;
            }

            public int SourceReadableBytes => _inputLength - _inputOffset;

            public override void Flush()
            {
                // NOOP: called on SslStream.Close
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing)
                {
                    TaskCompletionSource<int> p = _readCompletionSource;
                    if (p is object)
                    {
                        _readCompletionSource = null;
                        p.TrySetResult(0);
                    }
                }
            }

            #region plumbing

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length
            {
                get { throw new NotSupportedException(); }
            }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            #endregion

            #region sync result

            private sealed class SynchronousAsyncResult<T> : IAsyncResult
            {
                public T Result { get; set; }

                public bool IsCompleted => true;

                public WaitHandle AsyncWaitHandle
                {
                    get { throw new InvalidOperationException("Cannot wait on a synchronous result."); }
                }

                public object AsyncState { get; set; }

                public bool CompletedSynchronously => true;
            }

            #endregion
        }

        #endregion
    }

    #region == enum TlsHandlerState ==

    //[Flags]
    internal static class TlsHandlerState
    {
        public const int Authenticating = 1;
        public const int Authenticated = 1 << 1;
        public const int FailedAuthentication = 1 << 2;
        public const int ReadRequestedBeforeAuthenticated = 1 << 3;
        public const int FlushedBeforeHandshake = 1 << 4;
        public const int AuthenticationStarted = Authenticating | Authenticated | FailedAuthentication;
        public const int AuthenticationCompleted = Authenticated | FailedAuthentication;
    }

    #endregion

    #region == class TlsHandlerStateExtensions ==

    internal static class TlsHandlerStateExtensions
    {
        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static bool Has(this int value, int testValue) => (value & testValue) == testValue;

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static bool HasAny(this int value, int testValue) => (value & testValue) != 0;
    }

    #endregion
}