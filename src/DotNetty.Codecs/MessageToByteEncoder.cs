// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// <see cref="ChannelHandlerAdapter"/> which encodes message in a stream-like fashion from one message to an
    /// <see cref="IByteBuffer"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class MessageToByteEncoder<T> : ChannelHandlerAdapter
    {
        /// <summary>
        /// Returns <c>true</c> if the given message should be handled. If <c>false</c> it will be passed to the next
        /// <see cref="IChannelHandler"/> in the <see cref="IChannelPipeline"/>.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public virtual bool AcceptOutboundMessage(object message) => message is T;

        /// <inheritdoc />
        public override void Write(IChannelHandlerContext context, object message, IPromise promise)
        {
            if (context is null) { CThrowHelper.ThrowArgumentNullException(CExceptionArgument.context); }

            IByteBuffer buffer = null;
            try
            {
                if (this.AcceptOutboundMessage(message))
                {
                    var input = (T)message;
                    buffer = this.AllocateBuffer(context);
                    try
                    {
                        this.Encode(context, input, buffer);
                    }
                    finally
                    {
                        ReferenceCountUtil.Release(input);
                    }

                    if (buffer.IsReadable())
                    {
                        context.WriteAsync(buffer, promise);
                    }
                    else
                    {
                        buffer.Release();
                        context.WriteAsync(Unpooled.Empty, promise);
                    }

                    buffer = null;
                }
                else
                {
                    context.WriteAsync(message, promise);
                }
            }
            catch (EncoderException)
            {
                throw;
            }
            catch (Exception ex)
            {
                CThrowHelper.ThrowEncoderException(ex);
            }
            finally
            {
                buffer?.Release();
            }
        }

        /// <summary>
        /// Allocate a <see cref="IByteBuffer"/> which will be used as argument of <see cref="Encode(IChannelHandlerContext, T, IByteBuffer)"/>.
        /// Sub-classes may override this method to return <see cref="IByteBuffer"/> with a perfect matching <c>initialCapacity</c>.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected virtual IByteBuffer AllocateBuffer(IChannelHandlerContext context)
        {
            if (context is null) { CThrowHelper.ThrowArgumentNullException(CExceptionArgument.context); }

            return context.Allocator.Buffer();
        }

        /// <summary>
        /// Encode a message into a <see cref="IByteBuffer"/>. This method will be called for each written message that can be handled
        /// by this encoder.
        /// </summary>
        /// <param name="context">the <see cref="IChannelHandlerContext"/> which this <see cref="MessageToByteEncoder{T}"/> belongs to</param>
        /// <param name="message">the message to encode</param>
        /// <param name="output">the <see cref="IByteBuffer"/> into which the encoded message will be written</param>
        protected abstract void Encode(IChannelHandlerContext context, T message, IByteBuffer output);
    }
}
