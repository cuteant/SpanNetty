// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// <see cref="ChannelHandlerAdapter"/> which decodes from one message to an other message.
    /// </summary>
    public abstract class MessageToMessageDecoder<T> : ChannelHandlerAdapter
    {
        public virtual bool AcceptInboundMessage(object msg) => msg is T;

        /// <inheritdoc />
        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            ThreadLocalObjectList output = ThreadLocalObjectList.NewInstance();

            try
            {
                if (this.AcceptInboundMessage(message))
                {
                    var cast = (T)message;
                    try
                    {
                        this.Decode(context, cast, output);
                    }
                    finally
                    {
                        _ = ReferenceCountUtil.Release(cast);
                    }
                }
                else
                {
                    output.Add(message);
                }
            }
            catch (DecoderException)
            {
                throw;
            }
            catch (Exception e)
            {
                CThrowHelper.ThrowDecoderException(e);
            }
            finally
            {
                try
                {
                    int size = output.Count;
                    for (int i = 0; i < size; i++)
                    {
                        _ = context.FireChannelRead(output[i]);
                    }
                }
                finally
                {
                    output.Return();
                }
            }
        }

        /// <summary>
        /// Decode from one message to an other. This method will be called for each written message that can be handled
        /// by this decoder.
        /// </summary>
        /// <param name="context">the <see cref="IChannelHandlerContext"/> which this <see cref="MessageToMessageDecoder{T}"/> belongs to</param>
        /// <param name="message">the message to decode to an other one</param>
        /// <param name="output">the <see cref="List{Object}"/> to which decoded messages should be added</param>
        protected internal abstract void Decode(IChannelHandlerContext context, T message, List<object> output);
    }
}