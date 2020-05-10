// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;

    public class DatagramPacketEncoder<T> : MessageToMessageEncoder<IAddressedEnvelope<T>>
    {
        readonly MessageToMessageEncoder<T> encoder;

        public DatagramPacketEncoder(MessageToMessageEncoder<T> encoder)
        {
            if (null == encoder) { CThrowHelper.ThrowArgumentNullException(CExceptionArgument.encoder); }

            this.encoder = encoder;
        }

        public override bool AcceptOutboundMessage(object msg)
        {
            var envelope = msg as IAddressedEnvelope<T>;
            return envelope is object 
                && this.encoder.AcceptOutboundMessage(envelope.Content) 
                && (envelope.Sender is object || envelope.Recipient is object);
        }

        protected internal override void Encode(IChannelHandlerContext context, IAddressedEnvelope<T> message, List<object> output)
        {
            this.encoder.Encode(context, message.Content, output);
            if (output.Count != 1) {
                CThrowHelper.ThrowEncoderException_MustProduceOnlyOneMsg(this.encoder.GetType());
            }

            var content = output[0] as IByteBuffer;
            if (content == null)
            {
                CThrowHelper.ThrowEncoderException_MustProduceOnlyByteBuf(this.encoder.GetType());
            }

            // Replace the ByteBuf with a DatagramPacket.
            output[0] = new DatagramPacket(content, message.Sender, message.Recipient);
        }

        public override Task BindAsync(IChannelHandlerContext context, EndPoint localAddress) => 
            this.encoder.BindAsync(context, localAddress);

        public override Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress) => 
            this.encoder.ConnectAsync(context, remoteAddress, localAddress);

        public override void Disconnect(IChannelHandlerContext context, IPromise promise) => this.encoder.Disconnect(context, promise);

        public override void Close(IChannelHandlerContext context, IPromise promise) => this.encoder.Close(context, promise);

        public override void Deregister(IChannelHandlerContext context, IPromise promise) => this.encoder.Deregister(context, promise);

        public override void Read(IChannelHandlerContext context) => this.encoder.Read(context);

        public override void Flush(IChannelHandlerContext context) => this.encoder.Flush(context);

        public override void HandlerAdded(IChannelHandlerContext context) => this.encoder.HandlerAdded(context);

        public override void HandlerRemoved(IChannelHandlerContext context) => this.encoder.HandlerRemoved(context);

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) => 
            this.encoder.ExceptionCaught(context, exception);
    }
}
