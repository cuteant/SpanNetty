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

namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;
    using System.Threading;
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

        private enum Framing
        {
            Unknown = 0,    // Initial before any frame is processd.
            BeforeSSL3,     // SSlv2
            SinceSSL3,      // SSlv3 & TLS
            Unified,        // Intermediate on first frame until response is processes.
            Invalid         // Somthing is wrong.
        }

        // SSL3/TLS protocol frames definitions.
        private enum ContentType : byte
        {
            ChangeCipherSpec = 20,
            Alert = 21,
            Handshake = 22,
            AppData = 23
        }

        // This is set on the first packet to figure out the framing style.
        private Framing _framing = Framing.Unknown;

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
            int packetLength = _packetLength;
            // If we calculated the length of the current SSL record before, use that information.
            if (packetLength > 0)
            {
                if (input.ReadableBytes < packetLength) { return; }
            }
            else
            {
                // Get the packet length and wait until we get a packets worth of data to unwrap.
                int readableBytes = input.ReadableBytes;
                if (readableBytes < TlsUtils.SSL_RECORD_HEADER_LENGTH) { return; }

                if (!State.HasAny(TlsHandlerState.AuthenticationCompleted))
                {
                    if (_framing == Framing.Unified || _framing == Framing.Unknown)
                    {
                        _framing = DetectFraming(input.UnreadSpan);
                    }
                }
                packetLength = GetFrameSize(_framing, input.UnreadSpan);
                if ((uint)packetLength > SharedConstants.TooBigOrNegative) // < 0
                {
                    HandleInvalidTlsFrameSize(input);
                }
                Debug.Assert(packetLength > 0);
                if (packetLength > readableBytes)
                {
                    // wait until the whole packet can be read
                    _packetLength = packetLength;
                    return;
                }
            }

            // Reset the state of this class so we can get the length of the next packet. We assume the entire packet will
            // be consumed by the SSLEngine.
            _packetLength = 0;
            try
            {
                Unwrap(context, input, input.ReaderIndex, packetLength);
                input.SkipBytes(packetLength);
                //Debug.Assert(bytesConsumed == packetLength || engine.isInboundDone() :
                //    "we feed the SSLEngine a packets worth of data: " + packetLength + " but it only consumed: " +
                //            bytesConsumed);
            }
            catch (Exception cause)
            {
                HandleUnwrapThrowable(context, cause);
            }
        }

        /// <summary>Unwraps inbound SSL records.</summary>
        private void Unwrap(IChannelHandlerContext ctx, IByteBuffer packet, int offset, int length)
        {
            bool pending = false;

            IByteBuffer outputBuffer = null;
            try
            {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
                ReadOnlyMemory<byte> inputIoBuffer = packet.GetReadableMemory(offset, length);
                _mediationStream.SetSource(inputIoBuffer, ctx.Allocator);
#else
                ArraySegment<byte> inputIoBuffer = packet.GetIoBuffer(offset, length);
                _mediationStream.SetSource(inputIoBuffer.Array, inputIoBuffer.Offset, ctx.Allocator);
#endif
                if (!EnsureAuthenticationCompleted(ctx))
                {
                    _mediationStream.ExpandSource(length);
                    return;
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

                _mediationStream.ExpandSource(length);

                if (currentReadFuture is object)
                {
                    // there was a read pending already, so we make sure we completed that first
                    if (currentReadFuture.IsCompleted)
                    {
                        int read = currentReadFuture.Result;
                        if (0u >= (uint)read)
                        {
                            // Stream closed
                            return;
                        }

                        // Now output the result of previous read and decide whether to do an extra read on the same source or move forward
                        outputBuffer.Advance(read);
                        _firedChannelRead = true;
                        ctx.FireChannelRead(outputBuffer);

                        currentReadFuture = null;
                        outputBuffer = null;

                        if (0u >= (uint)_mediationStream.SourceReadableBytes)
                        {
                            // we just made a frame available for reading but there was already pending read so SslStream read it out to make further progress there

                            if (read < outputBufferLength)
                            {
                                // SslStream returned non-full buffer and there's no more input to go through ->
                                // typically it means SslStream is done reading current frame so we skip
                                return;
                            }

                            // we've read out `read` bytes out of current packet to fulfil previously outstanding read
                            outputBufferLength = length - read;
                            if ((uint)(outputBufferLength - 1) > SharedConstants.TooBigOrNegative) // <= 0
                            {
                                // after feeding to SslStream current frame it read out more bytes than current packet size
                                outputBufferLength = c_fallbackReadBufferSize;
                            }
                        }
                    }
                }
                else
                {
                    // there was no pending read before so we estimate buffer of `length` bytes to be sufficient
                    outputBufferLength = length;
                }

                outputBuffer = ctx.Allocator.Buffer(outputBufferLength);
                currentReadFuture = ReadFromSslStreamAsync(outputBuffer, outputBufferLength);

                // read out the rest of SslStream's output (if any) at risk of going async
                // using FallbackReadBufferSize - buffer size we're ok to have pinned with the SslStream until it's done reading
                while (true)
                {
                    if (currentReadFuture is object)
                    {
                        if (!currentReadFuture.IsCompleted) { break; }
                        int read = currentReadFuture.Result;

                        if (0u >= (uint)read)
                        {
                            // Stream closed
                            return;
                        }

                        outputBuffer.Advance(read);
                        _firedChannelRead = true;
                        ctx.FireChannelRead(outputBuffer);

                        currentReadFuture = null;
                        outputBuffer = null;
                        if (0u >= (uint)_mediationStream.SourceReadableBytes)
                        {
                            return;
                        }
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
                _mediationStream.ResetSource(ctx.Allocator);
                if (!pending && outputBuffer is object)
                {
                    if (outputBuffer.IsReadable())
                    {
                        _firedChannelRead = true;
                        ctx.FireChannelRead(outputBuffer);
                    }
                    else
                    {
                        outputBuffer.SafeRelease();
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void HandleInvalidTlsFrameSize(IByteBuffer input)
        {
            // Not an SSL/TLS packet
            var ex = GetNotSslRecordException(input);
            _ = input.SkipBytes(input.ReadableBytes);

            // First fail the handshake promise as we may need to have access to the SSLEngine which may
            // be released because the user will remove the SslHandler in an exceptionCaught(...) implementation.
            HandleFailure(ex);
            throw ex;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void HandleUnwrapThrowable(IChannelHandlerContext context, Exception cause)
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static NotSslRecordException GetNotSslRecordException(IByteBuffer input)
        {
            return new NotSslRecordException(
                "not an SSL/TLS record: " + ByteBufferUtil.HexDump(input));
        }

        private void Decode_old(IChannelHandlerContext context, IByteBuffer input, List<object> output)
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
                    HandleInvalidTlsFrameSize(input);
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
                    HandleUnwrapThrowable(context, cause);
                }
            }
        }

        /// <summary>Unwraps inbound SSL records.</summary>
        private void Unwrap(IChannelHandlerContext ctx, IByteBuffer packet, int offset, int length, List<int> packetLengths, List<object> output)
        {
            if (0u >= (uint)packetLengths.Count) { ThrowHelper.ThrowArgumentException(); }

            //bool notifyClosure = false; // todo: netty/issues/137
            bool pending = false;

            IByteBuffer decodeOut = null;

            try
            {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
                ReadOnlyMemory<byte> inputIoBuffer = packet.GetReadableMemory(offset, length);
                _mediationStream.SetSource(inputIoBuffer, ctx.Allocator);
#else
                ArraySegment<byte> inputIoBuffer = packet.GetIoBuffer(offset, length);
                _mediationStream.SetSource(inputIoBuffer.Array, inputIoBuffer.Offset, ctx.Allocator);
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

                    decodeOut = _pendingSslStreamReadBuffer;
                    outputBufferLength = decodeOut.WritableBytes;

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
                        AddBufferToOutput(decodeOut, read, output);

                        currentReadFuture = null;
                        decodeOut = null;
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

                    decodeOut = ctx.Allocator.Buffer(outputBufferLength);
                    currentReadFuture = ReadFromSslStreamAsync(decodeOut, outputBufferLength);
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
                        AddBufferToOutput(decodeOut, read, output);
                    }
                    decodeOut = ctx.Allocator.Buffer(c_fallbackReadBufferSize);
                    currentReadFuture = ReadFromSslStreamAsync(decodeOut, c_fallbackReadBufferSize);
                }

                pending = true;
                _pendingSslStreamReadBuffer = decodeOut;
                _pendingSslStreamReadFuture = currentReadFuture;
            }
            finally
            {
                _mediationStream.ResetSource(ctx.Allocator);
                if (!pending && decodeOut is object)
                {
                    if (decodeOut.IsReadable())
                    {
                        output.Add(decodeOut);
                    }
                    else
                    {
                        decodeOut.SafeRelease();
                    }
                }
            }
        }

        private static void AddBufferToOutput(IByteBuffer outputBuffer, int length, List<object> output)
        {
            Debug.Assert(length > 0);
            outputBuffer.Advance(length);
            output.Add(outputBuffer);
        }

        // We need at least 5 bytes to determine what we have.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Framing DetectFraming(in ReadOnlySpan<byte> bytes)
        {
            /* PCTv1.0 Hello starts with
             * RECORD_LENGTH_MSB  (ignore)
             * RECORD_LENGTH_LSB  (ignore)
             * PCT1_CLIENT_HELLO  (must be equal)
             * PCT1_CLIENT_VERSION_MSB (if version greater than PCTv1)
             * PCT1_CLIENT_VERSION_LSB (if version greater than PCTv1)
             *
             * ... PCT hello ...
             */

            /* Microsoft Unihello starts with
             * RECORD_LENGTH_MSB  (ignore)
             * RECORD_LENGTH_LSB  (ignore)
             * SSL2_CLIENT_HELLO  (must be equal)
             * SSL2_CLIENT_VERSION_MSB (if version greater than SSLv2) ( or v3)
             * SSL2_CLIENT_VERSION_LSB (if version greater than SSLv2) ( or v3)
             *
             * ... SSLv2 Compatible Hello ...
             */

            /* SSLv2 CLIENT_HELLO starts with
             * RECORD_LENGTH_MSB  (ignore)
             * RECORD_LENGTH_LSB  (ignore)
             * SSL2_CLIENT_HELLO  (must be equal)
             * SSL2_CLIENT_VERSION_MSB (if version greater than SSLv2) ( or v3)
             * SSL2_CLIENT_VERSION_LSB (if version greater than SSLv2) ( or v3)
             *
             * ... SSLv2 CLIENT_HELLO ...
             */

            /* SSLv2 SERVER_HELLO starts with
             * RECORD_LENGTH_MSB  (ignore)
             * RECORD_LENGTH_LSB  (ignore)
             * SSL2_SERVER_HELLO  (must be equal)
             * SSL2_SESSION_ID_HIT (ignore)
             * SSL2_CERTIFICATE_TYPE (ignore)
             * SSL2_CLIENT_VERSION_MSB (if version greater than SSLv2) ( or v3)
             * SSL2_CLIENT_VERSION_LSB (if version greater than SSLv2) ( or v3)
             *
             * ... SSLv2 SERVER_HELLO ...
             */

            /* SSLv3 Type 2 Hello starts with
              * RECORD_LENGTH_MSB  (ignore)
              * RECORD_LENGTH_LSB  (ignore)
              * SSL2_CLIENT_HELLO  (must be equal)
              * SSL2_CLIENT_VERSION_MSB (if version greater than SSLv3)
              * SSL2_CLIENT_VERSION_LSB (if version greater than SSLv3)
              *
              * ... SSLv2 Compatible Hello ...
              */

            /* SSLv3 Type 3 Hello starts with
             * 22 (HANDSHAKE MESSAGE)
             * VERSION MSB
             * VERSION LSB
             * RECORD_LENGTH_MSB  (ignore)
             * RECORD_LENGTH_LSB  (ignore)
             * HS TYPE (CLIENT_HELLO)
             * 3 bytes HS record length
             * HS Version
             * HS Version
             */

            /* SSLv2 message codes
             * SSL_MT_ERROR                0
             * SSL_MT_CLIENT_HELLO         1
             * SSL_MT_CLIENT_MASTER_KEY    2
             * SSL_MT_CLIENT_FINISHED      3
             * SSL_MT_SERVER_HELLO         4
             * SSL_MT_SERVER_VERIFY        5
             * SSL_MT_SERVER_FINISHED      6
             * SSL_MT_REQUEST_CERTIFICATE  7
             * SSL_MT_CLIENT_CERTIFICATE   8
             */

            int version = -1;

            // If the first byte is SSL3 HandShake, then check if we have a SSLv3 Type3 client hello.
            if (bytes[0] == (byte)ContentType.Handshake || bytes[0] == (byte)ContentType.AppData
                || bytes[0] == (byte)ContentType.Alert)
            {
                if (bytes.Length < 3)
                {
                    return Framing.Invalid;
                }

                version = (bytes[1] << 8) | bytes[2];
                if (version < 0x300 || version >= 0x500)
                {
                    return Framing.Invalid;
                }

                //
                // This is an SSL3 Framing
                //
                return Framing.SinceSSL3;
            }

            if (bytes.Length < 3)
            {
                return Framing.Invalid;
            }

            if (bytes[2] > 8)
            {
                return Framing.Invalid;
            }

            if (bytes[2] == 0x1)  // SSL_MT_CLIENT_HELLO
            {
                if (bytes.Length >= 5)
                {
                    version = (bytes[3] << 8) | bytes[4];
                }
            }
            else if (bytes[2] == 0x4) // SSL_MT_SERVER_HELLO
            {
                if (bytes.Length >= 7)
                {
                    version = (bytes[5] << 8) | bytes[6];
                }
            }

            if (version != -1)
            {
                // If this is the first packet, the client may start with an SSL2 packet
                // but stating that the version is 3.x, so check the full range.
                // For the subsequent packets we assume that an SSL2 packet should have a 2.x version.
                if (_framing == Framing.Unknown)
                {
                    if (version != 0x0002 && (version < 0x200 || version >= 0x500))
                    {
                        return Framing.Invalid;
                    }
                }
                else
                {
                    if (version != 0x0002)
                    {
                        return Framing.Invalid;
                    }
                }
            }

            // When server has replied the framing is already fixed depending on the prior client packet
            if (!_isServer || _framing == Framing.Unified)
            {
                return Framing.BeforeSSL3;
            }

            return Framing.Unified; // Will use Ssl2 just for this frame.
        }

        // Returns TLS Frame size.
        private static int GetFrameSize(Framing framing, in ReadOnlySpan<byte> buffer)
        {
            int payloadSize = -1;
            switch (framing)
            {
                case Framing.Unified:
                case Framing.BeforeSSL3:
                    // Note: Cannot detect version mismatch for <= SSL2

                    if ((buffer[0] & 0x80) != 0)
                    {
                        // Two bytes
                        payloadSize = (((buffer[0] & 0x7f) << 8) | buffer[1]) + 2;
                    }
                    else
                    {
                        // Three bytes
                        payloadSize = (((buffer[0] & 0x3f) << 8) | buffer[1]) + 3;
                    }

                    break;
                case Framing.SinceSSL3:
                    payloadSize = ((buffer[3] << 8) | buffer[4]) + 5;
                    break;
            }

            return payloadSize;
        }

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
        private Task<int> ReadFromSslStreamAsync(IByteBuffer outputBuffer, int outputBufferLength)
        {
            if (_sslStream is null) { return Task.FromResult(0); }
            Memory<byte> outlet = outputBuffer.GetMemory(outputBuffer.WriterIndex, outputBufferLength);
            return _sslStream.ReadAsync(outlet).AsTask();
        }
#else
        private Task<int> ReadFromSslStreamAsync(IByteBuffer outputBuffer, int outputBufferLength)
        {
            if (_sslStream is null) { return Task.FromResult(0); }
            ArraySegment<byte> outlet = outputBuffer.GetIoBuffer(outputBuffer.WriterIndex, outputBufferLength);
            return _sslStream.ReadAsync(outlet.Array, outlet.Offset, outlet.Count);
        }
#endif
    }
}
