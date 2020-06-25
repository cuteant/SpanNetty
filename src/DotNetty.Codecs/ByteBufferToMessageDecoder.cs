// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// <see cref="ChannelHandlerAdapter"/> which decodes from one <see cref="IByteBuffer"/> to an other message.
    /// </summary>
    public abstract class ByteBufferToMessageDecoder : ChannelHandlerAdapter
    {
        /// <inheritdoc />
        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            ThreadLocalObjectList output = ThreadLocalObjectList.NewInstance();

            try
            {
                if (message is IByteBuffer byteBuffer)
                {
                    try
                    {
                        var reader = new ByteBufferReader(byteBuffer);
                        Decode(context, ref reader, output);
                    }
                    finally
                    {
                        _ = ReferenceCountUtil.Release(message);
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
        /// <param name="context">the <see cref="IChannelHandlerContext"/> which this <see cref="ByteBufferToMessageDecoder"/> belongs to</param>
        /// <param name="reader"></param>
        /// <param name="output">the <see cref="List{Object}"/> to which decoded messages should be added</param>
        protected abstract void Decode(IChannelHandlerContext context, ref ByteBufferReader reader, List<object> output);
    }
}