// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using DotNetty.Buffers;
using DotNetty.Common.Internal.Logging;
using DotNetty.Common.Utilities;
using DotNetty.Handlers.Flow;
using DotNetty.Handlers.Streams;
using DotNetty.Transport.Channels;

namespace DotNetty.Handlers
{
    internal static class HttpLoggingExtensions
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void NonEmptyQueue(this IInternalLogger logger, RecyclableQueue queue)
        {
            logger.Trace($"Non-empty queue: {queue}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void UnexpectedClientHelloPacket(this IInternalLogger logger, IByteBuffer input, Exception e)
        {
            logger.Warn($"Unexpected client hello packet: {ByteBufferUtil.HexDump(input)}", e);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void IsEndOfInputFailed<T>(this IInternalLogger logger, Exception exception)
        {
            logger.Warn($"{StringUtil.SimpleClassName(typeof(ChunkedWriteHandler<T>))}.IsEndOfInput failed", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void UnexpectedExceptionWhileSendingChunks(this IInternalLogger logger, Exception exception)
        {
            logger.Warn("Unexpected exception while sending chunks.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FailedToCloseAChunkedInput(this IInternalLogger logger, Exception exception)
        {
            logger.Warn("Failed to close a chunked input.", exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void TlsHandshakeFailure(this IInternalLogger logger, IChannelHandlerContext ctx, Exception cause)
        {
            logger.Warn("{} TLS handshake failed:", ctx.Channel, cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FailedToSelectAppProtocol(this IInternalLogger logger, IChannelHandlerContext ctx, Exception cause)
        {
            logger.Warn("{} Failed to select the application-level protocol:", ctx.Channel, cause);
        }
    }
}
