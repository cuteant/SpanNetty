// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Builder for the <see cref="Http2FrameCodec"/>.
    /// </summary>
    public class Http2FrameCodecBuilder : AbstractHttp2ConnectionHandlerBuilder<Http2FrameCodec, Http2FrameCodecBuilder>
    {
        private IHttp2FrameWriter _frameWriter;


        public Http2FrameCodecBuilder(bool isServer)
        {
            IsServer = isServer;
            // For backwards compatibility we should disable to timeout by default at this layer.
            GracefulShutdownTimeout = TimeSpan.Zero;
        }

        /// <summary>
        /// Creates a builder for a HTTP/2 client.
        /// </summary>
        public static Http2FrameCodecBuilder ForClient()
        {
            return new Http2FrameCodecBuilder(false);
        }

        /// <summary>
        /// Creates a builder for a HTTP/2 server.
        /// </summary>
        public static Http2FrameCodecBuilder ForServer()
        {
            return new Http2FrameCodecBuilder(true);
        }

        // For testing only.
        internal Http2FrameCodecBuilder FrameWriter(IHttp2FrameWriter frameWriter)
        {
            if (frameWriter is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.frameWriter); }
            _frameWriter = frameWriter;
            return this;
        }

        /// <summary>
        /// Build a <see cref="Http2FrameCodec"/> object.
        /// </summary>
        public override Http2FrameCodec Build()
        {
            var frameWriter = _frameWriter;
            if (frameWriter is object)
            {
                // This is to support our tests and will never be executed by the user as frameWriter(...)
                // is package-private.
                DefaultHttp2Connection connection = new DefaultHttp2Connection(IsServer, MaxReservedStreams);
                var maxHeaderListSize = InitialSettings.MaxHeaderListSize();
                IHttp2FrameReader frameReader = new DefaultHttp2FrameReader(!maxHeaderListSize.HasValue ?
                        new DefaultHttp2HeadersDecoder(true) :
                        new DefaultHttp2HeadersDecoder(true, maxHeaderListSize.Value));

                if (FrameLogger is object)
                {
                    frameWriter = new Http2OutboundFrameLogger(frameWriter, FrameLogger);
                    frameReader = new Http2InboundFrameLogger(frameReader, FrameLogger);
                }
                IHttp2ConnectionEncoder encoder = new DefaultHttp2ConnectionEncoder(connection, frameWriter);
                if (EncoderEnforceMaxConcurrentStreams)
                {
                    encoder = new StreamBufferingEncoder(encoder);
                }
                IHttp2ConnectionDecoder decoder = new DefaultHttp2ConnectionDecoder(connection, encoder, frameReader,
                    PromisedRequestVerifier, AutoAckSettingsFrame, AutoAckPingFrame);

                int maxConsecutiveEmptyDataFrames = DecoderEnforceMaxConsecutiveEmptyDataFrames;
                if ((uint)maxConsecutiveEmptyDataFrames > 0u)
                {
                    decoder = new Http2EmptyDataFrameConnectionDecoder(decoder, maxConsecutiveEmptyDataFrames);
                }

                return Build(decoder, encoder, InitialSettings);
            }
            return base.Build();
        }

        protected override Http2FrameCodec Build(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings)
        {
            return new Http2FrameCodec(encoder, decoder, initialSettings, DecoupleCloseAndGoAway)
            {
                GracefulShutdownTimeout = GracefulShutdownTimeout
            };
        }
    }
}
