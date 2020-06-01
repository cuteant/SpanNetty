// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Base64;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Client-side cleartext upgrade codec from HTTP to HTTP/2.
    /// </summary>
    public class Http2ClientUpgradeCodec : HttpClientUpgradeHandler.IUpgradeCodec
    {
        private static readonly ICharSequence[] s_upgradeHeaders =
            new[] { Http2CodecUtil.HttpUpgradeSettingsHeader };

        private readonly string _handlerName;
        private readonly Http2ConnectionHandler _connectionHandler;
        private readonly IChannelHandler _upgradeToHandler;
        private readonly IChannelHandler _http2MultiplexHandler;

        public Http2ClientUpgradeCodec(Http2FrameCodec frameCodec, IChannelHandler upgradeToHandler)
            : this(null, frameCodec, upgradeToHandler)
        {
        }

        public Http2ClientUpgradeCodec(string handlerName, Http2FrameCodec frameCodec, IChannelHandler upgradeToHandler)
            : this(handlerName, (Http2ConnectionHandler)frameCodec, upgradeToHandler, null)
        {
        }

        /// <summary>
        /// Creates the codec using a default name for the connection handler when adding to the pipeline.
        /// </summary>
        /// <param name="connectionHandler">the HTTP/2 connection handler</param>
        public Http2ClientUpgradeCodec(Http2ConnectionHandler connectionHandler)
            : this(null, connectionHandler)
        {
        }

        public Http2ClientUpgradeCodec(Http2ConnectionHandler connectionHandler, Http2MultiplexHandler http2MultiplexHandler)
            : this((string)null, connectionHandler, http2MultiplexHandler)
        {
        }

        /// <summary>
        /// Creates the codec providing an upgrade to the given handler for HTTP/2.
        /// </summary>
        /// <param name="handlerName">the name of the HTTP/2 connection handler to be used in the pipeline,
        /// or <c>null</c> to auto-generate the name</param>
        /// <param name="connectionHandler">the HTTP/2 connection handler</param>
        public Http2ClientUpgradeCodec(string handlerName, Http2ConnectionHandler connectionHandler)
            : this(handlerName, connectionHandler, connectionHandler, null)
        {
        }

        /// <summary>
        /// Creates the codec providing an upgrade to the given handler for HTTP/2.
        /// </summary>
        /// <param name="handlerName">the name of the HTTP/2 connection handler to be used in the pipeline,
        /// or <c>null</c> to auto-generate the name</param>
        /// <param name="connectionHandler">the HTTP/2 connection handler</param>
        /// <param name="http2MultiplexHandler"></param>
        public Http2ClientUpgradeCodec(string handlerName, Http2ConnectionHandler connectionHandler, Http2MultiplexHandler http2MultiplexHandler)
            : this(handlerName, connectionHandler, connectionHandler, http2MultiplexHandler)
        {
        }

        private Http2ClientUpgradeCodec(string handlerName, Http2ConnectionHandler connectionHandler,
            IChannelHandler upgradeToHandler, Http2MultiplexHandler http2MultiplexHandler)
        {
            if (connectionHandler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connectionHandler); }
            if (upgradeToHandler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.upgradeToHandler); }

            _handlerName = handlerName;
            _connectionHandler = connectionHandler;
            _upgradeToHandler = upgradeToHandler;
            _http2MultiplexHandler = http2MultiplexHandler;
        }

        public ICharSequence Protocol => Http2CodecUtil.HttpUpgradeProtocolName;

        public ICollection<ICharSequence> SetUpgradeHeaders(IChannelHandlerContext ctx, IHttpRequest upgradeRequest)
        {
            var settingsValue = GetSettingsHeaderValue(ctx);
            upgradeRequest.Headers.Set(Http2CodecUtil.HttpUpgradeSettingsHeader, settingsValue);
            return s_upgradeHeaders;
        }

        public void UpgradeTo(IChannelHandlerContext ctx, IFullHttpResponse upgradeResponse)
        {
            try
            {
                // Add the handler to the pipeline.
                ctx.Pipeline.AddAfter(ctx.Name, _handlerName, _upgradeToHandler);

                // Add the Http2 Multiplex handler as this handler handle events produced by the connectionHandler.
                // See https://github.com/netty/netty/issues/9495
                if (_http2MultiplexHandler is object)
                {
                    var name = ctx.Pipeline.Context(_connectionHandler).Name;
                    ctx.Pipeline.AddAfter(name, null, _http2MultiplexHandler);
                }

                // Reserve local stream 1 for the response.
                _connectionHandler.OnHttpClientUpgrade();
            }
            catch (Http2Exception e)
            {
                ctx.FireExceptionCaught(e);
                ctx.CloseAsync();
            }
        }

        /// <summary>
        /// Converts the current settings for the handler to the Base64-encoded representation used in
        /// the HTTP2-Settings upgrade header.
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        private ICharSequence GetSettingsHeaderValue(IChannelHandlerContext ctx)
        {
            IByteBuffer buf = null;
            IByteBuffer encodedBuf = null;
            try
            {
                // Get the local settings for the handler.
                Http2Settings settings = _connectionHandler.Decoder.LocalSettings;

                // Serialize the payload of the SETTINGS frame.
                int payloadLength = Http2CodecUtil.SettingEntryLength * settings.Count;
                buf = ctx.Allocator.Buffer(payloadLength);
                foreach (var entry in settings)
                {
                    buf.WriteChar(entry.Key);
                    buf.WriteInt((int)entry.Value);
                }

                // Base64 encode the payload and then convert to a string for the header.
                encodedBuf = Base64.Encode(buf, Base64Dialect.URL_SAFE);
                return new StringCharSequence(encodedBuf.ToString(Encoding.UTF8));
            }
            finally
            {
                ReferenceCountUtil.Release(buf);
                ReferenceCountUtil.Release(encodedBuf);
            }
        }
    }
}
