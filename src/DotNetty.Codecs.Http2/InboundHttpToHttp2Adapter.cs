// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Codecs.Http;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Translates HTTP/1.x object reads into HTTP/2 frames.
    /// </summary>
    public class InboundHttpToHttp2Adapter : ChannelHandlerAdapter
    {
        private readonly IHttp2Connection connection;
        private readonly IHttp2FrameListener listener;

        public InboundHttpToHttp2Adapter(IHttp2Connection connection, IHttp2FrameListener listener)
        {
            this.connection = connection;
            this.listener = listener;
        }

        private static int GetStreamId(IHttp2Connection connection, HttpHeaders httpHeaders)
        {
            return httpHeaders.GetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId,
                                      connection.Remote.IncrementAndGetNextStreamId);
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            if (message is IFullHttpMessage fullHttpMessage)
            {
                Handle(ctx, connection, listener, fullHttpMessage);
            }
            else
            {
                ctx.FireChannelRead(message);
            }
        }

        // note that this may behave strangely when used for the initial upgrade
        // message when using h2c, since that message is ineligible for flow
        // control, but there is not yet an API for signaling that.
        internal static void Handle(IChannelHandlerContext ctx, IHttp2Connection connection,
            IHttp2FrameListener listener, IFullHttpMessage message)
        {
            try
            {
                int streamId = GetStreamId(connection, message.Headers);
                IHttp2Stream stream = connection.Stream(streamId);
                if (stream == null)
                {
                    stream = connection.Remote.CreateStream(streamId, false);
                }
                message.Headers.Set(HttpConversionUtil.ExtensionHeaderNames.Scheme, HttpScheme.Http.Name);
                IHttp2Headers messageHeaders = HttpConversionUtil.ToHttp2Headers(message, true);
                var hasContent = message.Content.IsReadable();
                var hasTrailers = !message.TrailingHeaders.IsEmpty;
                listener.OnHeadersRead(ctx, streamId, messageHeaders, 0, !(hasContent || hasTrailers));
                if (hasContent)
                {
                    listener.OnDataRead(ctx, streamId, message.Content, 0, !hasTrailers);
                }
                if (hasTrailers)
                {
                    IHttp2Headers headers = HttpConversionUtil.ToHttp2Headers(message.TrailingHeaders, true);
                    listener.OnHeadersRead(ctx, streamId, headers, 0, true);
                }
                stream.CloseRemoteSide();
            }
            finally
            {
                message.Release();
            }
        }
    }
}
