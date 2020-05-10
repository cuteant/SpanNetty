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
        readonly IHttp2HeadersDecoder headersDecoder;

        /// <summary>
        /// <c>true</c> = reading headers, <c>false</c> = reading payload.
        /// </summary>
        bool readingHeaders = true;
        /// <summary>
        /// Once set to <c>true</c> the value will never change. This is set to <c>true</c> if an unrecoverable error which
        /// renders the connection unusable.
        /// </summary>
        bool readError;
        Http2FrameTypes frameType;
        int streamId;
        Http2Flags flags;
        int payloadLength;
        HeadersContinuation headersContinuation;
        int maxFrameSize;

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
            this.headersDecoder = headersDecoder;
            this.maxFrameSize = Http2CodecUtil.DefaultMaxFrameSize;
        }

        public IHttp2HeadersDecoderConfiguration HeadersConfiguration => this.headersDecoder.Configuration;

        public IHttp2FrameReaderConfiguration Configuration => this;

        public IHttp2FrameSizePolicy FrameSizePolicy => this;

        public void SetMaxFrameSize(int max)
        {
            if (!Http2CodecUtil.IsMaxFrameSizeValid(max))
            {
                ThrowHelper.ThrowStreamError_InvalidMaxFrameSizeSpecifiedInSentSettings(this.streamId, max);
            }

            this.maxFrameSize = max;
        }

        public int MaxFrameSize => this.maxFrameSize;

        public void Dispose() => this.Close();

        protected virtual void Dispose(bool disposing)
        {
        }

        public virtual void Close()
        {
            this.CloseHeadersContinuation();
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        void CloseHeadersContinuation()
        {
            if (this.headersContinuation is object)
            {
                this.headersContinuation.Close();
                this.headersContinuation = null;
            }
        }

        public void ReadFrame(IChannelHandlerContext ctx, IByteBuffer input, IHttp2FrameListener listener)
        {
            if (this.readError)
            {
                input.SkipBytes(input.ReadableBytes);
                return;
            }

            try
            {
                do
                {
                    if (this.readingHeaders)
                    {
                        this.ProcessHeaderState(input);
                        if (this.readingHeaders)
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
                    this.ProcessPayloadState(ctx, input, listener);
                    if (!this.readingHeaders)
                    {
                        // Wait until the entire payload has arrived.
                        return;
                    }
                }
                while (input.IsReadable());
            }
            catch (Http2Exception e)
            {
                this.readError = !Http2Exception.IsStreamError(e);
                throw;
            }
            catch (Http2RuntimeException)
            {
                this.readError = true;
                throw;
            }
            catch (Exception)
            {
                this.readError = true;
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
            this.payloadLength = input.ReadUnsignedMedium();
            if (this.payloadLength > this.maxFrameSize)
            {
                ThrowHelper.ThrowConnectionError_FrameLengthExceedsMaximum(this.payloadLength, this.maxFrameSize);
            }

            this.frameType = (Http2FrameTypes)input.ReadByte();
            this.flags = new Http2Flags(input.ReadByte());
            this.streamId = Http2CodecUtil.ReadUnsignedInt(input);

            // We have consumed the data, next time we read we will be expecting to read the frame payload.
            this.readingHeaders = false;

            switch (this.frameType)
            {
                case Http2FrameTypes.Data:
                    this.VerifyDataFrame();
                    break;
                case Http2FrameTypes.Headers:
                    this.VerifyHeadersFrame();
                    break;
                case Http2FrameTypes.Priority:
                    this.VerifyPriorityFrame();
                    break;
                case Http2FrameTypes.RstStream:
                    this.VerifyRstStreamFrame();
                    break;
                case Http2FrameTypes.Settings:
                    this.VerifySettingsFrame();
                    break;
                case Http2FrameTypes.PushPromise:
                    this.VerifyPushPromiseFrame();
                    break;
                case Http2FrameTypes.Ping:
                    this.VerifyPingFrame();
                    break;
                case Http2FrameTypes.GoAway:
                    this.VerifyGoAwayFrame();
                    break;
                case Http2FrameTypes.WindowUpdate:
                    this.VerifyWindowUpdateFrame();
                    break;
                case Http2FrameTypes.Continuation:
                    this.VerifyContinuationFrame();
                    break;
                default:
                    // Unknown frame type, could be an extension.
                    this.VerifyUnknownFrame();
                    break;
            }
        }

        void ProcessPayloadState(IChannelHandlerContext ctx, IByteBuffer input, IHttp2FrameListener listener)
        {
            if (input.ReadableBytes < this.payloadLength)
            {
                // Wait until the entire payload has been read.
                return;
            }

            // Only process up to payloadLength bytes.
            int payloadEndIndex = input.ReaderIndex + payloadLength;

            // We have consumed the data, next time we read we will be expecting to read a frame header.
            this.readingHeaders = true;

            // Read the payload and fire the frame event to the listener.
            switch (this.frameType)
            {
                case Http2FrameTypes.Data:
                    this.ReadDataFrame(ctx, input, payloadEndIndex, listener);
                    break;
                case Http2FrameTypes.Headers:
                    this.ReadHeadersFrame(ctx, input, payloadEndIndex, listener);
                    break;
                case Http2FrameTypes.Priority:
                    this.ReadPriorityFrame(ctx, input, listener);
                    break;
                case Http2FrameTypes.RstStream:
                    this.ReadRstStreamFrame(ctx, input, listener);
                    break;
                case Http2FrameTypes.Settings:
                    this.ReadSettingsFrame(ctx, input, listener);
                    break;
                case Http2FrameTypes.PushPromise:
                    this.ReadPushPromiseFrame(ctx, input, payloadEndIndex, listener);
                    break;
                case Http2FrameTypes.Ping:
                    this.ReadPingFrame(ctx, input.ReadLong(), listener);
                    break;
                case Http2FrameTypes.GoAway:
                    ReadGoAwayFrame(ctx, input, payloadEndIndex, listener);
                    break;
                case Http2FrameTypes.WindowUpdate:
                    this.ReadWindowUpdateFrame(ctx, input, listener);
                    break;
                case Http2FrameTypes.Continuation:
                    this.ReadContinuationFrame(input, payloadEndIndex, listener);
                    break;
                default:
                    this.ReadUnknownFrame(ctx, input, payloadEndIndex, listener);
                    break;
            }
            input.SetReaderIndex(payloadEndIndex);
        }

        void VerifyDataFrame()
        {
            this.VerifyAssociatedWithAStream();
            this.VerifyNotProcessingHeaders();
            this.VerifyPayloadLength(this.payloadLength);

            if (this.payloadLength < this.flags.GetPaddingPresenceFieldLength())
            {
                ThrowHelper.ThrowStreamError_FrameLengthTooSmall(this.streamId, this.payloadLength);
            }
        }

        void VerifyHeadersFrame()
        {
            this.VerifyAssociatedWithAStream();
            this.VerifyNotProcessingHeaders();
            this.VerifyPayloadLength(this.payloadLength);

            int requiredLength = this.flags.GetPaddingPresenceFieldLength() + this.flags.GetNumPriorityBytes();
            if (this.payloadLength < requiredLength)
            {
                ThrowHelper.ThrowStreamError_FrameLengthTooSmall(this.streamId, this.payloadLength);
            }
        }

        void VerifyPriorityFrame()
        {
            this.VerifyAssociatedWithAStream();
            this.VerifyNotProcessingHeaders();

            if (this.payloadLength != Http2CodecUtil.PriorityEntryLength)
            {
                ThrowHelper.ThrowStreamError_InvalidFrameLength(this.streamId, this.payloadLength);
            }
        }

        void VerifyRstStreamFrame()
        {
            this.VerifyAssociatedWithAStream();
            this.VerifyNotProcessingHeaders();

            if (this.payloadLength != Http2CodecUtil.IntFieldLength)
            {
                ThrowHelper.ThrowConnectionError_InvalidFrameLength(this.payloadLength);
            }
        }

        void VerifySettingsFrame()
        {
            this.VerifyNotProcessingHeaders();
            this.VerifyPayloadLength(this.payloadLength);
            if (this.streamId != 0)
            {
                ThrowHelper.ThrowConnectionError_AStreamIDMustBeZero();
            }

            if (this.flags.Ack() && this.payloadLength > 0)
            {
                ThrowHelper.ThrowConnectionError_AckSettingsFrameMustHaveAnEmptyPayload();
            }

            if (this.payloadLength % Http2CodecUtil.SettingEntryLength > 0)
            {
                ThrowHelper.ThrowConnectionError_InvalidFrameLength(this.payloadLength);
            }
        }

        void VerifyPushPromiseFrame()
        {
            this.VerifyNotProcessingHeaders();
            this.VerifyPayloadLength(this.payloadLength);

            // Subtract the length of the promised stream ID field, to determine the length of the
            // rest of the payload (header block fragment + payload).
            int minLength = this.flags.GetPaddingPresenceFieldLength() + Http2CodecUtil.IntFieldLength;
            if (this.payloadLength < minLength)
            {
                ThrowHelper.ThrowStreamError_FrameLengthTooSmall(this.streamId, this.payloadLength);
            }
        }

        void VerifyPingFrame()
        {
            this.VerifyNotProcessingHeaders();
            if (this.streamId != 0)
            {
                ThrowHelper.ThrowConnectionError_AStreamIDMustBeZero();
            }

            if (this.payloadLength != Http2CodecUtil.PingFramePayloadLength)
            {
                ThrowHelper.ThrowConnectionError_FrameLengthIncorrectSizeForPing(this.payloadLength);
            }
        }

        void VerifyGoAwayFrame()
        {
            this.VerifyNotProcessingHeaders();
            this.VerifyPayloadLength(this.payloadLength);

            if (this.streamId != 0)
            {
                ThrowHelper.ThrowConnectionError_AStreamIDMustBeZero();
            }

            if (this.payloadLength < 8)
            {
                ThrowHelper.ThrowConnectionError_FrameLengthTooSmall(this.payloadLength);
            }
        }

        void VerifyWindowUpdateFrame()
        {
            this.VerifyNotProcessingHeaders();
            if (this.streamId < 0) { ThrowHelper.ThrowConnectionError_StreamIdPositiveOrZero(this.streamId); }

            if (this.payloadLength != Http2CodecUtil.IntFieldLength)
            {
                ThrowHelper.ThrowConnectionError_InvalidFrameLength(this.payloadLength);
            }
        }

        void VerifyContinuationFrame()
        {
            this.VerifyAssociatedWithAStream();
            this.VerifyPayloadLength(this.payloadLength);

            if (this.headersContinuation == null)
            {
                ThrowHelper.ThrowConnectionError_ReceivedFrameButNotCurrentlyProcessingHeaders(this.frameType);
            }

            var expectedStreamId = this.headersContinuation.GetStreamId();
            if (this.streamId != expectedStreamId)
            {
                ThrowHelper.ThrowConnectionError_ContinuationStreamIDDoesNotMatchPendingHeaders(expectedStreamId, this.streamId);
            }

            if (this.payloadLength < this.flags.GetPaddingPresenceFieldLength())
            {
                ThrowHelper.ThrowStreamError_FrameLengthTooSmallForPadding(this.streamId, this.payloadLength);
            }
        }

        void VerifyUnknownFrame()
        {
            this.VerifyNotProcessingHeaders();
        }

        void ReadDataFrame(IChannelHandlerContext ctx, IByteBuffer payload, int payloadEndIndex, IHttp2FrameListener listener)
        {
            int padding = this.ReadPadding(payload);
            this.VerifyPadding(padding);

            // Determine how much data there is to read by removing the trailing
            // padding.
            int dataLength = LengthWithoutTrailingPadding(payloadEndIndex - payload.ReaderIndex, padding);

            IByteBuffer data = payload.ReadSlice(dataLength);
            listener.OnDataRead(ctx, this.streamId, data, padding, this.flags.EndOfStream());
        }

        sealed class PriorityHeadersFrameHeadersContinuation : HeadersContinuation
        {
            readonly int streamDependency;
            readonly short weight;
            readonly bool exclusive;
            readonly Http2Flags headersFlags;

            public PriorityHeadersFrameHeadersContinuation(DefaultHttp2FrameReader reader,
                IChannelHandlerContext ctx, int streamId, int padding, int streamDependency,
                short weight, bool exclusive, Http2Flags headersFlags)
                : base(reader, ctx, streamId, padding)
            {
                this.streamDependency = streamDependency;
                this.weight = weight;
                this.exclusive = exclusive;
                this.headersFlags = headersFlags;
            }

            public override void ProcessFragment(bool endOfHeaders, IByteBuffer fragment, int len, IHttp2FrameListener listener)
            {
                this.builder.AddFragment(fragment, len, this.ctx.Allocator, endOfHeaders);
                if (endOfHeaders)
                {
                    listener.OnHeadersRead(this.ctx, this.streamId, this.builder.Headers(), this.streamDependency,
                        this.weight, this.exclusive, this.padding, this.headersFlags.EndOfStream());
                }
            }
        }

        sealed class HeadersFrameHeadersContinuation : HeadersContinuation
        {
            readonly Http2Flags headersFlags;

            public HeadersFrameHeadersContinuation(DefaultHttp2FrameReader reader,
                IChannelHandlerContext ctx, int streamId, int padding, Http2Flags headersFlags)
                : base(reader, ctx, streamId, padding)
            {
                this.headersFlags = headersFlags;
            }

            public override void ProcessFragment(bool endOfHeaders, IByteBuffer fragment, int len, IHttp2FrameListener listener)
            {
                this.builder.AddFragment(fragment, len, this.ctx.Allocator, endOfHeaders);
                if (endOfHeaders)
                {
                    listener.OnHeadersRead(this.ctx, this.streamId, this.builder.Headers(), this.padding, this.headersFlags.EndOfStream());
                }
            }
        }

        void ReadHeadersFrame(IChannelHandlerContext ctx, IByteBuffer payload, int payloadEndIndex, IHttp2FrameListener listener)
        {
            int headersStreamId = this.streamId;
            Http2Flags headersFlags = this.flags;
            int padding = this.ReadPadding(payload);
            this.VerifyPadding(padding);

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
                this.headersContinuation = new PriorityHeadersFrameHeadersContinuation(this,
                    ctx, headersStreamId, padding, streamDependency, weight, exclusive, headersFlags);

                // Process the initial fragment, invoking the listener's callback if end of headers.
                this.headersContinuation.ProcessFragment(headersFlags.EndOfHeaders(), payload, lenToRead, listener);
                this.ResetHeadersContinuationIfEnd(headersFlags.EndOfHeaders());
                return;
            }

            // The priority fields are not present in the frame. Prepare a continuation that invokes
            // the listener callback without priority information.
            this.headersContinuation = new HeadersFrameHeadersContinuation(this,
                ctx, headersStreamId, padding, headersFlags);

            // Process the initial fragment, invoking the listener's callback if end of headers.
            int dataLength = LengthWithoutTrailingPadding(payloadEndIndex - payload.ReaderIndex, padding);
            this.headersContinuation.ProcessFragment(headersFlags.EndOfHeaders(), payload, dataLength, listener);
            this.ResetHeadersContinuationIfEnd(headersFlags.EndOfHeaders());
        }

        void ResetHeadersContinuationIfEnd(bool endOfHeaders)
        {
            if (endOfHeaders)
            {
                this.CloseHeadersContinuation();
            }
        }

        void ReadPriorityFrame(IChannelHandlerContext ctx, IByteBuffer payload, IHttp2FrameListener listener)
        {
            long word1 = payload.ReadUnsignedInt();
            bool exclusive = (word1 & 0x80000000L) != 0;
            int streamDependency = (int)(word1 & 0x7FFFFFFFL);
            if (streamDependency == this.streamId)
            {
                ThrowHelper.ThrowStreamError_AStreamCannotDependOnItself(this.streamId);
            }

            short weight = (short)(payload.ReadByte() + 1);
            listener.OnPriorityRead(ctx, this.streamId, streamDependency, weight, exclusive);
        }

        void ReadRstStreamFrame(IChannelHandlerContext ctx, IByteBuffer payload, IHttp2FrameListener listener)
        {
            long errorCode = payload.ReadUnsignedInt();
            listener.OnRstStreamRead(ctx, this.streamId, (Http2Error)errorCode);
        }

        void ReadSettingsFrame(IChannelHandlerContext ctx, IByteBuffer payload, IHttp2FrameListener listener)
        {
            if (this.flags.Ack())
            {
                listener.OnSettingsAckRead(ctx);
            }
            else
            {
                int numSettings = this.payloadLength / Http2CodecUtil.SettingEntryLength;
                Http2Settings settings = new Http2Settings();
                for (int index = 0; index < numSettings; ++index)
                {
                    char id = (char)payload.ReadUnsignedShort();
                    long value = payload.ReadUnsignedInt();
                    try
                    {
                        settings.Put(id, value);
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
            readonly int promisedStreamId;

            public PushPromiseFrameHeadersContinuation(DefaultHttp2FrameReader reader,
                IChannelHandlerContext ctx, int streamId, int padding, int promisedStreamId)
                : base(reader, ctx, streamId, padding)
            {
                this.promisedStreamId = promisedStreamId;
            }

            public override void ProcessFragment(bool endOfHeaders, IByteBuffer fragment, int len, IHttp2FrameListener listener)
            {
                this.builder.AddFragment(fragment, len, this.ctx.Allocator, endOfHeaders);
                if (endOfHeaders)
                {
                    listener.OnPushPromiseRead(this.ctx, this.streamId, this.promisedStreamId, this.builder.Headers(), this.padding);
                }
            }
        }

        void ReadPushPromiseFrame(IChannelHandlerContext ctx, IByteBuffer payload, int payloadEndIndex, IHttp2FrameListener listener)
        {
            int pushPromiseStreamId = this.streamId;
            int padding = this.ReadPadding(payload);
            this.VerifyPadding(padding);
            int promisedStreamId = Http2CodecUtil.ReadUnsignedInt(payload);

            // Create a handler that invokes the listener when the header block is complete.
            this.headersContinuation = new PushPromiseFrameHeadersContinuation(this,
                ctx, pushPromiseStreamId, padding, promisedStreamId);

            // Process the initial fragment, invoking the listener's callback if end of headers.
            int dataLength = LengthWithoutTrailingPadding(payloadEndIndex - payload.ReaderIndex, padding);

            this.headersContinuation.ProcessFragment(this.flags.EndOfHeaders(), payload, dataLength, listener);
            this.ResetHeadersContinuationIfEnd(this.flags.EndOfHeaders());
        }

        void ReadPingFrame(IChannelHandlerContext ctx, long data, IHttp2FrameListener listener)
        {
            if (this.flags.Ack())
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
                ThrowHelper.ThrowStreamError_ReceivedWindowUpdateWithDelta0ForStream(this.streamId);
            }

            listener.OnWindowUpdateRead(ctx, this.streamId, windowSizeIncrement);
        }

        void ReadContinuationFrame(IByteBuffer payload, int payloadEndIndex, IHttp2FrameListener listener)
        {
            // Process the initial fragment, invoking the listener's callback if end of headers.
            this.headersContinuation.ProcessFragment(flags.EndOfHeaders(), payload, payloadEndIndex - payload.ReaderIndex, listener);
            this.ResetHeadersContinuationIfEnd(this.flags.EndOfHeaders());
        }

        void ReadUnknownFrame(IChannelHandlerContext ctx, IByteBuffer payload, int payloadEndIndex, IHttp2FrameListener listener)
        {
            payload = payload.ReadSlice(payloadEndIndex - payload.ReaderIndex);
            listener.OnUnknownFrame(ctx, this.frameType, this.streamId, this.flags, payload);
        }

        /// <summary>
        /// If padding is present in the payload, reads the next byte as padding. The padding also includes the one byte
        /// width of the pad length field. Otherwise, returns zero.
        /// </summary>
        /// <param name="payload"></param>
        /// <returns></returns>
        int ReadPadding(IByteBuffer payload)
        {
            if (!this.flags.PaddingPresent()) { return 0; }

            return payload.ReadByte() + 1;
        }

        [MethodImpl(InlineMethod.Value)]
        void VerifyPadding(int padding)
        {
            int len = LengthWithoutTrailingPadding(this.payloadLength, padding);
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
        [MethodImpl(InlineMethod.Value)]
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
            protected readonly IChannelHandlerContext ctx;
            protected readonly int streamId;
            protected readonly HeadersBlockBuilder builder;
            protected int padding;

            public HeadersContinuation(DefaultHttp2FrameReader reader, IChannelHandlerContext ctx, int streamId, int padding)
            {
                this.builder = new HeadersBlockBuilder(reader);
                this.ctx = ctx;
                this.streamId = streamId;
                this.padding = padding;
            }

            /// <summary>
            /// Returns the stream for which headers are currently being processed.
            /// </summary>
            internal int GetStreamId() => this.streamId;

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
                this.builder.Close();
            }
        }

        /// <summary>
        /// Utility class to help with construction of the headers block that may potentially span
        /// multiple frames.
        /// </summary>
        sealed class HeadersBlockBuilder
        {
            readonly DefaultHttp2FrameReader reader;
            IByteBuffer headerBlock;

            public HeadersBlockBuilder(DefaultHttp2FrameReader reader)
            {
                this.reader = reader;
            }

            /// <summary>
            /// The local header size maximum has been exceeded while accumulating bytes.
            /// </summary>
            /// <exception cref="Http2Exception">A connection error indicating too much data has been received.</exception>
            void HeaderSizeExceeded()
            {
                this.Close();
                Http2CodecUtil.HeaderListSizeExceeded(this.reader.headersDecoder.Configuration.MaxHeaderListSizeGoAway);
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
                if (this.headerBlock == null)
                {
                    if (len > this.reader.headersDecoder.Configuration.MaxHeaderListSizeGoAway)
                    {
                        this.HeaderSizeExceeded();
                    }

                    if (endOfHeaders)
                    {
                        // Optimization - don't bother copying, just use the buffer as-is. Need
                        // to retain since we release when the header block is built.
                        this.headerBlock = fragment.ReadRetainedSlice(len);
                    }
                    else
                    {
                        this.headerBlock = alloc.Buffer(len).WriteBytes(fragment, len);
                    }
                    return;
                }

                if (this.reader.headersDecoder.Configuration.MaxHeaderListSizeGoAway - len < this.headerBlock.ReadableBytes)
                {
                    this.HeaderSizeExceeded();
                }

                if (this.headerBlock.IsWritable(len))
                {
                    // The buffer can hold the requested bytes, just write it directly.
                    this.headerBlock.WriteBytes(fragment, len);
                }
                else
                {
                    // Allocate a new buffer that is big enough to hold the entire header block so far.
                    IByteBuffer buf = alloc.Buffer(this.headerBlock.ReadableBytes + len);
                    buf.WriteBytes(this.headerBlock).WriteBytes(fragment, len);
                    this.headerBlock.Release();
                    this.headerBlock = buf;
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
                    return this.reader.headersDecoder.DecodeHeaders(this.reader.streamId, this.headerBlock);
                }
                finally
                {
                    this.Close();
                }
            }

            /// <summary>
            /// Closes this builder and frees any resources.
            /// </summary>
            internal void Close()
            {
                if (this.headerBlock is object)
                {
                    this.headerBlock.Release();
                    this.headerBlock = null;
                }

                // Clear the member variable pointing at this instance.
                this.reader.headersContinuation = null;
            }
        }

        /// <summary>
        /// Verify that current state is not processing on header block
        /// </summary>
        /// <exception cref="Http2Exception">if <see cref="headersContinuation"/> is not null</exception>
        [MethodImpl(InlineMethod.Value)]
        void VerifyNotProcessingHeaders()
        {
            if (this.headersContinuation is object)
            {
                ThrowHelper.ThrowConnectionError_ReceivedFrameTypeWhileProcessingHeadersOnStream(
                    this.frameType, this.headersContinuation.GetStreamId());
            }
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        void VerifyPayloadLength(int payloadLength)
        {
            if ((uint)payloadLength > (uint)this.maxFrameSize)
            {
                ThrowHelper.ThrowConnectionError_TotalPayloadLengthExceedsMaxFrameLength(payloadLength);
            }
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        void VerifyAssociatedWithAStream()
        {
            if (0u >= (uint)this.streamId)
            {
                ThrowHelper.ThrowConnectionError_FrameTypeMustBeAssociatedWithAStream(this.frameType);
            }
        }
    }
}