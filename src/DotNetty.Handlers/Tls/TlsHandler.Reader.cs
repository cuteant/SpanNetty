
namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    partial class TlsHandler
    {
        private const int c_fallbackReadBufferSize = 256;

        private int _packetLength;
        private bool _firedChannelRead;
        private IByteBuffer _pendingSslStreamReadBuffer;
        private Task<int> _pendingSslStreamReadFuture;

        public override void Read(IChannelHandlerContext context)
        {
            var oldState = State;
            if (!oldState.HasAny(TlsHandlerState.AuthenticationCompleted))
            {
                State = oldState | TlsHandlerState.ReadRequestedBeforeAuthenticated;
            }

            _ = context.Read();
        }

        public override void ChannelReadComplete(IChannelHandlerContext ctx)
        {
            // Discard bytes of the cumulation buffer if needed.
            DiscardSomeReadBytes();

            ReadIfNeeded(ctx);

            _firedChannelRead = false;
            _ = ctx.FireChannelReadComplete();
        }

        private void ReadIfNeeded(IChannelHandlerContext ctx)
        {
            // if handshake is not finished yet, we need more data
            if (!ctx.Channel.Configuration.IsAutoRead && (!_firedChannelRead || !State.HasAny(TlsHandlerState.AuthenticationCompleted)))
            {
                // No auto-read used and no message was passed through the ChannelPipeline or the handshake was not completed
                // yet, which means we need to trigger the read to ensure we will not stall
                _ = ctx.Read();
            }
        }

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
                    packetLengths = new List<int>(4) { _packetLength };
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
                if (encryptedPacketLength == TlsUtils.NOT_ENCRYPTED)
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

                _ = input.SkipBytes(totalLength);
                try
                {
                    Unwrap(context, input, startOffset, totalLength, packetLengths, output);

                    if (!_firedChannelRead)
                    {
                        // Check first if firedChannelRead is not set yet as it may have been set in a
                        // previous decode(...) call.
                        _firedChannelRead = (uint)output.Count > 0u;
                    }
                }
                catch (Exception cause)
                {
                    try
                    {
                        // We need to flush one time as there may be an alert that we should send to the remote peer because
                        // of the SSLException reported here.
                        WrapAndFlush(context);
                    }
                    // TODO revisit
                    //catch (IOException)
                    //{
                    //    if (s_logger.DebugEnabled)
                    //    {
                    //        s_logger.Debug("SSLException during trying to call SSLEngine.wrap(...)" +
                    //                " because of an previous SSLException, ignoring...", ex);
                    //    }
                    //}
                    finally
                    {
                        HandleFailure(cause);
                    }
                    ExceptionDispatchInfo.Capture(cause).Throw();
                }
            }

            if (nonSslRecord)
            {
                // Not an SSL/TLS packet
                var ex = GetNotSslRecordException(input);
                _ = input.SkipBytes(input.ReadableBytes);

                // First fail the handshake promise as we may need to have access to the SSLEngine which may
                // be released because the user will remove the SslHandler in an exceptionCaught(...) implementation.
                HandleFailure(ex);

                _ = context.FireExceptionCaught(ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static NotSslRecordException GetNotSslRecordException(IByteBuffer input)
        {
            return new NotSslRecordException(
                "not an SSL/TLS record: " + ByteBufferUtil.HexDump(input));
        }

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
                    if ((uint)(++packetIndex) >= (uint)packetLengths.Count)
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
                            if ((uint)(outputBufferLength - 1) > SharedConstants.TooBigOrNegative) // <= 0
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

        private static void AddBufferToOutput(IByteBuffer outputBuffer, int length, List<object> output)
        {
            Debug.Assert(length > 0);
            output.Add(outputBuffer.SetWriterIndex(outputBuffer.WriterIndex + length));
        }

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
    }
}
