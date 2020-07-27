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
            _ = upgradeRequest.Headers.Set(Http2CodecUtil.HttpUpgradeSettingsHeader, settingsValue);
            return s_upgradeHeaders;
        }

        public void UpgradeTo(IChannelHandlerContext ctx, IFullHttpResponse upgradeResponse)
        {
            try
            {
                // Add the handler to the pipeline.
                _ = ctx.Pipeline.AddAfter(ctx.Name, _handlerName, _upgradeToHandler);

                // Add the Http2 Multiplex handler as this handler handle events produced by the connectionHandler.
                // See https://github.com/netty/netty/issues/9495
                if (_http2MultiplexHandler is object)
                {
                    var name = ctx.Pipeline.Context(_connectionHandler).Name;
                    _ = ctx.Pipeline.AddAfter(name, null, _http2MultiplexHandler);
                }

                // Reserve local stream 1 for the response.
                _connectionHandler.OnHttpClientUpgrade();
            }
            catch (Http2Exception e)
            {
                _ = ctx.FireExceptionCaught(e);
                _ = ctx.CloseAsync();
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
                    _ = buf.WriteChar(entry.Key);
                    _ = buf.WriteInt((int)entry.Value);
                }

                // Base64 encode the payload and then convert to a string for the header.
                encodedBuf = Base64.Encode(buf, Base64Dialect.UrlSafe);
                return new StringCharSequence(encodedBuf.ToString(Encoding.UTF8));
            }
            finally
            {
                _ = ReferenceCountUtil.Release(buf);
                _ = ReferenceCountUtil.Release(encodedBuf);
            }
        }
    }
}
