// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// A <see cref="IHttp2FrameReader"/> that supports all frame types defined by the HTTP/2 specification.
    /// </summary>
    public class DefaultHttp2FrameReader : IHttp2FrameReader, IHttp2FrameSizePolicy, IHttp2FrameReaderConfiguration
    {
        readonly IHttp2HeadersDecoder _headersDecoder;

        /// <summary>
        /// <c>true</c> = reading headers, <c>false</c> = reading payload.
        /// </summary>
        bool _readingHeaders = true;
        /// <summary>
        /// Once set to <c>true</c> the value will never change. This is set to <c>true</c> if an unrecoverable error which
        /// renders the connection unusable.
        /// </summary>
        bool _readError;
        Http2FrameTypes _frameType;
        int _streamId;
        Http2Flags _flags;
        int _payloadLength;
        HeadersContinuation _headersContinuation;
        int _maxFrameSize;

        /// <summary>
        /// Create a new instance. Header names will be validated.
        /// </summary>
        public DefaultHttp2FrameReader()
            : this(true)
        {
        }

        /// <summary>
        /// Create a new instance.
        /// </summary>
        /// <param name="validateHeaders"><c>true</c> to validate headers. <c>false</c> to not validate headers.</param>
        public DefaultHttp2FrameReader(bool validateHeaders)
            : this(new DefaultHttp2HeadersDecoder(validateHeaders))
        {
        }

        public DefaultHttp2FrameReader(IHttp2HeadersDecoder headersDecoder)
        {
            _headersDecoder = headersDecoder;
            _maxFrameSize = Http2CodecUtil.DefaultMaxFrameSize;
        }

        public IHttp2HeadersDecoderConfiguration HeadersConfiguration => _headersDecoder.Configuration;

        public IHttp2FrameReaderConfiguration Configuration => this;

        public IHttp2FrameSizePolicy FrameSizePolicy => this;

        public void SetMaxFrameSize(int max)
        {
            if (!Http2CodecUtil.IsMaxFrameSizeValid(max))
            {
                ThrowHelper.ThrowStreamError_InvalidMaxFrameSizeSpecifiedInSentSettings(_streamId, max);
            }

            _maxFrameSize = max;
        }

        public int MaxFrameSize => _maxFrameSize;

        public void Dispose() => Close();

        protected virtual void Dispose(bool disposing)
        {
        }

        public virtual void Close()
        {
            CloseHeadersContinuation();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void CloseHeadersContinuation()
        {
            if (_headersContinuation is object)
            {
                _headersContinuation.Close();
                _headersContinuation = null;
            }
        }

        public void ReadFrame(IChannelHandlerContext ctx, IByteBuffer input, IHttp2FrameListener listener)
        {
            if (_readError)
            {
                _ = input.SkipBytes(input.ReadableBytes);
                return;
            }

            try
            {
                do
                {
                    if (_readingHeaders)
                    {
                        ProcessHeaderState(input);
                        if (_readingHeaders)
                        {
                            // Wait until the entire header has arrived.
                            return;
                        }
                    }

                    // The header is complete, fall into the next case to process the payload.
                    // This is to ensure the proper handling of zero-length payloads. In this
                    // case, we don't want to loop around because there may be no more data
                    // available, causing us to exit the loop. Instead, we just want to perform
                    // the first pass at payload processing now.
                    ProcessPayloadState(ctx, input, listener);
                    if (!_readingHeaders)
                    {
                        // Wait until the entire payload has arrived.
                        return;
                    }
                }
                while (input.IsReadable());
            }
            catch (Http2Exception e)
            {
                _readError = !Http2Exception.IsStreamError(e);
                throw;
            }
            catch (Http2RuntimeException)
            {
                _readError = true;
                throw;
            }
            catch (Exception)
            {
                _readError = true;
                throw;
            }
        }

        void ProcessHeaderState(IByteBuffer input)
        {
            if (input.ReadableBytes < Http2CodecUtil.FrameHeaderLength)
            {
                // Wait until the entire frame header has been read.
                return;
            }

            // Read the header and prepare the unmarshaller to read the frame.
            _payloadLength = input.ReadUnsignedMedium();
            if (_payloadLength > _maxFrameSize)
            {
                ThrowHelper.ThrowConnectionError_FrameLengthExceedsMaximum(_payloadLength, _maxFrameSize);
            }

            _frameType = (Http2FrameTypes)input.ReadByte();
            _flags = new Http2Flags(input.ReadByte());
            _streamId = Http2CodecUtil.ReadUnsignedInt(input);

            // We have consumed the data, next time we read we will be expecting to read the frame payload.
            _readingHeaders = false;

            switch (_frameType)
            {
                case Http2FrameTypes.Data:
                    VerifyDataFrame();
                    break;
                case Http2FrameTypes.Headers:
                    VerifyHeadersFrame();
                    break;
                case Http2FrameTypes.Priority:
                    VerifyPriorityFrame();
                    break;
                case Http2FrameTypes.RstStream:
                    VerifyRstStreamFrame();
                    break;
                case Http2FrameTypes.Settings:
                    VerifySettingsFrame();
                    break;
                case Http2FrameTypes.PushPromise:
                    VerifyPushPromiseFrame();
                    break;
                case Http2FrameTypes.Ping:
                    VerifyPingFrame();
                    break;
                case Http2FrameTypes.GoAway:
                    VerifyGoAwayFrame();
                    break;
                case Http2FrameTypes.WindowUpdate:
                    VerifyWindowUpdateFrame();
                    break;
                case Http2FrameTypes.Continuation:
                    VerifyContinuationFrame();
                    break;
                default:
                    // Unknown frame type, could be an extension.
                    VerifyUnknownFrame();
                    break;
            }
        }

        void ProcessPayloadState(IChannelHandlerContext ctx, IByteBuffer input, IHttp2FrameListener listener)
        {
            if (input.ReadableBytes < _payloadLength)
            {
                // Wait until the entire payload has been read.
                return;
            }

            // Only process up to payloadLength bytes.
            int payloadEndIndex = input.ReaderIndex + _payloadLength;

            // We have consumed the data, next time we read we will be expecting to read a frame header.
            _readingHeaders = true;

            // Read the payload and fire the frame event to the listener.
            switch (_frameType)
            {
                case Http2FrameTypes.Data:
                    ReadDataFrame(ctx, input, payloadEndIndex, listener);
                    break;
                case Http2FrameTypes.Headers:
                    ReadHeadersFrame(ctx, input, payloadEndIndex, listener);
                    break;
                case Http2FrameTypes.Priority:
                    ReadPriorityFrame(ctx, input, listener);
                    break;
                case Http2FrameTypes.RstStream:
                    ReadRstStreamFrame(ctx, input, listener);
                    break;
                case Http2FrameTypes.Settings:
                    ReadSettingsFrame(ctx, input, listener);
                    break;
                case Http2FrameTypes.PushPromise:
                    ReadPushPromiseFrame(ctx, input, payloadEndIndex, listener);
                    break;
                case Http2FrameTypes.Ping:
                    ReadPingFrame(ctx, input.ReadLong(), listener);
                    break;
                case Http2FrameTypes.GoAway:
                    ReadGoAwayFrame(ctx, input, payloadEndIndex, listener);
                    break;
                case Http2FrameTypes.WindowUpdate:
                    ReadWindowUpdateFrame(ctx, input, listener);
                    break;
                case Http2FrameTypes.Continuation:
                    ReadContinuationFrame(input, payloadEndIndex, listener);
                    break;
                default:
                    ReadUnknownFrame(ctx, input, payloadEndIndex, listener);
                    break;
            }
            _ = input.SetReaderIndex(payloadEndIndex);
        }

        void VerifyDataFrame()
        {
            VerifyAssociatedWithAStream();
            VerifyNotProcessingHeaders();
            VerifyPayloadLength(_payloadLength);

            if (_payloadLength < _flags.GetPaddingPresenceFieldLength())
            {
                ThrowHelper.ThrowStreamError_FrameLengthTooSmall(_streamId, _payloadLength);
            }
        }

        void VerifyHeadersFrame()
        {
            VerifyAssociatedWithAStream();
            VerifyNotProcessingHeaders();
            VerifyPayloadLength(_payloadLength);

            int requiredLength = _flags.GetPaddingPresenceFieldLength() + _flags.GetNumPriorityBytes();
            if (_payloadLength < requiredLength)
            {
                ThrowHelper.ThrowStreamError_FrameLengthTooSmall(_streamId, _payloadLength);
            }
        }

        void VerifyPriorityFrame()
        {
            VerifyAssociatedWithAStream();
            VerifyNotProcessingHeaders();

            if (_payloadLength != Http2CodecUtil.PriorityEntryLength)
            {
                ThrowHelper.ThrowStreamError_InvalidFrameLength(_streamId, _payloadLength);
            }
        }

        void VerifyRstStreamFrame()
        {
            VerifyAssociatedWithAStream();
            VerifyNotProcessingHeaders();

            if (_payloadLength != Http2CodecUtil.IntFieldLength)
            {
                ThrowHelper.ThrowConnectionError_InvalidFrameLength(_payloadLength);
            }
        }

        void VerifySettingsFrame()
        {
            VerifyNotProcessingHeaders();
            VerifyPayloadLength(_payloadLength);
            if (_streamId != 0)
            {
                ThrowHelper.ThrowConnectionError_AStreamIDMustBeZero();
            }

            if (_flags.Ack() && _payloadLength > 0)
            {
                ThrowHelper.ThrowConnectionError_AckSettingsFrameMustHaveAnEmptyPayload();
            }

            if (_payloadLength % Http2CodecUtil.SettingEntryLength > 0)
            {
                ThrowHelper.ThrowConnectionError_InvalidFrameLength(_payloadLength);
            }
        }

        void VerifyPushPromiseFrame()
        {
            VerifyNotProcessingHeaders();
            VerifyPayloadLength(_payloadLength);

            // Subtract the length of the promised stream ID field, to determine the length of the
            // rest of the payload (header block fragment + payload).
            int minLength = _flags.GetPaddingPresenceFieldLength() + Http2CodecUtil.IntFieldLength;
            if (_payloadLength < minLength)
            {
                ThrowHelper.ThrowStreamError_FrameLengthTooSmall(_streamId, _payloadLength);
            }
        }

        void VerifyPingFrame()
        {
            VerifyNotProcessingHeaders();
            if (_streamId != 0)
            {
                ThrowHelper.ThrowConnectionError_AStreamIDMustBeZero();
            }

            if (_payloadLength != Http2CodecUtil.PingFramePayloadLength)
            {
                ThrowHelper.ThrowConnectionError_FrameLengthIncorrectSizeForPing(_payloadLength);
            }
        }

        void VerifyGoAwayFrame()
        {
            VerifyNotProcessingHeaders();
            VerifyPayloadLength(_payloadLength);

            if (_streamId != 0)
            {
                ThrowHelper.ThrowConnectionError_AStreamIDMustBeZero();
            }

            if (_payloadLength < 8)
            {
                ThrowHelper.ThrowConnectionError_FrameLengthTooSmall(_payloadLength);
            }
        }

        void VerifyWindowUpdateFrame()
        {
            VerifyNotProcessingHeaders();
            if (_streamId < 0) { ThrowHelper.ThrowConnectionError_StreamIdPositiveOrZero(); }

            if (_payloadLength != Http2CodecUtil.IntFieldLength)
            {
                ThrowHelper.ThrowConnectionError_InvalidFrameLength(_payloadLength);
            }
        }

        void VerifyContinuationFrame()
        {
            VerifyAssociatedWithAStream();
            VerifyPayloadLength(_payloadLength);

            if (_headersContinuation is null)
            {
                ThrowHelper.ThrowConnectionError_ReceivedFrameButNotCurrentlyProcessingHeaders(_frameType);
            }

            var expectedStreamId = _headersContinuation.GetStreamId();
            if (_streamId != expectedStreamId)
            {
                ThrowHelper.ThrowConnectionError_ContinuationStreamIDDoesNotMatchPendingHeaders(expectedStreamId, _streamId);
            }

            if (_payloadLength < _flags.GetPaddingPresenceFieldLength())
            {
                ThrowHelper.ThrowStreamError_FrameLengthTooSmallForPadding(_streamId, _payloadLength);
            }
        }

        void VerifyUnknownFrame()
        {
            VerifyNotProcessingHeaders();
        }

        void ReadDataFrame(IChannelHandlerContext ctx, IByteBuffer payload, int payloadEndIndex, IHttp2FrameListener listener)
        {
            int padding = ReadPadding(payload);
            VerifyPadding(padding);

            // Determine how much data there is to read by removing the trailing
            // padding.
            int dataLength = LengthWithoutTrailingPadding(payloadEndIndex - payload.ReaderIndex, padding);

            IByteBuffer data = payload.ReadSlice(dataLength);
            _ = listener.OnDataRead(ctx, _streamId, data, padding, _flags.EndOfStream());
        }

        sealed class PriorityHeadersFrameHeadersContinuation : HeadersContinuation
        {
            readonly int _streamDependency;
            readonly short _weight;
            readonly bool _exclusive;
            readonly Http2Flags _headersFlags;

            public PriorityHeadersFrameHeadersContinuation(DefaultHttp2FrameReader reader,
                IChannelHandlerContext ctx, int streamId, int padding, int streamDependency,
                short weight, bool exclusive, Http2Flags headersFlags)
                : base(reader, ctx, streamId, padding)
            {
                _streamDependency = streamDependency;
                _weight = weight;
                _exclusive = exclusive;
                _headersFlags = headersFlags;
            }

            public override void ProcessFragment(bool endOfHeaders, IByteBuffer fragment, int len, IHttp2FrameListener listener)
            {
                _builder.AddFragment(fragment, len, _ctx.Allocator, endOfHeaders);
                if (endOfHeaders)
                {
                    listener.OnHeadersRead(_ctx, _streamId, _builder.Headers(), _streamDependency,
                        _weight, _exclusive, _padding, _headersFlags.EndOfStream());
                }
            }
        }

        sealed class HeadersFrameHeadersContinuation : HeadersContinuation
        {
            readonly Http2Flags _headersFlags;

            public HeadersFrameHeadersContinuation(DefaultHttp2FrameReader reader,
                IChannelHandlerContext ctx, int streamId, int padding, Http2Flags headersFlags)
                : base(reader, ctx, streamId, padding)
            {
                _headersFlags = headersFlags;
            }

            public override void ProcessFragment(bool endOfHeaders, IByteBuffer fragment, int len, IHttp2FrameListener listener)
            {
                _builder.AddFragment(fragment, len, _ctx.Allocator, endOfHeaders);
                if (endOfHeaders)
                {
                    listener.OnHeadersRead(_ctx, _streamId, _builder.Headers(), _padding, _headersFlags.EndOfStream());
                }
            }
        }

        void ReadHeadersFrame(IChannelHandlerContext ctx, IByteBuffer payload, int payloadEndIndex, IHttp2FrameListener listener)
        {
            int headersStreamId = _streamId;
            Http2Flags headersFlags = _flags;
            int padding = ReadPadding(payload);
            VerifyPadding(padding);

            // The callback that is invoked is different depending on whether priority information
            // is present in the headers frame.
            if (headersFlags.PriorityPresent())
            {
                long word1 = payload.ReadUnsignedInt();
                bool exclusive = (word1 & 0x80000000L) != 0;
                int streamDependency = (int)(word1 & 0x7FFFFFFFL);
                if (streamDependency == headersStreamId)
                {
                    ThrowHelper.ThrowStreamError_AStreamCannotDependOnItself(headersStreamId);
                }

                short weight = (short)(payload.ReadByte() + 1);
                int lenToRead = LengthWithoutTrailingPadding(payloadEndIndex - payload.ReaderIndex, padding);

                // Create a handler that invokes the listener when the header block is complete.
                _headersContinuation = new PriorityHeadersFrameHeadersContinuation(this,
                    ctx, headersStreamId, padding, streamDependency, weight, exclusive, headersFlags);

                // Process the initial fragment, invoking the listener's callback if end of headers.
                _headersContinuation.ProcessFragment(headersFlags.EndOfHeaders(), payload, lenToRead, listener);
                ResetHeadersContinuationIfEnd(headersFlags.EndOfHeaders());
                return;
            }

            // The priority fields are not present in the frame. Prepare a continuation that invokes
            // the listener callback without priority information.
            _headersContinuation = new HeadersFrameHeadersContinuation(this,
                ctx, headersStreamId, padding, headersFlags);

            // Process the initial fragment, invoking the listener's callback if end of headers.
            int dataLength = LengthWithoutTrailingPadding(payloadEndIndex - payload.ReaderIndex, padding);
            _headersContinuation.ProcessFragment(headersFlags.EndOfHeaders(), payload, dataLength, listener);
            ResetHeadersContinuationIfEnd(headersFlags.EndOfHeaders());
        }

        void ResetHeadersContinuationIfEnd(bool endOfHeaders)
        {
            if (endOfHeaders)
            {
                CloseHeadersContinuation();
            }
        }

        void ReadPriorityFrame(IChannelHandlerContext ctx, IByteBuffer payload, IHttp2FrameListener listener)
        {
            long word1 = payload.ReadUnsignedInt();
            bool exclusive = (word1 & 0x80000000L) != 0;
            int streamDependency = (int)(word1 & 0x7FFFFFFFL);
            if (streamDependency == _streamId)
            {
                ThrowHelper.ThrowStreamError_AStreamCannotDependOnItself(_streamId);
            }

            short weight = (short)(payload.ReadByte() + 1);
            listener.OnPriorityRead(ctx, _streamId, streamDependency, weight, exclusive);
        }

        void ReadRstStreamFrame(IChannelHandlerContext ctx, IByteBuffer payload, IHttp2FrameListener listener)
        {
            long errorCode = payload.ReadUnsignedInt();
            listener.OnRstStreamRead(ctx, _streamId, (Http2Error)errorCode);
        }

        void ReadSettingsFrame(IChannelHandlerContext ctx, IByteBuffer payload, IHttp2FrameListener listener)
        {
            if (_flags.Ack())
            {
                listener.OnSettingsAckRead(ctx);
            }
            else
            {
                int numSettings = _payloadLength / Http2CodecUtil.SettingEntryLength;
                Http2Settings settings = new Http2Settings();
                for (int index = 0; index < numSettings; ++index)
                {
                    char id = (char)payload.ReadUnsignedShort();
                    long value = payload.ReadUnsignedInt();
                    try
                    {
                        _ = settings.Put(id, value);
                    }
                    catch (ArgumentException e)
                    {
                        switch (id)
                        {
                            case Http2CodecUtil.SettingsMaxFrameSize:
                                ThrowHelper.ThrowConnectionError(Http2Error.ProtocolError, e);
                                break;
                            case Http2CodecUtil.SettingsInitialWindowSize:
                                ThrowHelper.ThrowConnectionError(Http2Error.FlowControlError, e);
                                break;
                            default:
                                ThrowHelper.ThrowConnectionError(Http2Error.ProtocolError, e);
                                break;
                        }
                    }
                }

                listener.OnSettingsRead(ctx, settings);
            }
        }

        sealed class PushPromiseFrameHeadersContinuation : HeadersContinuation
        {
            readonly int _promisedStreamId;

            public PushPromiseFrameHeadersContinuation(DefaultHttp2FrameReader reader,
                IChannelHandlerContext ctx, int streamId, int padding, int promisedStreamId)
                : base(reader, ctx, streamId, padding)
            {
                _promisedStreamId = promisedStreamId;
            }

            public override void ProcessFragment(bool endOfHeaders, IByteBuffer fragment, int len, IHttp2FrameListener listener)
            {
                _builder.AddFragment(fragment, len, _ctx.Allocator, endOfHeaders);
                if (endOfHeaders)
                {
                    listener.OnPushPromiseRead(_ctx, _streamId, _promisedStreamId, _builder.Headers(), _padding);
                }
            }
        }

        void ReadPushPromiseFrame(IChannelHandlerContext ctx, IByteBuffer payload, int payloadEndIndex, IHttp2FrameListener listener)
        {
            int pushPromiseStreamId = _streamId;
            int padding = ReadPadding(payload);
            VerifyPadding(padding);
            int promisedStreamId = Http2CodecUtil.ReadUnsignedInt(payload);

            // Create a handler that invokes the listener when the header block is complete.
            _headersContinuation = new PushPromiseFrameHeadersContinuation(this,
                ctx, pushPromiseStreamId, padding, promisedStreamId);

            // Process the initial fragment, invoking the listener's callback if end of headers.
            int dataLength = LengthWithoutTrailingPadding(payloadEndIndex - payload.ReaderIndex, padding);

            _headersContinuation.ProcessFragment(_flags.EndOfHeaders(), payload, dataLength, listener);
            ResetHeadersContinuationIfEnd(_flags.EndOfHeaders());
        }

        void ReadPingFrame(IChannelHandlerContext ctx, long data, IHttp2FrameListener listener)
        {
            if (_flags.Ack())
            {
                listener.OnPingAckRead(ctx, data);
            }
            else
            {
                listener.OnPingRead(ctx, data);
            }
        }

        static void ReadGoAwayFrame(IChannelHandlerContext ctx, IByteBuffer payload, int payloadEndIndex, IHttp2FrameListener listener)
        {
            int lastStreamId = Http2CodecUtil.ReadUnsignedInt(payload);
            var errorCode = (Http2Error)payload.ReadUnsignedInt();
            IByteBuffer debugData = payload.ReadSlice(payloadEndIndex - payload.ReaderIndex);
            listener.OnGoAwayRead(ctx, lastStreamId, errorCode, debugData);
        }

        void ReadWindowUpdateFrame(IChannelHandlerContext ctx, IByteBuffer payload, IHttp2FrameListener listener)
        {
            int windowSizeIncrement = Http2CodecUtil.ReadUnsignedInt(payload);
            if (0u >= (uint)windowSizeIncrement)
            {
                ThrowHelper.ThrowStreamError_ReceivedWindowUpdateWithDelta0ForStream(_streamId);
            }

            listener.OnWindowUpdateRead(ctx, _streamId, windowSizeIncrement);
        }

        void ReadContinuationFrame(IByteBuffer payload, int payloadEndIndex, IHttp2FrameListener listener)
        {
            // Process the initial fragment, invoking the listener's callback if end of headers.
            _headersContinuation.ProcessFragment(_flags.EndOfHeaders(), payload, payloadEndIndex - payload.ReaderIndex, listener);
            ResetHeadersContinuationIfEnd(_flags.EndOfHeaders());
        }

        void ReadUnknownFrame(IChannelHandlerContext ctx, IByteBuffer payload, int payloadEndIndex, IHttp2FrameListener listener)
        {
            payload = payload.ReadSlice(payloadEndIndex - payload.ReaderIndex);
            listener.OnUnknownFrame(ctx, _frameType, _streamId, _flags, payload);
        }

        /// <summary>
        /// If padding is present in the payload, reads the next byte as padding. The padding also includes the one byte
        /// width of the pad length field. Otherwise, returns zero.
        /// </summary>
        /// <param name="payload"></param>
        /// <returns></returns>
        int ReadPadding(IByteBuffer payload)
        {
            if (!_flags.PaddingPresent()) { return 0; }

            return payload.ReadByte() + 1;
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        void VerifyPadding(int padding)
        {
            int len = LengthWithoutTrailingPadding(_payloadLength, padding);
            if ((uint)len > SharedConstants.TooBigOrNegative) // < 0
            {
                ThrowHelper.ThrowConnectionError_FramePayloadTooSmallForPadding();
            }
        }

        /// <summary>
        /// The padding parameter consists of the 1 byte pad length field and the trailing padding bytes.
        /// </summary>
        /// <param name="readableBytes"></param>
        /// <param name="padding"></param>
        /// <returns>the number of readable bytes without the trailing padding.</returns>
        [MethodImpl(InlineMethod.AggressiveInlining)]
        static int LengthWithoutTrailingPadding(int readableBytes, int padding)
        {
            return 0u >= (uint)padding ? readableBytes : readableBytes - (padding - 1);
        }

        /// <summary>
        /// Base class for processing of HEADERS and PUSH_PROMISE header blocks that potentially span
        /// multiple frames. The implementation of this interface will perform the final callback to the
        /// <see cref="IHttp2FrameListener"/> once the end of headers is reached.
        /// </summary>
        abstract class HeadersContinuation
        {
            protected readonly IChannelHandlerContext _ctx;
            protected readonly int _streamId;
            protected readonly HeadersBlockBuilder _builder;
            protected int _padding;

            public HeadersContinuation(DefaultHttp2FrameReader reader, IChannelHandlerContext ctx, int streamId, int padding)
            {
                _builder = new HeadersBlockBuilder(reader);
                _ctx = ctx;
                _streamId = streamId;
                _padding = padding;
            }

            /// <summary>
            /// Returns the stream for which headers are currently being processed.
            /// </summary>
            internal int GetStreamId() => _streamId;

            /// <summary>
            /// Processes the next fragment for the current header block.
            /// </summary>
            /// <param name="endOfHeaders">whether the fragment is the last in the header block.</param>
            /// <param name="fragment">the fragment of the header block to be added.</param>
            /// <param name="len"></param>
            /// <param name="listener">the listener to be notified if the header block is completed.</param>
            public abstract void ProcessFragment(bool endOfHeaders, IByteBuffer fragment, int len, IHttp2FrameListener listener);

            /// <summary>
            /// Free any allocated resources.
            /// </summary>
            internal void Close()
            {
                _builder.Close();
            }
        }

        /// <summary>
        /// Utility class to help with construction of the headers block that may potentially span
        /// multiple frames.
        /// </summary>
        sealed class HeadersBlockBuilder
        {
            readonly DefaultHttp2FrameReader _reader;
            IByteBuffer _headerBlock;

            public HeadersBlockBuilder(DefaultHttp2FrameReader reader)
            {
                _reader = reader;
            }

            /// <summary>
            /// The local header size maximum has been exceeded while accumulating bytes.
            /// </summary>
            /// <exception cref="Http2Exception">A connection error indicating too much data has been received.</exception>
            void HeaderSizeExceeded()
            {
                Close();
                Http2CodecUtil.HeaderListSizeExceeded(_reader._headersDecoder.Configuration.MaxHeaderListSizeGoAway);
            }

            /// <summary>
            /// Adds a fragment to the block.
            /// </summary>
            /// <param name="fragment">the fragment of the headers block to be added.</param>
            /// <param name="len"></param>
            /// <param name="alloc">allocator for new blocks if needed.</param>
            /// <param name="endOfHeaders">flag indicating whether the current frame is the end of the headers.
            /// This is used for an optimization for when the first fragment is the full
            /// block. In that case, the buffer is used directly without copying.</param>
            internal void AddFragment(IByteBuffer fragment, int len, IByteBufferAllocator alloc, bool endOfHeaders)
            {
                if (_headerBlock is null)
                {
                    if (len > _reader._headersDecoder.Configuration.MaxHeaderListSizeGoAway)
                    {
                        HeaderSizeExceeded();
                    }

                    if (endOfHeaders)
                    {
                        // Optimization - don't bother copying, just use the buffer as-is. Need
                        // to retain since we release when the header block is built.
                        _headerBlock = fragment.ReadRetainedSlice(len);
                    }
                    else
                    {
                        _headerBlock = alloc.Buffer(len).WriteBytes(fragment, len);
                    }
                    return;
                }

                if (_reader._headersDecoder.Configuration.MaxHeaderListSizeGoAway - len < _headerBlock.ReadableBytes)
                {
                    HeaderSizeExceeded();
                }

                if (_headerBlock.IsWritable(len))
                {
                    // The buffer can hold the requested bytes, just write it directly.
                    _ = _headerBlock.WriteBytes(fragment, len);
                }
                else
                {
                    // Allocate a new buffer that is big enough to hold the entire header block so far.
                    IByteBuffer buf = alloc.Buffer(_headerBlock.ReadableBytes + len);
                    _ = buf.WriteBytes(_headerBlock).WriteBytes(fragment, len);
                    _ = _headerBlock.Release();
                    _headerBlock = buf;
                }
            }

            /// <summary>
            /// Builds the headers from the completed headers block. After this is called, this builder
            /// should not be called again.
            /// </summary>
            internal IHttp2Headers Headers()
            {
                try
                {
                    return _reader._headersDecoder.DecodeHeaders(_reader._streamId, _headerBlock);
                }
                finally
                {
                    Close();
                }
            }

            /// <summary>
            /// Closes this builder and frees any resources.
            /// </summary>
            internal void Close()
            {
                if (_headerBlock is object)
                {
                    _ = _headerBlock.Release();
                    _headerBlock = null;
                }

                // Clear the member variable pointing at this instance.
                _reader._headersContinuation = null;
            }
        }

        /// <summary>
        /// Verify that current state is not processing on header block
        /// </summary>
        /// <exception cref="Http2Exception">if <see cref="_headersContinuation"/> is not null</exception>
        [MethodImpl(InlineMethod.AggressiveInlining)]
        void VerifyNotProcessingHeaders()
        {
            if (_headersContinuation is object)
            {
                ThrowHelper.ThrowConnectionError_ReceivedFrameTypeWhileProcessingHeadersOnStream(
                    _frameType, _headersContinuation.GetStreamId());
            }
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        void VerifyPayloadLength(int payloadLength)
        {
            if ((uint)payloadLength > (uint)_maxFrameSize)
            {
                ThrowHelper.ThrowConnectionError_TotalPayloadLengthExceedsMaxFrameLength(payloadLength);
            }
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        void VerifyAssociatedWithAStream()
        {
            if (0u >= (uint)_streamId)
            {
                ThrowHelper.ThrowConnectionError_FrameTypeMustBeAssociatedWithAStream(_frameType);
            }
        }
    }
}