// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable UseStringInterpolation
namespace DotNetty.Codecs.Http.WebSockets
{
    using CuteAnt.Collections;
    using DotNetty.Transport.Channels;

    internal static class ProtocolViolationHelper
    {
        private static readonly CachedReadConcurrentDictionary<int, bool> _invalidCloseFrameStatusCodeCache =
            new CachedReadConcurrentDictionary<int, bool>();

        public static bool IsInvalidCloseFrameStatusCodeRfc6455(this int statusCode)
        {
            return _invalidCloseFrameStatusCodeCache.GetOrAdd(statusCode,
                code => code >= 0 && code <= 999 || code >= 1004 && code <= 1006 || code >= 1012 && code <= 2999);
        }

        public static void ProtocolViolation_InvalidCloseFrameBody(this WebSocket08FrameDecoder decoder, IChannelHandlerContext ctx)
        {
            decoder.ProtocolViolation0(ctx, "Invalid close frame body");
        }

        public static void ProtocolViolation_InvalidCloseFrameStatusCode(this WebSocket08FrameDecoder decoder, IChannelHandlerContext ctx, int statusCode)
        {
            decoder.ProtocolViolation0(ctx, $"Invalid close frame getStatus code: {statusCode}");
        }

        public static void ProtocolViolation_MaxFrameLengthHasBeenExceeded(this WebSocket08FrameDecoder decoder, IChannelHandlerContext ctx, long maxFramePayloadLength)
        {
            decoder.ProtocolViolation0(ctx, $"Max frame length of {maxFramePayloadLength} has been exceeded.");
        }

        public static void ProtocolViolation_InvalidDataFrameLength(this WebSocket08FrameDecoder decoder, IChannelHandlerContext ctx)
        {
            decoder.ProtocolViolation0(ctx, "invalid data frame length (not using minimal length encoding)");
        }

        public static void ProtocolViolation_RecNonContionuationDataFrame(this WebSocket08FrameDecoder decoder, IChannelHandlerContext ctx)
        {
            decoder.ProtocolViolation0(ctx, "received non-continuation data frame while inside fragmented message");
        }

        public static void ProtocolViolation_RecContionuationDataFrame(this WebSocket08FrameDecoder decoder, IChannelHandlerContext ctx)
        {
            decoder.ProtocolViolation0(ctx, "received continuation data frame outside fragmented message");
        }

        public static void ProtocolViolation_DataFrameUsingReservedOpcode(this WebSocket08FrameDecoder decoder, IChannelHandlerContext ctx, int frameOpcode)
        {
            decoder.ProtocolViolation0(ctx, $"data frame using reserved opcode {frameOpcode}");
        }

        public static void ProtocolViolation_RecCloseControlFrame(this WebSocket08FrameDecoder decoder, IChannelHandlerContext ctx)
        {
            decoder.ProtocolViolation0(ctx, "received close control frame with payload len 1");
        }

        public static void ProtocolViolation_ControlFrameUsingReservedOpcode(this WebSocket08FrameDecoder decoder, IChannelHandlerContext ctx, int frameOpcode)
        {
            decoder.ProtocolViolation0(ctx, $"control frame using reserved opcode {frameOpcode}");
        }

        public static void ProtocolViolation_ControlFrameWithPayloadLength125Octets(this WebSocket08FrameDecoder decoder, IChannelHandlerContext ctx)
        {
            decoder.ProtocolViolation0(ctx, "control frame with payload length > 125 octets");
        }

        public static void ProtocolViolation_FragmentedControlFrame(this WebSocket08FrameDecoder decoder, IChannelHandlerContext ctx)
        {
            decoder.ProtocolViolation0(ctx, "fragmented control frame");
        }

        public static void ProtocolViolation_RecAFrameThatIsNotMaskedAsExected(this WebSocket08FrameDecoder decoder, IChannelHandlerContext ctx)
        {
            decoder.ProtocolViolation0(ctx, "received a frame that is not masked as expected");
        }

        public static void ProtocolViolation_RSVNoExtensionNegotiated(this WebSocket08FrameDecoder decoder, IChannelHandlerContext ctx, int frameRsv)
        {
            decoder.ProtocolViolation0(ctx, $"RSV != 0 and no extension negotiated, RSV:{frameRsv}");
        }
    }
}
