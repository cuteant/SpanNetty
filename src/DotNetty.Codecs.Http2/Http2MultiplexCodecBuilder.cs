// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// A builder for <see cref="Http2MultiplexCodec"/>.
    /// </summary>
    public class Http2MultiplexCodecBuilder : AbstractHttp2ConnectionHandlerBuilder<Http2MultiplexCodec, Http2MultiplexCodecBuilder>
    {
        internal readonly IChannelHandler childHandler;
        IChannelHandler upgradeStreamHandler;

        public Http2MultiplexCodecBuilder(bool server, IChannelHandler childHandler)
        {
            if (childHandler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.childHandler); }
            this.IsServer = server;
            this.childHandler = CheckSharable(childHandler);
        }

        private static IChannelHandler CheckSharable(IChannelHandler handler)
        {
            if (handler is ChannelHandlerAdapter handlerAdapter && !handlerAdapter.IsSharable)
            {
                ThrowHelper.ThrowArgumentException_TheHandlerMustBeSharable();
            }
            return handler;
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
            if (this.IsServer)
            {
                ThrowHelper.ThrowArgumentException_ServerCodecsDonotUseAnExtraHandlerForTheUpgradeStream();
            }
            this.upgradeStreamHandler = upgradeStreamHandler;
            return this;
        }

        protected override Http2MultiplexCodec Build(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings)
        {
            return new Http2MultiplexCodec(encoder, decoder, initialSettings, childHandler, upgradeStreamHandler);
        }
    }
}
