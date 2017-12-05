// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
  using System;
  using System.Collections.Generic;
  using System.Diagnostics.Contracts;
  using System.IO;
  using System.Net.Security;
  using System.Runtime.ExceptionServices;
  using System.Security.Cryptography.X509Certificates;
  using System.Threading;
  using System.Threading.Tasks;
  using DotNetty.Buffers;
  using DotNetty.Codecs;
  using DotNetty.Common.Concurrency;
  using DotNetty.Common.Utilities;
  using DotNetty.Transport.Channels;

#if !DESKTOPCLR && (NET40 || NET45 || NET451 || NET46 || NET461 || NET462 || NET47 || NET471)
  确保编译不出问题
#endif
#if !NETSTANDARD && (NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETSTANDARD2_0)
  确保编译不出问题
#endif

  public sealed class TlsHandler : ByteToMessageDecoder
  {
    #region @@ Fields @@

    private readonly TlsSettings _settings;
    private const int c_fallbackReadBufferSize = 256;
    private const int c_unencryptedWriteBatchSize = 14 * 1024;

    private static readonly Exception s_channelClosedException = new IOException("Channel is closed");
#if !NET40
    private static readonly Action<Task, object> s_handshakeCompletionCallback = new Action<Task, object>(HandleHandshakeCompleted);
#endif

    private readonly SslStream _sslStream;
    private readonly MediationStream _mediationStream;
    private readonly TaskCompletionSource _closeFuture;

    private TlsHandlerState _state;
    private int _packetLength;
    private volatile IChannelHandlerContext _capturedContext;
    private BatchingPendingWriteQueue _pendingUnencryptedWrites;
    private Task _lastContextWriteTask;
    private bool _firedChannelRead;
    private IByteBuffer _pendingSslStreamReadBuffer;
    private Task<int> _pendingSslStreamReadFuture;

    #endregion

    #region @@ Constructors @@

    public TlsHandler(TlsSettings settings)
      : this(stream => new SslStream(stream, true), settings)
    {
    }

    public TlsHandler(Func<Stream, SslStream> sslStreamFactory, TlsSettings settings)
    {
      Contract.Requires(sslStreamFactory != null);
      Contract.Requires(settings != null);

      _settings = settings;
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

    private bool IsServer => _settings is ServerTlsSettings;

    #endregion

    #region -- IDisposable Members --

    public void Dispose() => _sslStream?.Dispose();

    #endregion

    #region -- ChannelActive --

    public override void ChannelActive(IChannelHandlerContext context)
    {
      base.ChannelActive(context);

      if (!IsServer)
      {
        EnsureAuthenticated();
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
      _capturedContext = context;
      _pendingUnencryptedWrites = new BatchingPendingWriteQueue(context, c_unencryptedWriteBatchSize);
      if (context.Channel.Active && !IsServer)
      {
        // todo: support delayed initialization on an existing/active channel if in client mode
        EnsureAuthenticated();
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

        Contract.Assert(encryptedPacketLength > 0);

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
          _firedChannelRead = output.Count > 0;
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
      if (!ctx.Channel.Configuration.AutoRead && (!_firedChannelRead || !_state.HasAny(TlsHandlerState.AuthenticationCompleted)))
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
      Contract.Requires(packetLengths.Count > 0);

      //bool notifyClosure = false; // todo: netty/issues/137
      bool pending = false;

      IByteBuffer outputBuffer = null;

      try
      {
        ArraySegment<byte> inputIoBuffer = packet.GetIoBuffer(offset, length);
        _mediationStream.SetSource(inputIoBuffer.Array, inputIoBuffer.Offset);

        int packetIndex = 0;

        while (!EnsureAuthenticated())
        {
          _mediationStream.ExpandSource(packetLengths[packetIndex]);
          if (++packetIndex == packetLengths.Count)
          {
            return;
          }
        }

        Task<int> currentReadFuture = _pendingSslStreamReadFuture;

        int outputBufferLength;

        if (currentReadFuture != null)
        {
          // restoring context from previous read
          Contract.Assert(_pendingSslStreamReadBuffer != null);

          outputBuffer = _pendingSslStreamReadBuffer;
          outputBufferLength = outputBuffer.WritableBytes;
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

          if (currentReadFuture != null)
          {
            // there was a read pending already, so we make sure we completed that first

            if (!currentReadFuture.IsCompleted)
            {
              // we did feed the whole current packet to SslStream yet it did not produce any result -> move to the next packet in input
              Contract.Assert(_mediationStream.SourceReadableBytes == 0);

              continue;
            }

            int read = currentReadFuture.Result;

            // Now output the result of previous read and decide whether to do an extra read on the same source or move forward
            AddBufferToOutput(outputBuffer, read, output);

            currentReadFuture = null;
            if (_mediationStream.SourceReadableBytes == 0)
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
          if (currentReadFuture != null)
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
        if (!pending && outputBuffer != null)
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
      Contract.Assert(length > 0);
      output.Add(outputBuffer.SetWriterIndex(outputBuffer.WriterIndex + length));
    }

    #endregion

    #region ** ReadFromSslStreamAsync **

    private Task<int> ReadFromSslStreamAsync(IByteBuffer outputBuffer, int outputBufferLength)
    {
      ArraySegment<byte> outlet = outputBuffer.GetIoBuffer(outputBuffer.WriterIndex, outputBufferLength);
      return _sslStream.ReadAsync(outlet.Array, outlet.Offset, outlet.Count);
    }

    #endregion

    #region -- Read --

    public override void Read(IChannelHandlerContext context)
    {
      TlsHandlerState oldState = _state;
      if (!oldState.HasAny(TlsHandlerState.AuthenticationCompleted))
      {
        _state = oldState | TlsHandlerState.ReadRequestedBeforeAuthenticated;
      }

      context.Read();
    }

    #endregion

    #region ** EnsureAuthenticated **

    private bool EnsureAuthenticated()
    {
      TlsHandlerState oldState = _state;
      if (!oldState.HasAny(TlsHandlerState.AuthenticationStarted))
      {
        _state = oldState | TlsHandlerState.Authenticating;
        var serverSettings = _settings as ServerTlsSettings;
        if (serverSettings != null)
        {
#if !NET40
          _sslStream.AuthenticateAsServerAsync(serverSettings.Certificate,
                                               serverSettings.NegotiateClientCertificate,
                                               serverSettings.EnabledProtocols,
                                               serverSettings.CheckCertificateRevocation)
                    .ContinueWith(s_handshakeCompletionCallback, this, TaskContinuationOptions.ExecuteSynchronously);
#else
          _sslStream.BeginAuthenticateAsServer(serverSettings.Certificate,
                                               serverSettings.NegotiateClientCertificate,
                                               serverSettings.EnabledProtocols,
                                               serverSettings.CheckCertificateRevocation,
                                               Server_HandleHandshakeCompleted,
                                               this);
#endif
        }
        else
        {
          var clientSettings = (ClientTlsSettings)_settings;
#if !NET40
          _sslStream.AuthenticateAsClientAsync(clientSettings.TargetHost,
                                               clientSettings.X509CertificateCollection,
                                               clientSettings.EnabledProtocols,
                                               clientSettings.CheckCertificateRevocation)
                    .ContinueWith(s_handshakeCompletionCallback, this, TaskContinuationOptions.ExecuteSynchronously);
#else
          _sslStream.BeginAuthenticateAsClient(clientSettings.TargetHost,
                                               clientSettings.X509CertificateCollection,
                                               clientSettings.EnabledProtocols,
                                               clientSettings.CheckCertificateRevocation,
                                               Client_HandleHandshakeCompleted,
                                               this);
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
      TlsHandlerState oldState;
      try
      {
        self._sslStream.EndAuthenticateAsClient(result);
      }
      catch (Exception ex)
      {
        // ReSharper disable once AssignNullToNotNullAttribute -- task.Exception will be present as task is faulted
        oldState = self._state;
        Contract.Assert(!oldState.HasAny(TlsHandlerState.AuthenticationCompleted));
        self.HandleFailure(ex);
        return;
      }

      oldState = self._state;

      Contract.Assert(!oldState.HasAny(TlsHandlerState.AuthenticationCompleted));
      self._state = (oldState | TlsHandlerState.Authenticated) & ~(TlsHandlerState.Authenticating | TlsHandlerState.FlushedBeforeHandshake);

      self._capturedContext.FireUserEventTriggered(TlsHandshakeCompletionEvent.Success);

      if (oldState.Has(TlsHandlerState.ReadRequestedBeforeAuthenticated) && !self._capturedContext.Channel.Configuration.AutoRead)
      {
        self._capturedContext.Read();
      }

      if (oldState.Has(TlsHandlerState.FlushedBeforeHandshake))
      {
        self.Wrap(self._capturedContext);
        self._capturedContext.Flush();
      }
    }

    private static void Server_HandleHandshakeCompleted(IAsyncResult result)
    {
      var self = (TlsHandler)result.AsyncState;
      TlsHandlerState oldState;
      try
      {
        self._sslStream.EndAuthenticateAsServer(result);
      }
      catch (Exception ex)
      {
        // ReSharper disable once AssignNullToNotNullAttribute -- task.Exception will be present as task is faulted
        oldState = self._state;
        Contract.Assert(!oldState.HasAny(TlsHandlerState.AuthenticationCompleted));
        self.HandleFailure(ex);
        return;
      }

      oldState = self._state;

      Contract.Assert(!oldState.HasAny(TlsHandlerState.AuthenticationCompleted));
      self._state = (oldState | TlsHandlerState.Authenticated) & ~(TlsHandlerState.Authenticating | TlsHandlerState.FlushedBeforeHandshake);

      self._capturedContext.FireUserEventTriggered(TlsHandshakeCompletionEvent.Success);

      if (oldState.Has(TlsHandlerState.ReadRequestedBeforeAuthenticated) && !self._capturedContext.Channel.Configuration.AutoRead)
      {
        self._capturedContext.Read();
      }

      if (oldState.Has(TlsHandlerState.FlushedBeforeHandshake))
      {
        self.Wrap(self._capturedContext);
        self._capturedContext.Flush();
      }
    }
#else
    private static void HandleHandshakeCompleted(Task task, object state)
    {
      var self = (TlsHandler)state;
      switch (task.Status)
      {
        case TaskStatus.RanToCompletion:
          {
            TlsHandlerState oldState = self._state;

            Contract.Assert(!oldState.HasAny(TlsHandlerState.AuthenticationCompleted));
            self._state = (oldState | TlsHandlerState.Authenticated) & ~(TlsHandlerState.Authenticating | TlsHandlerState.FlushedBeforeHandshake);

            self._capturedContext.FireUserEventTriggered(TlsHandshakeCompletionEvent.Success);

            if (oldState.Has(TlsHandlerState.ReadRequestedBeforeAuthenticated) && !self._capturedContext.Channel.Configuration.AutoRead)
            {
              self._capturedContext.Read();
            }

            if (oldState.Has(TlsHandlerState.FlushedBeforeHandshake))
            {
              self.Wrap(self._capturedContext);
              self._capturedContext.Flush();
            }
            break;
          }
        case TaskStatus.Canceled:
        case TaskStatus.Faulted:
          {
            // ReSharper disable once AssignNullToNotNullAttribute -- task.Exception will be present as task is faulted
            TlsHandlerState oldState = self._state;
            Contract.Assert(!oldState.HasAny(TlsHandlerState.AuthenticationCompleted));
            self.HandleFailure(task.Exception);
            break;
          }
        default:
          throw new ArgumentOutOfRangeException(nameof(task), "Unexpected task status: " + task.Status);
      }
    }
#endif

    #endregion

    #region -- WriteAsync --

    public override Task WriteAsync(IChannelHandlerContext context, object message)
    {
      if (!(message is IByteBuffer))
      {
        return TaskUtil.FromException(new UnsupportedMessageTypeException(message, typeof(IByteBuffer)));
      }
      return _pendingUnencryptedWrites.Add(message);
    }

    #endregion

    #region -- Flush --

    public override void Flush(IChannelHandlerContext context)
    {
      if (_pendingUnencryptedWrites.IsEmpty)
      {
        _pendingUnencryptedWrites.Add(Unpooled.Empty);
      }

      if (!EnsureAuthenticated())
      {
        _state |= TlsHandlerState.FlushedBeforeHandshake;
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
      Contract.Assert(context == _capturedContext);

      IByteBuffer buf = null;
      try
      {
        while (true)
        {
          List<object> messages = _pendingUnencryptedWrites.Current;
          if (messages == null || messages.Count == 0)
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

          TaskCompletionSource promise = _pendingUnencryptedWrites.Remove();
          Task task = _lastContextWriteTask;
          if (task != null)
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

    private void FinishWrap(byte[] buffer, int offset, int count)
    {
      IByteBuffer output;
      if (count == 0)
      {
        output = Unpooled.Empty;
      }
      else
      {
        output = _capturedContext.Allocator.Buffer(count);
        output.WriteBytes(buffer, offset, count);
      }

      _lastContextWriteTask = _capturedContext.WriteAsync(output);
    }

    #endregion

    #region ** FinishWrapNonAppDataAsync **

    private Task FinishWrapNonAppDataAsync(byte[] buffer, int offset, int count)
    {
      var future = _capturedContext.WriteAndFlushAsync(Unpooled.WrappedBuffer(buffer, offset, count));
      this.ReadIfNeeded(_capturedContext);
      return future;
    }

    #endregion

    #region -- CloseAsync --

    public override Task CloseAsync(IChannelHandlerContext context)
    {
      _closeFuture.TryComplete();
      _sslStream.Dispose();
      return base.CloseAsync(context);
    }

    #endregion

    #region ** HandleFailure **

    private void HandleFailure(Exception cause)
    {
      // Release all resources such as internal buffers that SSLEngine
      // is managing.

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
        //if (msg == null || !msg.contains("possible truncation attack"))
        //{
        //    //Logger.Debug("{} SSLEngine.closeInbound() raised an exception.", ctx.channel(), e);
        //}
      }
      NotifyHandshakeFailure(cause);
      _pendingUnencryptedWrites.RemoveAndFailAll(cause);
    }

    #endregion

    #region ** NotifyHandshakeFailure **

    private void NotifyHandshakeFailure(Exception cause)
    {
      if (!_state.HasAny(TlsHandlerState.AuthenticationCompleted))
      {
        // handshake was not completed yet => TlsHandler react to failure by closing the channel
        _state = (_state | TlsHandlerState.FailedAuthentication) & ~TlsHandlerState.Authenticating;
        _capturedContext.FireUserEventTriggered(new TlsHandshakeCompletionEvent(cause));
        CloseAsync(_capturedContext);
      }
    }

    #endregion

    #region ** class MediationStream **

    private sealed class MediationStream : Stream
    {
      private readonly TlsHandler _owner;
      private byte[] _input;
      private int _inputStartOffset;
      private int _inputOffset;
      private int _inputLength;
      private TaskCompletionSource<int> _readCompletionSource;
      private ArraySegment<byte> _sslOwnedBuffer;
#if NETSTANDARD1_3
      private int _readByteCount;
#else
      private SynchronousAsyncResult<int> _syncReadResult;
      private AsyncCallback _readCallback;
      private TaskCompletionSource _writeCompletion;
      private AsyncCallback _writeCallback;
#endif

      public MediationStream(TlsHandler owner)
      {
        _owner = owner;
      }

      public int SourceReadableBytes => _inputLength - _inputOffset;

      public void SetSource(byte[] source, int offset)
      {
        _input = source;
        _inputStartOffset = offset;
        _inputOffset = 0;
        _inputLength = 0;
      }

      public void ResetSource()
      {
        _input = null;
        _inputLength = 0;
      }

      public void ExpandSource(int count)
      {
        Contract.Assert(_input != null);

        _inputLength += count;

        TaskCompletionSource<int> promise = _readCompletionSource;
        if (promise == null)
        {
          // there is no pending read operation - keep for future
          return;
        }

        ArraySegment<byte> sslBuffer = _sslOwnedBuffer;

#if NETSTANDARD1_3
        this._readByteCount = this.ReadFromInput(sslBuffer.Array, sslBuffer.Offset, sslBuffer.Count);
        // hack: this tricks SslStream's continuation to run synchronously instead of dispatching to TP. Remove once Begin/EndRead are available. 
        new Task(
            ms =>
            {
                var self = (MediationStream)ms;
                TaskCompletionSource<int> p = self._readCompletionSource;
                this._readCompletionSource = null;
                p.TrySetResult(self._readByteCount);
            },
            this)
            .RunSynchronously(TaskScheduler.Default);
#else
        int read = ReadFromInput(sslBuffer.Array, sslBuffer.Offset, sslBuffer.Count);
        _readCompletionSource = null;
        promise.TrySetResult(read);
        _readCallback?.Invoke(promise.Task);
#endif
      }

#if !NET40

      public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
      {
        if (_inputLength - _inputOffset > 0)
        {
          // we have the bytes available upfront - write out synchronously
          int read = ReadFromInput(buffer, offset, count);
          return Task.FromResult(read);
        }

        // take note of buffer - we will pass bytes there once available
        _sslOwnedBuffer = new ArraySegment<byte>(buffer, offset, count);
        _readCompletionSource = new TaskCompletionSource<int>();
        return _readCompletionSource.Task;
      }

#endif

//#if DESKTOPCLR
      public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
      {
        if (_inputLength - _inputOffset > 0)
        {
          // we have the bytes available upfront - write out synchronously
          int read = ReadFromInput(buffer, offset, count);
          return PrepareSyncReadResult(read, state);
        }

        // take note of buffer - we will pass bytes there once available
        _sslOwnedBuffer = new ArraySegment<byte>(buffer, offset, count);
        _readCompletionSource = new TaskCompletionSource<int>(state);
        _readCallback = callback;
        return _readCompletionSource.Task;
      }

      public override int EndRead(IAsyncResult asyncResult)
      {
        SynchronousAsyncResult<int> syncResult = _syncReadResult;
        if (ReferenceEquals(asyncResult, syncResult))
        {
          return syncResult.Result;
        }

        Contract.Assert(!((Task<int>)asyncResult).IsCanceled);

        try
        {
          return ((Task<int>)asyncResult).Result;
        }
        catch (AggregateException ex)
        {
#if !NET40
          ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
#else
          throw ExceptionEnlightenment.PrepareForRethrow(ex.InnerException);
#endif
          throw; // unreachable
        }
        finally
        {
          _readCompletionSource = null;
          _readCallback = null;
          _sslOwnedBuffer = default(ArraySegment<byte>);
        }
      }

      private IAsyncResult PrepareSyncReadResult(int readBytes, object state)
      {
        // it is safe to reuse sync result object as it can't lead to leak (no way to attach to it via handle)
        SynchronousAsyncResult<int> result = _syncReadResult ?? (_syncReadResult = new SynchronousAsyncResult<int>());
        result.Result = readBytes;
        result.AsyncState = state;
        return result;
      }
//#endif

      public override void Write(byte[] buffer, int offset, int count) => _owner.FinishWrap(buffer, offset, count);

#if !NET40
      public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
          => _owner.FinishWrapNonAppDataAsync(buffer, offset, count);

#endif

//#if DESKTOPCLR
#if !NET40
      private static readonly Action<Task, object> s_writeCompleteCallback = HandleChannelWriteComplete;
#endif

      public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
      {
#if NET40
        Task task = _owner.FinishWrapNonAppDataAsync(buffer, offset, count);
#else
        Task task = this.WriteAsync(buffer, offset, count);
#endif
        switch (task.Status)
        {
          case TaskStatus.RanToCompletion:
            // write+flush completed synchronously (and successfully)
            var result = new SynchronousAsyncResult<int>();
            result.AsyncState = state;
            callback(result);
            return result;

          default:
            _writeCallback = callback;
            var tcs = new TaskCompletionSource(state);
            _writeCompletion = tcs;
#if !NET40
            task.ContinueWith(s_writeCompleteCallback, this, TaskContinuationOptions.ExecuteSynchronously);
#else
            Action<Task> continuationAction = completed => HandleChannelWriteComplete(completed, this);
            task.ContinueWith(continuationAction, TaskContinuationOptions.ExecuteSynchronously);
#endif
            return tcs.Task;
        }
      }

      private static void HandleChannelWriteComplete(Task writeTask, object state)
      {
        var self = (MediationStream)state;
        switch (writeTask.Status)
        {
          case TaskStatus.RanToCompletion:
            self._writeCompletion.TryComplete();
            break;

          case TaskStatus.Canceled:
            self._writeCompletion.TrySetCanceled();
            break;

          case TaskStatus.Faulted:
            self._writeCompletion.TrySetException(writeTask.Exception);
            break;

          default:
            throw new ArgumentOutOfRangeException("Unexpected task status: " + writeTask.Status);
        }

        self._writeCallback?.Invoke(self._writeCompletion.Task);
      }

      public override void EndWrite(IAsyncResult asyncResult)
      {
        _writeCallback = null;
        _writeCompletion = null;

        if (asyncResult is SynchronousAsyncResult<int>)
        {
          return;
        }

        try
        {
          ((Task<int>)asyncResult).Wait();
        }
        catch (AggregateException ex)
        {
#if !NET40
          ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
#else
          throw ExceptionEnlightenment.PrepareForRethrow(ex.InnerException);
#endif
          throw;
        }
      }
//#endif

      private int ReadFromInput(byte[] destination, int destinationOffset, int destinationCapacity)
      {
        Contract.Assert(destination != null);

        byte[] source = _input;
        int readableBytes = _inputLength - _inputOffset;
        int length = Math.Min(readableBytes, destinationCapacity);
        Buffer.BlockCopy(source, _inputStartOffset + _inputOffset, destination, destinationOffset, length);
        _inputOffset += length;
        return length;
      }

      public override void Flush()
      {
        // NOOP: called on SslStream.Close
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

  [Flags]
  internal enum TlsHandlerState
  {
    Authenticating = 1,
    Authenticated = 1 << 1,
    FailedAuthentication = 1 << 2,
    ReadRequestedBeforeAuthenticated = 1 << 3,
    FlushedBeforeHandshake = 1 << 4,
    AuthenticationStarted = Authenticating | Authenticated | FailedAuthentication,
    AuthenticationCompleted = Authenticated | FailedAuthentication
  }

  #endregion

  #region == class TlsHandlerStateExtensions ==

  internal static class TlsHandlerStateExtensions
  {
    public static bool Has(this TlsHandlerState value, TlsHandlerState testValue) => (value & testValue) == testValue;

    public static bool HasAny(this TlsHandlerState value, TlsHandlerState testValue) => (value & testValue) != 0;
  }

  #endregion
}