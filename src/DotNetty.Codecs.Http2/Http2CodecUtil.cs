// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Constants and utility method used for encoding/decoding HTTP2 frames.
    /// </summary>
    public static class Http2CodecUtil
    {
        public const int ConnectionStreamId = 0;

        public const int HttpUpgradeStreamId = 1;

        public static readonly AsciiString HttpUpgradeSettingsHeader = AsciiString.Cached("HTTP2-Settings");

        public static readonly AsciiString HttpUpgradeProtocolName = AsciiString.Cached("h2c");

        public static readonly AsciiString TlsUpgradeProtocolName = AsciiString.Cached(ApplicationProtocolNames.Http2);

        public const int PingFramePayloadLength = 8;

        public const short MaxUnsignedByte = 0xff;

        /// <summary>
        /// The maximum number of padding bytes. That is the 255 padding bytes appended to the end of a frame and the 1 byte
        /// pad length field.
        /// </summary>
        public const int MaxPadding = 256;

        public const long MaxUnsignedInt = 0xffffffffL;

        public const int FrameHeaderLength = 9;

        public const int SettingEntryLength = 6;

        public const int PriorityEntryLength = 5;

        public const int IntFieldLength = 4;

        public const short MaxWeight = 256;

        public const short MinWeight = 1;

        static readonly IByteBuffer ConnectionPreface =
            UnreleasableBuffer(DirectBuffer(24).WriteBytes(TextEncodings.UTF8NoBOM.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n")).AsReadOnly());

        const int MaxPaddingLengthLength = 1;

        public const int DataFrameHeaderLength = FrameHeaderLength + MaxPaddingLengthLength;

        public const int HeadersFrameHeaderLength =
            FrameHeaderLength + MaxPaddingLengthLength + IntFieldLength + 1;

        public const int PriorityFrameLength = FrameHeaderLength + PriorityEntryLength;

        public const int RstStreamFrameLength = FrameHeaderLength + IntFieldLength;

        public const int PushPromiseFrameHeaderLength =
            FrameHeaderLength + MaxPaddingLengthLength + IntFieldLength;

        public const int GoAwayFrameHeaderLength = FrameHeaderLength + 2 * IntFieldLength;

        public const int WindowUpdateFrameLength = FrameHeaderLength + IntFieldLength;

        public const int ContinuationFrameHeaderLength = FrameHeaderLength + MaxPaddingLengthLength;

        public const char SettingsHeaderTableSize = (char)1;

        public const char SettingsEnablePush = (char)2;

        public const char SettingsMaxConcurrentStreams = (char)3;

        public const char SettingsInitialWindowSize = (char)4;

        public const char SettingsMaxFrameSize = (char)5;

        public const char SettingsMaxHeaderListSize = (char)6;

        public const int NumStandardSettings = 6;

        public const long MaxHeaderTableSize = MaxUnsignedInt;

        public const long MaxConcurrentStreams = MaxUnsignedInt;

        public const int MaxInitialWindowSize = int.MaxValue;

        public const int MaxFrameSizeLowerBound = 0x4000;

        public const int MaxFrameSizeUpperBound = 0xffffff;

        public const long MaxHeaderListSize = MaxUnsignedInt;

        public const long MinHeaderTableSize = 0;

        public const long MinConcurrentStreams = 0;

        public const int MinInitialWindowSize = 0;

        public const long MinHeaderListSize = 0;

        public const int DefaultWindowSize = 65535;

        public const short DefaultPriorityWeight = 16;

        public const int DefaultHeaderTableSize = 4096;

        /// <summary>
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">The initial value of this setting is unlimited</a>.
        /// However in practice we don't want to allow our peers to use unlimited memory by default. So we take advantage
        /// of the <c>For any given request, a lower limit than what is advertised MAY be enforced.</c> loophole.
        /// </summary>
        public const long DefaultHeaderListSize = 8192;

        public const int DefaultMaxFrameSize = MaxFrameSizeLowerBound;

        /// <summary>
        /// The assumed minimum value for {@code SETTINGS_MAX_CONCURRENT_STREAMS} as
        /// recommended by the <a herf="https://tools.ietf.org/html/rfc7540#section-6.5.2">HTTP/2 spec</a>.
        /// </summary>
        public const int SmallestMaxConcurrentStreams = 100;

        public const int DefaultMaxReservedStreams = SmallestMaxConcurrentStreams;

        public const int DefaultMinAllocationChunk = 1024;

        /// <summary>
        /// Calculate the threshold in bytes which should trigger a <c>GO_AWAY</c> if a set of headers exceeds this amount.
        /// </summary>
        /// <param name="maxHeaderListSize"><a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>
        /// for the local endpoint.</param>
        /// <returns>the threshold in bytes which should trigger a <c>GO_AWAY</c> if a set of headers exceeds this amount.</returns>
        public static long CalculateMaxHeaderListSizeGoAway(long maxHeaderListSize)
        {
            // This is equivalent to `maxHeaderListSize * 1.25` but we avoid floating point multiplication.
            return maxHeaderListSize + (maxHeaderListSize.RightUShift(2));
        }

        public static readonly TimeSpan DefaultGracefulShutdownTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Returns <c>true</c> if the stream is an outbound stream.
        /// </summary>
        /// <param name="server"><c>true</c> if the endpoint is a server, <c>false</c> otherwise.</param>
        /// <param name="streamId">the stream identifier</param>
        /// <returns></returns>
        public static bool IsOutboundStream(bool server, int streamId)
        {
            bool even = 0u >= (uint)(streamId & 1);
            return streamId > 0 && server == even;
        }

        /// <summary>
        /// Returns true if the <paramref name="streamId"/> is a valid HTTP/2 stream identifier.
        /// </summary>
        /// <param name="streamId"></param>
        /// <returns></returns>
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static bool IsStreamIdValid(int streamId)
        {
            return streamId >= 0;
        }

        internal static bool IsStreamIdValid(int streamId, bool server)
        {
            return IsStreamIdValid(streamId) && server == (0u >= (uint)(streamId & 1));
        }

        /// <summary>
        /// Indicates whether or not the given value for max frame size falls within the valid range.
        /// </summary>
        /// <param name="maxFrameSize"></param>
        /// <returns></returns>
        public static bool IsMaxFrameSizeValid(long maxFrameSize)
        {
            return maxFrameSize >= MaxFrameSizeLowerBound && maxFrameSize <= MaxFrameSizeUpperBound;
        }

        /// <summary>
        /// Returns a buffer containing the <see cref="ConnectionPreface"/>.
        /// </summary>
        /// <returns></returns>
        public static IByteBuffer ConnectionPrefaceBuf()
        {
            // Return a duplicate so that modifications to the reader index will not affect the original buffer.
            return ConnectionPreface.RetainedDuplicate();
        }

        /// <summary>
        /// Iteratively looks through the causality chain for the given exception and returns the first
        /// <see cref="Http2Exception"/> or <c>null</c> if none.
        /// </summary>
        /// <param name="cause"></param>
        /// <returns></returns>
        public static Http2Exception GetEmbeddedHttp2Exception(Exception cause)
        {
            while (cause is object)
            {
                if (cause is Http2Exception http2Exception)
                {
                    return http2Exception;
                }
                cause = cause.InnerException;
            }
            return null;
        }

        /// <summary>
        /// Creates a buffer containing the error message from the given exception. If the cause is
        /// <c>null</c> returns an empty buffer.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="cause"></param>
        /// <returns></returns>
        public static IByteBuffer ToByteBuf(IChannelHandlerContext ctx, Exception cause)
        {
            if (cause is null || cause.Message is null)
            {
                return Unpooled.Empty;
            }

            return ByteBufferUtil.WriteUtf8(ctx.Allocator, cause.Message);
        }

        /// <summary>
        /// Reads a big-endian (31-bit) integer from the buffer.
        /// </summary>
        /// <param name="buf"></param>
        /// <returns></returns>
        public static int ReadUnsignedInt(IByteBuffer buf)
        {
            return buf.ReadInt() & 0x7fffffff;
        }

        /// <summary>
        /// Writes an HTTP/2 frame header to the output buffer.
        /// </summary>
        /// <param name="output"></param>
        /// <param name="payloadLength"></param>
        /// <param name="type"></param>
        /// <param name="flags"></param>
        /// <param name="streamId"></param>
        public static void WriteFrameHeader(IByteBuffer output, int payloadLength, Http2FrameTypes type,
            Http2Flags flags, int streamId)
        {
            output.EnsureWritable(FrameHeaderLength + payloadLength);
            WriteFrameHeaderInternal(output, payloadLength, type, flags, streamId);
        }

        /// <summary>
        /// Calculate the amount of bytes that can be sent by <paramref name="state"/>. The lower bound is <c>0</c>.
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public static int StreamableBytes(IStreamByteDistributorStreamState state)
        {
            return Math.Max(0, (int)Math.Min(state.PendingBytes, state.WindowSize));
        }

        /// <summary>
        /// Return a unreleasable view on the given <see cref="IByteBuffer"/> which will just ignore release and retain calls.
        /// </summary>
        /// <param name="buf"></param>
        /// <returns></returns>
        public static IByteBuffer UnreleasableBuffer(IByteBuffer buf)
        {
            return new UnreleasableByteBuffer(buf);
        }

        /// <summary>
        /// Creates a new big-endian direct buffer with the specified <see cref="IByteBuffer.Capacity"/>, which
        /// expands its capacity boundlessly on demand. The new buffer's <see cref="IByteBuffer.ReaderIndex"/> and
        /// <see cref="IByteBuffer.WriterIndex"/> are <c>0</c>.
        /// </summary>
        /// <param name="initialCapacity"></param>
        /// <returns></returns>
        public static IByteBuffer DirectBuffer(int initialCapacity)
        {
            return Unpooled.DirectBuffer(initialCapacity);
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static void VerifyPadding(int padding)
        {
            if (/*padding < 0 || */(uint)padding > (uint)MaxPadding)
            {
                ThrowHelper.ThrowArgumentException_InvalidPadding(padding);
            }
        }

        /// <summary>
        /// Results in a RST_STREAM being sent for <paramref name="streamId"/> due to violating
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a>.
        /// </summary>
        /// <param name="streamId">The stream ID that was being processed when the exceptional condition occurred.</param>
        /// <param name="maxHeaderListSize">The max allowed size for a list of headers in bytes which was exceeded.</param>
        /// <param name="onDecode"><c>true</c> if the exception was encountered during decoder. <c>false</c> for encode.</param>
        /// <exception cref="Http2Exception">a stream error.</exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void HeaderListSizeExceeded(int streamId, long maxHeaderListSize, bool onDecode)
        {
            throw Http2Exception.HeaderListSizeError(streamId, Http2Error.ProtocolError, onDecode,
                "Header size exceeded max allowed size ({0})", maxHeaderListSize);
        }

        /// <summary>
        /// Results in a GO_AWAY being sent due to violating
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_HEADER_LIST_SIZE</a> in an unrecoverable
        /// manner.
        /// </summary>
        /// <param name="maxHeaderListSize">The max allowed size for a list of headers in bytes which was exceeded.</param>
        /// <exception cref="Http2Exception">a connection error.</exception>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void HeaderListSizeExceeded(long maxHeaderListSize)
        {
            throw Http2Exception.ConnectionError(Http2Error.ProtocolError,
                "Header size exceeded max allowed size ({0})", maxHeaderListSize);
        }

        internal static void WriteFrameHeaderInternal(IByteBuffer output, int payloadLength, Http2FrameTypes type,
            Http2Flags flags, int streamId)
        {
            output.WriteMedium(payloadLength);
            output.WriteByte((int)type);
            output.WriteByte(flags.Value);
            output.WriteInt(streamId);
        }
    }
}