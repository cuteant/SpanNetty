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

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Base64;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Server-side codec for performing a cleartext upgrade from HTTP/1.x to HTTP/2.
    /// </summary>
    public class Http2ServerUpgradeCodec : HttpServerUpgradeHandler.IUpgradeCodec
    {
        private static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<Http2ServerUpgradeCodec>();
        private static readonly AsciiString[] REQUIRED_UPGRADE_HEADERS = new[] { Http2CodecUtil.HttpUpgradeSettingsHeader };
        private static readonly IChannelHandler[] EMPTY_HANDLERS = new IChannelHandler[0];

        private readonly string _handlerName;
        private readonly Http2ConnectionHandler _connectionHandler;
        private readonly IChannelHandler[] _handlers;
        private readonly IHttp2FrameReader _frameReader;

        private Http2Settings _settings;

        /// <summary>
        /// Creates the codec using a default name for the connection handler when adding to the pipeline.
        /// </summary>
        /// <param name="connectionHandler">the HTTP/2 connection handler</param>
        public Http2ServerUpgradeCodec(Http2ConnectionHandler connectionHandler)
            : this(null, connectionHandler, EMPTY_HANDLERS)
        {
        }

        /// <summary>
        /// Creates the codec using a default name for the connection handler when adding to the pipeline.
        /// </summary>
        /// <param name="http2Codec">the HTTP/2 multiplexing handler.</param>
        public Http2ServerUpgradeCodec(Http2MultiplexCodec http2Codec)
            : this(null, http2Codec, EMPTY_HANDLERS)
        {
        }

        /// <summary>
        /// Creates the codec providing an upgrade to the given handler for HTTP/2.
        /// </summary>
        /// <param name="handlerName">the name of the HTTP/2 connection handler to be used in the pipeline,
        /// or <c>null</c> to auto-generate the name.</param>
        /// <param name="connectionHandler">the HTTP/2 connection handler</param>
        public Http2ServerUpgradeCodec(string handlerName, Http2ConnectionHandler connectionHandler)
            : this(handlerName, connectionHandler, EMPTY_HANDLERS)
        {
        }

        /// <summary>
        /// Creates the codec providing an upgrade to the given handler for HTTP/2.
        /// </summary>
        /// <param name="handlerName">the name of the HTTP/2 connection handler to be used in the pipeline.</param>
        /// <param name="http2Codec">the HTTP/2 multiplexing handler.</param>
        public Http2ServerUpgradeCodec(string handlerName, Http2MultiplexCodec http2Codec)
            : this(handlerName, http2Codec, EMPTY_HANDLERS)
        {
        }

        /// <summary>
        /// Creates the codec using a default name for the connection handler when adding to the pipeline.
        /// </summary>
        /// <param name="http2Codec">the HTTP/2 frame handler.</param>
        /// <param name="handlers">the handlers that will handle the <see cref="IHttp2Frame"/>s.</param>
        public Http2ServerUpgradeCodec(Http2FrameCodec http2Codec, params IChannelHandler[] handlers)
            : this(null, http2Codec, handlers)
        {
        }

        private Http2ServerUpgradeCodec(string handlerName, Http2ConnectionHandler connectionHandler, params IChannelHandler[] handlers)
        {
            _handlerName = handlerName;
            _connectionHandler = connectionHandler;
            _handlers = handlers;
            _frameReader = new DefaultHttp2FrameReader();
        }

        public virtual IReadOnlyList<AsciiString> RequiredUpgradeHeaders => REQUIRED_UPGRADE_HEADERS;

        public virtual bool PrepareUpgradeResponse(IChannelHandlerContext ctx, IFullHttpRequest upgradeRequest, HttpHeaders headers)
        {
            try
            {
                // Decode the HTTP2-Settings header and set the settings on the handler to make
                // sure everything is fine with the request.
                var upgradeHeaders = upgradeRequest.Headers.GetAll(Http2CodecUtil.HttpUpgradeSettingsHeader);
                var upgradeHeadersCount = upgradeHeaders.Count;
                if (upgradeHeadersCount <= 0 || upgradeHeadersCount > 1)
                {
                    ThrowHelper.ThrowArgumentException_MustOnlyOne();
                }
                _settings = DecodeSettingsHeader(ctx, upgradeHeaders[0]);
                // Everything looks good.
                return true;
            }
            catch (Exception cause)
            {
                if (Logger.InfoEnabled) { Logger.ErrorDuringUpgradeToHTTP2(cause); }
                return false;
            }
        }

        public virtual void UpgradeTo(IChannelHandlerContext ctx, IFullHttpRequest upgradeRequest)
        {
            try
            {
                var pipeline = ctx.Pipeline;
                // Add the HTTP/2 connection handler to the pipeline immediately following the current handler.
                _ = pipeline.AddAfter(ctx.Name, _handlerName, _connectionHandler);

                // Add also all extra handlers as these may handle events / messages produced by the connectionHandler.
                // See https://github.com/netty/netty/issues/9314
                if (_handlers != null)
                {
                    var name = pipeline.Context(_connectionHandler).Name;
                    for (int i = _handlers.Length - 1; i >= 0; i--)
                    {
                        _ = pipeline.AddAfter(name, null, _handlers[i]);
                    }
                }
                _connectionHandler.OnHttpServerUpgrade(_settings);
            }
            catch (Http2Exception e)
            {
                _ = ctx.FireExceptionCaught(e);
                _ = ctx.CloseAsync();
            }
        }

        /// <summary>
        /// Decodes the settings header and returns a <see cref="Http2Settings"/> object.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="settingsHeader"></param>
        /// <returns></returns>
        private Http2Settings DecodeSettingsHeader(IChannelHandlerContext ctx, ICharSequence settingsHeader)
        {
            var header = ByteBufferUtil.EncodeString(ctx.Allocator, settingsHeader.ToString(), Encoding.UTF8);
            try
            {
                // Decode the SETTINGS payload.
                var payload = Base64.Decode(header, Base64Dialect.UrlSafe);

                // Create an HTTP/2 frame for the settings.
                var frame = CreateSettingsFrame(ctx, payload);

                // Decode the SETTINGS frame and return the settings object.
                return DecodeSettings(ctx, frame);
            }
            finally
            {
                _ = header.Release();
            }
        }

        /// <summary>
        /// Decodes the settings frame and returns the settings.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="frame"></param>
        /// <returns></returns>
        private Http2Settings DecodeSettings(IChannelHandlerContext ctx, IByteBuffer frame)
        {
            try
            {
                var decodedSettings = new Http2Settings();
                _frameReader.ReadFrame(ctx, frame, new DelegatingFrameAdapter(decodedSettings));
                return decodedSettings;
            }
            finally
            {
                _ = frame.Release();
            }
        }

        /// <summary>
        /// Creates an HTTP2-Settings header with the given payload. The payload buffer is released.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="payload"></param>
        /// <returns></returns>
        private static IByteBuffer CreateSettingsFrame(IChannelHandlerContext ctx, IByteBuffer payload)
        {
            var frame = ctx.Allocator.Buffer(Http2CodecUtil.FrameHeaderLength + payload.ReadableBytes);
            Http2CodecUtil.WriteFrameHeader(frame, payload.ReadableBytes, Http2FrameTypes.Settings, new Http2Flags(), 0);
            _ = frame.WriteBytes(payload);
            _ = payload.Release();
            return frame;
        }

        sealed class DelegatingFrameAdapter : Http2FrameAdapter
        {
            readonly Http2Settings _decodedSettings;

            public DelegatingFrameAdapter(Http2Settings decodedSettings) => _decodedSettings = decodedSettings;

            public override void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings)
            {
                _ = _decodedSettings.CopyFrom(settings);
            }
        }
    }
}
