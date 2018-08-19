// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    public abstract class MessageToMessageCodec2<TInbound, TOutbound> : ChannelDuplexHandler
        where TInbound : class
        where TOutbound : class
    {
        readonly Encoder encoder;
        readonly Decoder decoder;

        sealed class Encoder : MessageToMessageEncoder2<TOutbound>
        {
            readonly MessageToMessageCodec2<TInbound, TOutbound> codec;

            public Encoder(MessageToMessageCodec2<TInbound, TOutbound> codec)
            {
                this.codec = codec;
            }

            public override bool TryAcceptOutboundMessage(object msg, out TOutbound cast)
                => this.codec.TryAcceptOutboundMessage(msg, out cast);

            protected internal override void Encode(IChannelHandlerContext context, TOutbound message, List<object> output)
                => this.codec.Encode(context, message, output);
        }

        sealed class Decoder : MessageToMessageDecoder2<TInbound>
        {
            readonly MessageToMessageCodec2<TInbound, TOutbound> codec;

            public Decoder(MessageToMessageCodec2<TInbound, TOutbound> codec)
            {
                this.codec = codec;
            }

            public override bool TryAcceptInboundMessage(object msg, out TInbound cast)
                => this.codec.TryAcceptInboundMessage(msg, out cast);

            protected internal override void Decode(IChannelHandlerContext context, TInbound message, List<object> output)
                => this.codec.Decode(context, message, output);
        }

        protected MessageToMessageCodec2()
        {
            this.encoder = new Encoder(this);
            this.decoder = new Decoder(this);
        }

        public sealed override void ChannelRead(IChannelHandlerContext context, object message)
            => this.decoder.ChannelRead(context, message);

        public sealed override Task WriteAsync(IChannelHandlerContext context, object message)
            => this.encoder.WriteAsync(context, message);

        public virtual bool TryAcceptInboundMessage(object msg, out TInbound cast)
        {
            cast = msg as TInbound;
            return cast != null;
        }

        public virtual bool TryAcceptOutboundMessage(object message, out TOutbound cast)
        {
            cast = message as TOutbound;
            return cast != null;
        }

        protected abstract void Encode(IChannelHandlerContext ctx, TOutbound msg, List<object> output);

        protected abstract void Decode(IChannelHandlerContext ctx, TInbound msg, List<object> output);
    }
}
