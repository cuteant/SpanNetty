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
        /// Creates a builder for an HTTP/2 client.
        /// </summary>
        public static Http2FrameCodecBuilder ForClient()
        {
            return new Http2FrameCodecBuilder(false);
        }

        /// <summary>
        /// Creates a builder for an HTTP/2 server.
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
                        new DefaultHttp2HeadersDecoder(IsValidateHeaders) :
                        new DefaultHttp2HeadersDecoder(IsValidateHeaders, maxHeaderListSize.Value));

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
