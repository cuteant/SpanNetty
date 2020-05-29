// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// A builder for <see cref="Http2MultiplexCodec"/>.
    /// 
    /// <para>Deprecated use <see cref="Http2FrameCodecBuilder"/> together with <see cref="Http2MultiplexHandler"/>.</para>
    /// </summary>
    public class Http2MultiplexCodecBuilder : AbstractHttp2ConnectionHandlerBuilder<Http2MultiplexCodec, Http2MultiplexCodecBuilder>
    {
        internal readonly IChannelHandler _childHandler;
        private IChannelHandler _upgradeStreamHandler;
        private IHttp2FrameWriter _frameWriter;

        public Http2MultiplexCodecBuilder(bool server, IChannelHandler childHandler)
        {
            if (childHandler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.childHandler); }
            IsServer = server;
            _childHandler = CheckSharable(childHandler);
            // For backwards compatibility we should disable to timeout by default at this layer.
            GracefulShutdownTimeout = TimeSpan.Zero;
        }

        private static IChannelHandler CheckSharable(IChannelHandler handler)
        {
            if (handler is ChannelHandlerAdapter handlerAdapter && !handlerAdapter.IsSharable)
            {
                ThrowHelper.ThrowArgumentException_TheHandlerMustBeSharable();
            }
            return handler;
        }

        // For testing only.
        internal Http2MultiplexCodecBuilder FrameWriter(IHttp2FrameWriter frameWriter)
        {
            if (frameWriter is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.frameWriter); }
            _frameWriter = frameWriter;
            return this;
        }

        /// <summary>
        /// Creates a builder for a HTTP/2 client.
        /// </summary>
        /// <param name="childHandler">the handler added to channels for remotely-created streams. It must be
        /// {@link ChannelHandler.Sharable}</param>
        /// <returns></returns>
        public static Http2MultiplexCodecBuilder ForClient(IChannelHandler childHandler)
        {
            return new Http2MultiplexCodecBuilder(false, childHandler);
        }

        /// <summary>
        /// Creates a builder for a HTTP/2 server.
        /// </summary>
        /// <param name="childHandler">the handler added to channels for remotely-created streams. It must be
        /// {@link ChannelHandler.Sharable}.</param>
        /// <returns></returns>
        public static Http2MultiplexCodecBuilder ForServer(IChannelHandler childHandler)
        {
            return new Http2MultiplexCodecBuilder(true, childHandler);
        }

        public Http2MultiplexCodecBuilder WithUpgradeStreamHandler(IChannelHandler upgradeStreamHandler)
        {
            if (IsServer)
            {
                ThrowHelper.ThrowArgumentException_ServerCodecsDonotUseAnExtraHandlerForTheUpgradeStream();
            }
            _upgradeStreamHandler = upgradeStreamHandler;
            return this;
        }

        /// <inheritdoc />
        public override Http2MultiplexCodec Build()
        {
            var frameWriter = _frameWriter;
            if (frameWriter is object)
            {
                // This is to support our tests and will never be executed by the user as frameWriter(...)
                // is package-private.
                var connection = new DefaultHttp2Connection(IsServer, MaxReservedStreams);
                var maxHeaderListSize = InitialSettings.MaxHeaderListSize();
                IHttp2FrameReader frameReader = new DefaultHttp2FrameReader(!maxHeaderListSize.HasValue ?
                        new DefaultHttp2HeadersDecoder(true) :
                        new DefaultHttp2HeadersDecoder(true, maxHeaderListSize.Value));

                var frameLogger = FrameLogger;
                if (frameLogger is object)
                {
                    frameWriter = new Http2OutboundFrameLogger(frameWriter, frameLogger);
                    frameReader = new Http2InboundFrameLogger(frameReader, frameLogger);
                }
                IHttp2ConnectionEncoder encoder = new DefaultHttp2ConnectionEncoder(connection, frameWriter);
                if (EncoderEnforceMaxConcurrentStreams)
                {
                    encoder = new StreamBufferingEncoder(encoder);
                }
                IHttp2ConnectionDecoder decoder = new DefaultHttp2ConnectionDecoder(connection, encoder, frameReader,
                        PromisedRequestVerifier, AutoAckSettingsFrame, AutoAckPingFrame);

                return Build(decoder, encoder, InitialSettings);
            }
            return base.Build();
        }

        /// <inheritdoc />
        protected override Http2MultiplexCodec Build(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings)
        {
            return new Http2MultiplexCodec(encoder, decoder, initialSettings, _childHandler, _upgradeStreamHandler, DecoupleCloseAndGoAway)
            {
                GracefulShutdownTimeout = GracefulShutdownTimeout
            };
        }
    }
}
