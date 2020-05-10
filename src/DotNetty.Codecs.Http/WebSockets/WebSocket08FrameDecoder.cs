// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable UseStringInterpolation
namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;

    using static Buffers.ByteBufferUtil;

    public partial class WebSocket08FrameDecoder : ByteToMessageDecoder, IWebSocketFrameDecoder
    {
        enum State
        {
            ReadingFirst,
            ReadingSecond,
            ReadingSize,
            MaskingKey,
            Payload,
            Corrupt
        }

        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<WebSocket08FrameDecoder>();

        const byte OpcodeCont = 0x0;
        const byte OpcodeText = 0x1;
        const byte OpcodeBinary = 0x2;
        const byte OpcodeClose = 0x8;
        const byte OpcodePing = 0x9;
        const byte OpcodePong = 0xA;

        readonly long maxFramePayloadLength;
        readonly bool allowExtensions;
        readonly bool expectMaskedFrames;
        readonly bool allowMaskMismatch;

        int fragmentedFramesCount;
        bool frameFinalFlag;
        bool frameMasked;
        int frameRsv;
        int frameOpcode;
        long framePayloadLength;
        byte[] maskingKey;
        int framePayloadLen1;
        bool receivedClosingHandshake;
        State state = State.ReadingFirst;

        public WebSocket08FrameDecoder(bool expectMaskedFrames, bool allowExtensions, int maxFramePayloadLength)
            : this(expectMaskedFrames, allowExtensions, maxFramePayloadLength, false)
        {
        }

        public WebSocket08FrameDecoder(bool expectMaskedFrames, bool allowExtensions, int maxFramePayloadLength, bool allowMaskMismatch)
        {
            this.expectMaskedFrames = expectMaskedFrames;
            this.allowMaskMismatch = allowMaskMismatch;
            this.allowExtensions = allowExtensions;
            this.maxFramePayloadLength = maxFramePayloadLength;
        }

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            // Discard all data received if closing handshake was received before.
            if (this.receivedClosingHandshake)
            {
                input.SkipBytes(this.ActualReadableBytes);
                return;
            }

            switch (this.state)
            {
                case State.ReadingFirst:
                    if (!input.IsReadable())
                    {
                        return;
                    }

                    this.framePayloadLength = 0;

                    // FIN, RSV, OPCODE
                    byte b = input.ReadByte();
                    this.frameFinalFlag = (b & 0x80) != 0;
                    this.frameRsv = (b & 0x70) >> 4;
                    this.frameOpcode = b & 0x0F;

                    if (Logger.DebugEnabled)
                    {
                        Logger.DecodingWebSocketFrameOpCode(this.frameOpcode);
                    }

                    this.state = State.ReadingSecond;
                    goto case State.ReadingSecond;
                case State.ReadingSecond:
                    if (!input.IsReadable())
                    {
                        return;
                    }

                    // MASK, PAYLOAD LEN 1
                    b = input.ReadByte();
                    this.frameMasked = (b & 0x80) != 0;
                    this.framePayloadLen1 = b & 0x7F;

                    if (this.frameRsv != 0 && !this.allowExtensions)
                    {
                        this.ProtocolViolation_RSVNoExtensionNegotiated(context, this.frameRsv);
                        return;
                    }

                    if (!this.allowMaskMismatch && this.expectMaskedFrames != this.frameMasked)
                    {
                        this.ProtocolViolation_RecAFrameThatIsNotMaskedAsExected(context);
                        return;
                    }

                    // control frame (have MSB in opcode set)
                    if (this.frameOpcode > 7)
                    {
                        // control frames MUST NOT be fragmented
                        if (!this.frameFinalFlag)
                        {
                            this.ProtocolViolation_FragmentedControlFrame(context);
                            return;
                        }

                        // control frames MUST have payload 125 octets or less
                        if (this.framePayloadLen1 > 125)
                        {
                            this.ProtocolViolation_ControlFrameWithPayloadLength125Octets(context);
                            return;
                        }

                        switch (this.frameOpcode)
                        {
                            // close frame : if there is a body, the first two bytes of the
                            // body MUST be a 2-byte unsigned integer (in network byte
                            // order) representing a getStatus code
                            case OpcodeClose:
                                if(this.framePayloadLen1 == 1)
                                {
                                    this.ProtocolViolation_RecCloseControlFrame(context);
                                    return;
                                }
                                break;

                            case OpcodePing:
                            case OpcodePong:
                                break;

                            // check for reserved control frame opcodes
                            default:
                                this.ProtocolViolation_ControlFrameUsingReservedOpcode(context, this.frameOpcode);
                                return;
                        }
                    }
                    else // data frame
                    {
                        switch (this.frameOpcode)
                        {
                            case OpcodeCont:
                            case OpcodeText:
                            case OpcodeBinary:
                                break;
                            // check for reserved data frame opcodes
                            default:
                                this.ProtocolViolation_DataFrameUsingReservedOpcode(context, this.frameOpcode);
                                return;
                        }

                        uint uFragmentedFramesCount = (uint)this.fragmentedFramesCount;
                        // check opcode vs message fragmentation state 1/2
                        if (0u >= uFragmentedFramesCount && this.frameOpcode == OpcodeCont)
                        {
                            this.ProtocolViolation_RecContionuationDataFrame(context);
                            return;
                        }

                        // check opcode vs message fragmentation state 2/2
                        if (uFragmentedFramesCount > 0u && this.frameOpcode != OpcodeCont && this.frameOpcode != OpcodePing)
                        {
                            this.ProtocolViolation_RecNonContionuationDataFrame(context);
                            return;
                        }
                    }

                    this.state = State.ReadingSize;
                    goto case State.ReadingSize;
                case State.ReadingSize:
                    // Read frame payload length
                    switch (this.framePayloadLen1)
                    {
                        case 126:
                            if (input.ReadableBytes < 2)
                            {
                                return;
                            }
                            this.framePayloadLength = input.ReadUnsignedShort();
                            if (this.framePayloadLength < 126)
                            {
                                this.ProtocolViolation_InvalidDataFrameLength(context);
                                return;
                            }
                            break;

                        case 127:
                            if (input.ReadableBytes < 8)
                            {
                                return;
                            }
                            this.framePayloadLength = input.ReadLong();
                            // TODO: check if it's bigger than 0x7FFFFFFFFFFFFFFF, Maybe
                            // just check if it's negative?

                            if (this.framePayloadLength < 65536)
                            {
                                this.ProtocolViolation_InvalidDataFrameLength(context);
                                return;
                            }
                            break;

                        default:
                            this.framePayloadLength = this.framePayloadLen1;
                            break;
                    }

                    if (this.framePayloadLength > this.maxFramePayloadLength)
                    {
                        this.ProtocolViolation_MaxFrameLengthHasBeenExceeded(context, this.maxFramePayloadLength);
                        return;
                    }

                    if (Logger.DebugEnabled)
                    {
                        Logger.DecodingWebSocketFrameLength(this.framePayloadLength);
                    }

                    this.state = State.MaskingKey;
                    goto case State.MaskingKey;
                case State.MaskingKey:
                    if (this.frameMasked)
                    {
                        if (input.ReadableBytes < 4)
                        {
                            return;
                        }
                        if (this.maskingKey == null)
                        {
                            this.maskingKey = new byte[4];
                        }
                        input.ReadBytes(this.maskingKey);
                    }
                    this.state = State.Payload;
                    goto case State.Payload;
                case State.Payload:
                    if (input.ReadableBytes < this.framePayloadLength)
                    {
                        return;
                    }

                    IByteBuffer payloadBuffer = null;
                    try
                    {
                        payloadBuffer = ReadBytes(context.Allocator, input, ToFrameLength(this.framePayloadLength));

                        // Now we have all the data, the next checkpoint must be the next
                        // frame
                        this.state = State.ReadingFirst;

                        // Unmask data if needed
                        if (this.frameMasked)
                        {
                            this.Unmask(payloadBuffer);
                        }

                        // Processing ping/pong/close frames because they cannot be
                        // fragmented
                        switch (this.frameOpcode)
                        {
                            case OpcodePing:
                                output.Add(new PingWebSocketFrame(this.frameFinalFlag, this.frameRsv, payloadBuffer));
                                payloadBuffer = null;
                                return;

                            case OpcodePong:
                                output.Add(new PongWebSocketFrame(this.frameFinalFlag, this.frameRsv, payloadBuffer));
                                payloadBuffer = null;
                                return;

                            case OpcodeClose:
                                this.receivedClosingHandshake = true;
                                this.CheckCloseFrameBody(context, payloadBuffer);
                                output.Add(new CloseWebSocketFrame(this.frameFinalFlag, this.frameRsv, payloadBuffer));
                                payloadBuffer = null;
                                return;
                        }

                        // Processing for possible fragmented messages for text and binary
                        // frames
                        if (this.frameFinalFlag)
                        {
                            // Final frame of the sequence. Apparently ping frames are
                            // allowed in the middle of a fragmented message
                            if (this.frameOpcode != OpcodePing)
                            {
                                this.fragmentedFramesCount = 0;
                            }
                        }
                        else
                        {
                            // Increment counter
                            this.fragmentedFramesCount++;
                        }

                        // Return the frame
                        switch (this.frameOpcode)
                        {
                            case OpcodeText:
                                output.Add(new TextWebSocketFrame(this.frameFinalFlag, this.frameRsv, payloadBuffer));
                                payloadBuffer = null;
                                return;

                            case OpcodeBinary:
                                output.Add(new BinaryWebSocketFrame(this.frameFinalFlag, this.frameRsv, payloadBuffer));
                                payloadBuffer = null;
                                return;

                            case OpcodeCont:
                                output.Add(new ContinuationWebSocketFrame(this.frameFinalFlag, this.frameRsv, payloadBuffer));
                                payloadBuffer = null;
                                return;

                            default:
                                ThrowNotSupportedException(this.frameOpcode); return;
                        }
                    }
                    finally
                    {
                        payloadBuffer?.Release();
                    }
                case State.Corrupt:
                    if (input.IsReadable())
                    {
                        // If we don't keep reading Netty will throw an exception saying
                        // we can't return null if no bytes read and state not changed.
                        input.ReadByte();
                    }
                    return;
                default:
                    ThrowHelper.ThrowException_FrameDecoder(); break;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowNotSupportedException(int frameOpcode)
        {
            throw GetNotSupportedException();

            NotSupportedException GetNotSupportedException()
            {
                return new NotSupportedException($"Cannot decode web socket frame with opcode: {frameOpcode}");
            }
        }

        void Unmask(IByteBuffer frame)
        {
            int i = frame.ReaderIndex;
            int end = frame.WriterIndex;

            int intMask = (this.maskingKey[0] << 24)
                  | (this.maskingKey[1] << 16)
                  | (this.maskingKey[2] << 8)
                  | this.maskingKey[3];

            for (; i + 3 < end; i += 4)
            {
                int unmasked = frame.GetInt(i) ^ intMask;
                frame.SetInt(i, unmasked);
            }
            for (; i < end; i++)
            {
                frame.SetByte(i, frame.GetByte(i) ^ this.maskingKey[i % 4]);
            }
        }

        internal void ProtocolViolation0(IChannelHandlerContext ctx, string reason) => this.ProtocolViolation(ctx, new CorruptedFrameException(reason));

        void ProtocolViolation(IChannelHandlerContext ctx, CorruptedFrameException ex)
        {
            this.state = State.Corrupt;
            if (ctx.Channel.Active)
            {
                object closeMessage;
                if (this.receivedClosingHandshake)
                {
                    closeMessage = Unpooled.Empty;
                }
                else
                {
                    closeMessage = new CloseWebSocketFrame(1002, null);
                }
#if NET40
                Action<Task> closeOnComplete = (Task t) => ctx.Channel.CloseAsync();
                ctx.WriteAndFlushAsync(closeMessage).ContinueWith(closeOnComplete, TaskContinuationOptions.ExecuteSynchronously);
#else
                ctx.WriteAndFlushAsync(closeMessage).ContinueWith(CloseOnCompleteAction, ctx.Channel, TaskContinuationOptions.ExecuteSynchronously);
#endif
            }
            throw ex;
        }

        [MethodImpl(InlineMethod.Value)]
        static int ToFrameLength(long l)
        {
            if (l > int.MaxValue)
            {
                ThrowTooLongFrameException(l);
            }
            return (int)l;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowTooLongFrameException(long l)
        {
            throw GetTooLongFrameException();

            TooLongFrameException GetTooLongFrameException()
            {
                return new TooLongFrameException(string.Format("Length: {0}", l));
            }
        }

        protected void CheckCloseFrameBody(IChannelHandlerContext ctx, IByteBuffer buffer)
        {
            if (buffer == null || !buffer.IsReadable())
            {
                return;
            }
            if (buffer.ReadableBytes == 1)
            {
                this.ProtocolViolation_InvalidCloseFrameBody(ctx);
            }

            // Save reader index
            int idx = buffer.ReaderIndex;
            buffer.SetReaderIndex(0);

            // Must have 2 byte integer within the valid range
            int statusCode = buffer.ReadShort();
            if (statusCode.IsInvalidCloseFrameStatusCodeRfc6455())
            {
                this.ProtocolViolation_InvalidCloseFrameStatusCode(ctx, statusCode);
            }

            // May have UTF-8 message
            if (buffer.IsReadable())
            {
                try
                {
                    new Utf8Validator().Check(buffer);
                }
                catch (CorruptedFrameException ex)
                {
                    this.ProtocolViolation(ctx, ex);
                }
            }

            // Restore reader index
            buffer.SetReaderIndex(idx);
        }
    }
}
