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

using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Common.Concurrency;
using DotNetty.Common.Internal.Logging;
using DotNetty.Transport.Channels;

namespace DotNetty.Codecs.Http2
{
    internal static class Http2LoggingExtensions
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SendGoAwaySuccess(this IInternalLogger logger, IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData, Task future)
        {
            logger.Debug("{} Sent GOAWAY: lastStreamId '{}', errorCode '{}', " +
                         "debugData '{}'. Forcing shutdown of the connection.",
                         ctx.Channel, lastStreamId, errorCode, debugData.ToString(Encoding.UTF8), future.Exception?.InnerException);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SendingGoAwayFailed(this IInternalLogger logger, IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData, Task future)
        {
            logger.Debug("{} Sending GOAWAY failed: lastStreamId '{}', errorCode '{}', " +
                         "debugData '{}'. Forcing shutdown of the connection.",
                         ctx.Channel, lastStreamId, errorCode, debugData.ToString(Encoding.UTF8), future.Exception?.InnerException);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ErrorDuringUpgradeToHTTP2(this IInternalLogger logger, Exception cause)
        {
            logger.Info("Error during upgrade to HTTP/2", cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void IgnoringFrameForStream(this IInternalLogger logger, IChannelHandlerContext ctx, Http2FrameTypes frameName, int streamId)
        {
            logger.Info("{} ignoring {} frame for stream {}. Stream sent after GOAWAY sent",
                        ctx.Channel, frameName, streamId);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void IgnoringFrameForStreamRst(this IInternalLogger logger, IChannelHandlerContext ctx, Http2FrameTypes frameName, bool isResetSent, int lastStreamKnownByPeer)
        {
            logger.Info("{} ignoring {} frame for stream {}", ctx.Channel, frameName,
                    isResetSent ? "RST_STREAM sent." :
                        ("Stream created after GOAWAY sent. Last known stream by peer " +
                         lastStreamKnownByPeer));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Maximum_number_of_outstanding_control_frames_reached(this IInternalLogger logger, int maxOutstandingControlFrames, IChannelHandlerContext ctx, Http2Exception exception)
        {
            logger.Info("Maximum number {} of outstanding control frames reached. Closing channel {}",
                    maxOutstandingControlFrames, ctx.Channel, exception);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void StreamExceptionThrownWithoutStreamObjectAttached(this IInternalLogger logger, Exception cause)
        {
            logger.Warn("Stream exception thrown without stream object attached.", cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void StreamExceptionThrownForUnkownStreamD(this IInternalLogger logger, int streamId, Exception cause)
        {
            logger.Debug("Stream exception thrown for unknown stream {}.", streamId, cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void StreamExceptionThrownForUnkownStream(this IInternalLogger logger, int streamId, Exception cause)
        {
            logger.Warn("Stream exception thrown for unknown stream {}.", streamId, cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CanotInvokeTaskLaterAsEventLoopRejectedIt(this IInternalLogger logger, RejectedExecutionException e)
        {
            logger.Warn("Can't invoke task later as EventLoop rejected it", e);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CaughtExceptionFromListenerWritabilityChanged(this IInternalLogger logger, Exception cause)
        {
            logger.Error("Caught Exception from listener.writabilityChanged", cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CaughtExceptionWhileProcessingPendingActiveStreamsEvent(this IInternalLogger logger, Exception cause)
        {
            logger.Error("Caught Exception while processing pending ActiveStreams$Event.", cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CaughtExceptionFromListenerOnStreamActive(this IInternalLogger logger, Exception cause)
        {
            logger.Error("Caught Exception from listener onStreamActive.", cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CaughtExceptionFromListenerOnStreamAdded(this IInternalLogger logger, Exception cause)
        {
            logger.Error("Caught Exception from listener onStreamAdded.", cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CaughtExceptionFromListenerOnStreamClosed(this IInternalLogger logger, Exception cause)
        {
            logger.Error("Caught Exception from listener onStreamClosed.", cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CaughtExceptionFromListenerOnStreamHalfClosed(this IInternalLogger logger, Exception cause)
        {
            logger.Error("Caught Exception from listener onStreamHalfClosed.", cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CaughtExceptionFromListenerOnStreamRemoved(this IInternalLogger logger, Exception cause)
        {
            logger.Error("Caught Exception from listener onStreamRemoved.", cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CaughtExceptionFromListenerOnGoAwaySent(this IInternalLogger logger, Exception cause)
        {
            logger.Error("Caught Exception from listener onGoAwaySent.", cause);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void CaughtExceptionFromListenerOnGoAwayReceived(this IInternalLogger logger, Exception cause)
        {
            logger.Error("Caught Exception from listener onGoAwayReceived.", cause);
        }
    }
}
