// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Builder for the <see cref="Http2FrameCodec"/>.
    /// </summary>
    public class Http2FrameCodecBuilder : AbstractHttp2ConnectionHandlerBuilder<Http2FrameCodec, Http2FrameCodecBuilder>
    {
        private IHttp2FrameWriter frameWriter;


        public Http2FrameCodecBuilder(bool isServer)
        {
            this.IsServer = isServer;
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
            this.frameWriter = frameWriter;
            return this;
        }

        /// <summary>
        /// Build a <see cref="Http2FrameCodec"/> object.
        /// </summary>
        public override Http2FrameCodec Build()
        {
            var frameWriter = this.frameWriter;
            if (frameWriter is object)
            {
                // This is to support our tests and will never be executed by the user as frameWriter(...)
                // is package-private.
                DefaultHttp2Connection connection = new DefaultHttp2Connection(this.IsServer, this.MaxReservedStreams);
                var maxHeaderListSize = this.InitialSettings.MaxHeaderListSize();
                IHttp2FrameReader frameReader = new DefaultHttp2FrameReader(!maxHeaderListSize.HasValue ?
                        new DefaultHttp2HeadersDecoder(true) :
                        new DefaultHttp2HeadersDecoder(true, maxHeaderListSize.Value));

                if (this.FrameLogger is object)
                {
                    frameWriter = new Http2OutboundFrameLogger(frameWriter, this.FrameLogger);
                    frameReader = new Http2InboundFrameLogger(frameReader, this.FrameLogger);
                }
                IHttp2ConnectionEncoder encoder = new DefaultHttp2ConnectionEncoder(connection, frameWriter);
                if (this.EncoderEnforceMaxConcurrentStreams)
                {
                    encoder = new StreamBufferingEncoder(encoder);
                }
                IHttp2ConnectionDecoder decoder = new DefaultHttp2ConnectionDecoder(connection, encoder, frameReader);

                return this.Build(decoder, encoder, this.InitialSettings);
            }
            return base.Build();
        }

        protected override Http2FrameCodec Build(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings)
        {
            return new Http2FrameCodec(encoder, decoder, initialSettings);
        }
    }
}
