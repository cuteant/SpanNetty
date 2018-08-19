// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public abstract class MessageToByteEncoder2<T> : ChannelHandlerAdapter
        where T : class
    {
        public virtual bool TryAcceptOutboundMessage(object message, out T input)
        {
            input = message as T;
            return input != null;
        }

        public override Task WriteAsync(IChannelHandlerContext context, object message)
        {
            Contract.Requires(context != null);

            IByteBuffer buffer = null;
            Task result;
            try
            {
                if (this.TryAcceptOutboundMessage(message, out T input))
                {
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
                        result = context.WriteAsync(buffer);
                    }
                    else
                    {
                        buffer.Release();
                        result = context.WriteAsync(Unpooled.Empty);
                    }

                    buffer = null;
                }
                else
                {
                    return context.WriteAsync(message);
                }
            }
            catch (EncoderException e)
            {
                return TaskUtil.FromException(e);
            }
            catch (Exception ex)
            {
                return ThrowHelper.ThrowEncoderException(ex);
            }
            finally
            {
                buffer?.Release();
            }

            return result;
        }

        protected virtual IByteBuffer AllocateBuffer(IChannelHandlerContext context)
        {
            Contract.Requires(context != null);

            return context.Allocator.Buffer();
        }

        protected abstract void Encode(IChannelHandlerContext context, T message, IByteBuffer output);
    }
}
