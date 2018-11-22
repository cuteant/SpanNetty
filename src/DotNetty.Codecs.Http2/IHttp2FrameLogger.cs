// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public interface IHttp2FrameLogger: IChannelHandler
    {
        bool IsEnabled();

        void LogData(Direction direction, IChannelHandlerContext ctx, int streamId, IByteBuffer data,
            int padding, bool endStream);

        void LogHeaders(Direction direction, IChannelHandlerContext ctx, int streamId, IHttp2Headers headers,
            int padding, bool endStream);

        void LogHeaders(Direction direction, IChannelHandlerContext ctx, int streamId, IHttp2Headers headers,
            int streamDependency, short weight, bool exclusive, int padding, bool endStream);

        void LogPriority(Direction direction, IChannelHandlerContext ctx, int streamId, int streamDependency,
            short weight, bool exclusive);

        void LogRstStream(Direction direction, IChannelHandlerContext ctx, int streamId, Http2Error errorCode);

        void LogSettingsAck(Direction direction, IChannelHandlerContext ctx);

        void LogSettings(Direction direction, IChannelHandlerContext ctx, Http2Settings settings);

        void LogPing(Direction direction, IChannelHandlerContext ctx, long data);

        void LogPingAck(Direction direction, IChannelHandlerContext ctx, long data);

        void LogPushPromise(Direction direction, IChannelHandlerContext ctx, int streamId, int promisedStreamId,
            IHttp2Headers headers, int padding);

        void LogGoAway(Direction direction, IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData);

        void LogWindowsUpdate(Direction direction, IChannelHandlerContext ctx, int streamId, int windowSizeIncrement);

        void LogUnknownFrame(Direction direction, IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId,
            Http2Flags flags, IByteBuffer data);


    }
}
