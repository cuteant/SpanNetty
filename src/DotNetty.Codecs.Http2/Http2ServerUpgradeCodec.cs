// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        private readonly string handlerName;
        private readonly Http2ConnectionHandler connectionHandler;
        private readonly IChannelHandler[] handlers;
        private readonly IHttp2FrameReader frameReader;

        private Http2Settings settings;

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
            this.handlerName = handlerName;
            this.connectionHandler = connectionHandler;
            this.handlers = handlers;
            this.frameReader = new DefaultHttp2FrameReader();
        }

        public virtual ICollection<AsciiString> RequiredUpgradeHeaders => REQUIRED_UPGRADE_HEADERS;

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
                settings = this.DecodeSettingsHeader(ctx, upgradeHeaders[0]);
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
                // Add the HTTP/2 connection handler to the pipeline immediately following the current handler.
                ctx.Pipeline.AddAfter(ctx.Name, handlerName, connectionHandler);
                connectionHandler.OnHttpServerUpgrade(settings);
            }
            catch (Http2Exception e)
            {
                ctx.FireExceptionCaught(e);
                ctx.CloseAsync();
                return;
            }

            if (handlers is object)
            {
                var name = ctx.Pipeline.Context(connectionHandler).Name;
                for (int i = handlers.Length - 1; i >= 0; i--)
                {
                    ctx.Pipeline.AddAfter(name, null, handlers[i]);
                }
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
                var payload = Base64.Decode(header, Base64Dialect.URL_SAFE);

                // Create an HTTP/2 frame for the settings.
                var frame = CreateSettingsFrame(ctx, payload);

                // Decode the SETTINGS frame and return the settings object.
                return this.DecodeSettings(ctx, frame);
            }
            finally
            {
                header.Release();
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
                this.frameReader.ReadFrame(ctx, frame, new DelegatingFrameAdapter(decodedSettings));
                return decodedSettings;
            }
            finally
            {
                frame.Release();
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
            frame.WriteBytes(payload);
            payload.Release();
            return frame;
        }

        sealed class DelegatingFrameAdapter : Http2FrameAdapter
        {
            readonly Http2Settings decodedSettings;

            public DelegatingFrameAdapter(Http2Settings decodedSettings) => this.decodedSettings = decodedSettings;

            public override void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings)
            {
                this.decodedSettings.CopyFrom(settings);
            }
        }
    }
}
