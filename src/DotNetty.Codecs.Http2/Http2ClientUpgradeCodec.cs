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
        private static readonly ICharSequence[] UPGRADE_HEADERS =
            new[] { Http2CodecUtil.HttpUpgradeSettingsHeader };

        private readonly string handlerName;
        private readonly Http2ConnectionHandler connectionHandler;
        private readonly IChannelHandler upgradeToHandler;

        public Http2ClientUpgradeCodec(Http2FrameCodec frameCodec, IChannelHandler upgradeToHandler)
            : this(null, frameCodec, upgradeToHandler)
        {
        }

        public Http2ClientUpgradeCodec(string handlerName, Http2FrameCodec frameCodec, IChannelHandler upgradeToHandler)
            : this(handlerName, (Http2ConnectionHandler)frameCodec, upgradeToHandler)
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

        /// <summary>
        /// Creates the codec providing an upgrade to the given handler for HTTP/2.
        /// </summary>
        /// <param name="handlerName">the name of the HTTP/2 connection handler to be used in the pipeline,
        /// or <c>null</c> to auto-generate the name</param>
        /// <param name="connectionHandler">the HTTP/2 connection handler</param>
        public Http2ClientUpgradeCodec(string handlerName, Http2ConnectionHandler connectionHandler)
            : this(handlerName, connectionHandler, connectionHandler)
        {
        }

        private Http2ClientUpgradeCodec(string handlerName, Http2ConnectionHandler connectionHandler, IChannelHandler upgradeToHandler)
        {
            if (connectionHandler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connectionHandler); }
            if (upgradeToHandler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.upgradeToHandler); }

            this.handlerName = handlerName;
            this.connectionHandler = connectionHandler;
            this.upgradeToHandler = upgradeToHandler;
        }

        public ICharSequence Protocol => Http2CodecUtil.HttpUpgradeProtocolName;

        public ICollection<ICharSequence> SetUpgradeHeaders(IChannelHandlerContext ctx, IHttpRequest upgradeRequest)
        {
            var settingsValue = this.GetSettingsHeaderValue(ctx);
            upgradeRequest.Headers.Set(Http2CodecUtil.HttpUpgradeSettingsHeader, settingsValue);
            return UPGRADE_HEADERS;
        }

        public void UpgradeTo(IChannelHandlerContext ctx, IFullHttpResponse upgradeResponse)
        {
            // Add the handler to the pipeline.
            ctx.Pipeline.AddAfter(ctx.Name, handlerName, upgradeToHandler);

            // Reserve local stream 1 for the response.
            connectionHandler.OnHttpClientUpgrade();
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
                Http2Settings settings = this.connectionHandler.Decoder.LocalSettings;

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
