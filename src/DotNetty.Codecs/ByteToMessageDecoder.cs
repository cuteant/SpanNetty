// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;

    public abstract partial class ByteToMessageDecoder : ChannelHandlerAdapter
    {
        public delegate IByteBuffer CumulationFunc(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer input);

        /// <summary>
        ///     Cumulates instances of <see cref="IByteBuffer" /> by merging them into one <see cref="IByteBuffer" />, using memory
        ///     copies.
        /// </summary>
        public static readonly CumulationFunc MergeCumulator = MergeCumulatorInternal;
        private static IByteBuffer MergeCumulatorInternal(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer input)
        {
            IByteBuffer buffer;
            if (cumulation.WriterIndex > cumulation.MaxCapacity - input.ReadableBytes || cumulation.ReferenceCount > 1)
            {
                // Expand cumulation (by replace it) when either there is not more room in the buffer
                // or if the refCnt is greater then 1 which may happen when the user use Slice().Retain() or
                // Duplicate().Retain().
                //
                // See:
                // - https://github.com/netty/netty/issues/2327
                // - https://github.com/netty/netty/issues/1764
                buffer = ExpandCumulation(alloc, cumulation, input.ReadableBytes);
            }
            else
            {
                buffer = cumulation;
            }
            buffer.WriteBytes(input);
            input.Release();
            return buffer;
        }

        /// <summary>
        ///     Cumulate instances of <see cref="IByteBuffer" /> by add them to a <see cref="CompositeByteBuffer" /> and therefore
        ///     avoiding memory copy when possible.
        /// </summary>
        /// <remarks>
        ///     Be aware that <see cref="CompositeByteBuffer" /> use a more complex indexing implementation so depending on your
        ///     use-case
        ///     and the decoder implementation this may be slower then just use the <see cref="MergeCumulator" />.
        /// </remarks>
        public static readonly CumulationFunc CompositionCumulation = CompositionCumulationInternal;
        private static IByteBuffer CompositionCumulationInternal(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer input)
        {
            IByteBuffer buffer;
            if (cumulation.ReferenceCount > 1)
            {
                // Expand cumulation (by replace it) when the refCnt is greater then 1 which may happen when the user
                // use slice().retain() or duplicate().retain().
                //
                // See:
                // - https://github.com/netty/netty/issues/2327
                // - https://github.com/netty/netty/issues/1764
                buffer = ExpandCumulation(alloc, cumulation, input.ReadableBytes);
                buffer.WriteBytes(input);
                input.Release();
            }
            else
            {
                CompositeByteBuffer composite;
                if (cumulation is CompositeByteBuffer asComposite)
                {
                    composite = asComposite;
                }
                else
                {
                    composite = alloc.CompositeBuffer(int.MaxValue);
                    composite.AddComponent(true, cumulation);
                }
                composite.AddComponent(true, input);
                buffer = composite;
            }
            return buffer;
        }

        const byte STATE_INIT = 0;
        const byte STATE_CALLING_CHILD_DECODE = 1;
        const byte STATE_HANDLER_REMOVED_PENDING = 2;

        IByteBuffer cumulation;
        CumulationFunc cumulator = MergeCumulator;
        bool decodeWasNull;
        bool first;
        /**
         * A bitmask where the bits are defined as
         * <ul>
         *     <li>{@link #STATE_INIT}</li>
         *     <li>{@link #STATE_CALLING_CHILD_DECODE}</li>
         *     <li>{@link #STATE_HANDLER_REMOVED_PENDING}</li>
         * </ul>
         */
        byte decodeState = STATE_INIT;
        int discardAfterReads = 16;
        int numReads;

        protected ByteToMessageDecoder()
        {
            // ReSharper disable once DoNotCallOverridableMethodsInConstructor -- used for safety check only
            if (this.IsSharable)
            {
                ThrowHelper.ThrowInvalidOperationException_ByteToMessageDecoder();
            }
        }

        /// <summary>
        ///     Determines whether only one message should be decoded per <see cref="ChannelRead" /> call.
        ///     Default is <code>false</code> as this has performance impacts.
        /// </summary>
        /// <remarks>Is particularly useful in support of protocol upgrade scenarios.</remarks>
        public bool SingleDecode { get; set; }

        public void SetCumulator(CumulationFunc cumulationFunc)
        {
            if (null == cumulationFunc) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.cumulationFunc); }

            this.cumulator = cumulationFunc;
        }

        /**
         * Set the number of reads after which {@link ByteBuf#discardSomeReadBytes()} are called and so free up memory.
         * The default is {@code 16}.
         */
        public void SetDiscardAfterReads(int discardAfterReads)
        {
            if (discardAfterReads <= 0)
            {
                ThrowHelper.ThrowArgumentException_DiscardAfterReads();
            }
            this.discardAfterReads = discardAfterReads;
        }

        /// <summary>
        ///     Returns the actual number of readable bytes in the internal cumulative
        ///     buffer of this decoder. You usually do not need to rely on this value
        ///     to write a decoder. Use it only when you must use it at your own risk.
        ///     This method is a shortcut to <see cref="IByteBuffer.ReadableBytes" /> of <see cref="InternalBuffer" />.
        /// </summary>
        protected int ActualReadableBytes => this.InternalBuffer.ReadableBytes;

        protected IByteBuffer InternalBuffer
        {
            get
            {
                if (this.cumulation != null)
                {
                    return this.cumulation;
                }
                else
                {
                    return Unpooled.Empty;
                }
            }
        }

        public override void HandlerRemoved(IChannelHandlerContext context)
        {
            if (this.decodeState == STATE_CALLING_CHILD_DECODE)
            {
                this.decodeState = STATE_HANDLER_REMOVED_PENDING;
                return;
            }
            IByteBuffer buf = this.cumulation;
            if (buf != null)
            {
                // Directly set this to null so we are sure we not access it in any other method here anymore.
                this.cumulation = null;

                int readable = buf.ReadableBytes;
                if (readable > 0)
                {
                    IByteBuffer bytes = buf.ReadBytes(readable);
                    buf.Release();
                    context.FireChannelRead(bytes);
                }
                else
                {
                    buf.Release();
                }

                this.numReads = 0;
                context.FireChannelReadComplete();
            }
            this.HandlerRemovedInternal(context);
        }

        protected virtual void HandlerRemovedInternal(IChannelHandlerContext context)
        {
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (message is IByteBuffer data)
            {
                ThreadLocalObjectList output = ThreadLocalObjectList.NewInstance();
                try
                {
                    this.first = this.cumulation == null;
                    if (this.first)
                    {
                        this.cumulation = data;
                    }
                    else
                    {
                        this.cumulation = this.cumulator(context.Allocator, this.cumulation, data);
                    }
                    this.CallDecode(context, this.cumulation, output);
                }
                catch (DecoderException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    ThrowHelper.ThrowDecoderException(ex);
                }
                finally
                {
                    if (this.cumulation != null && !this.cumulation.IsReadable())
                    {
                        this.numReads = 0;
                        this.cumulation.Release();
                        this.cumulation = null;
                    }
                    else if (++this.numReads >= this.discardAfterReads)
                    {
                        // We did enough reads already try to discard some bytes so we not risk to see a OOME.
                        // See https://github.com/netty/netty/issues/4275
                        this.numReads = 0;
                        this.DiscardSomeReadBytes();
                    }

                    int size = output.Count;
                    this.decodeWasNull = size == 0;

                    for (int i = 0; i < size; i++)
                    {
                        context.FireChannelRead(output[i]);
                    }
                    output.Return();
                }
            }
            else
            {
                context.FireChannelRead(message);
            }
        }

        /**
         * Get {@code numElements} out of the {@link CodecOutputList} and forward these through the pipeline.
         */
        protected static void FireChannelRead(IChannelHandlerContext ctx, List<object> output, int numElements)
        {
            for (int i = 0; i < numElements; i++)
            {
                ctx.FireChannelRead(output[i]);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            this.numReads = 0;
            this.DiscardSomeReadBytes();
            if (this.decodeWasNull)
            {
                this.decodeWasNull = false;
                if (!context.Channel.Configuration.AutoRead)
                {
                    context.Read();
                }
            }
            context.FireChannelReadComplete();
        }

        protected void DiscardSomeReadBytes()
        {
            if (this.cumulation != null && !this.first && this.cumulation.ReferenceCount == 1)
            {
                // discard some bytes if possible to make more room input the
                // buffer but only if the refCnt == 1  as otherwise the user may have
                // used slice().retain() or duplicate().retain().
                //
                // See:
                // - https://github.com/netty/netty/issues/2327
                // - https://github.com/netty/netty/issues/1764
                this.cumulation.DiscardSomeReadBytes();
            }
        }

        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            this.ChannelInputClosed(ctx, true);
        }

        public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
        {
            if(evt is ChannelInputShutdownEvent)
            {
                // The decodeLast method is invoked when a channelInactive event is encountered.
                // This method is responsible for ending requests in some situations and must be called
                // when the input has been shutdown.
                this.ChannelInputClosed(ctx, false);
            }
            ctx.FireUserEventTriggered(evt);
        }

        private void ChannelInputClosed(IChannelHandlerContext ctx, bool callChannelInactive)
        {
            ThreadLocalObjectList output = ThreadLocalObjectList.NewInstance();
            try
            {
                //this.ChannelInputClosed(ctx, output);
                if (this.cumulation != null)
                {
                    this.CallDecode(ctx, this.cumulation, output);
                    this.DecodeLast(ctx, this.cumulation, output);
                }
                else
                {
                    this.DecodeLast(ctx, Unpooled.Empty, output);
                }
            }
            catch (DecoderException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                ThrowHelper.ThrowDecoderException(e);
            }
            finally
            {
                try
                {
                    if (this.cumulation != null)
                    {
                        this.cumulation.Release();
                        this.cumulation = null;
                    }
                    int size = output.Count;
                    for (int i = 0; i < size; i++)
                    {
                        ctx.FireChannelRead(output[i]);
                    }
                    if (size > 0)
                    {
                        // Something was read, call fireChannelReadComplete()
                        ctx.FireChannelReadComplete();
                    }
                    if (callChannelInactive)
                    {
                        ctx.FireChannelInactive();
                    }
                }
                finally
                {
                    // Recycle in all cases
                    output.Return();
                }
            }
        }

        ///**
        // * Called when the input of the channel was closed which may be because it changed to inactive or because of
        // * {@link ChannelInputShutdownEvent}.
        // */
        //protected virtual void ChannelInputClosed(IChannelHandlerContext ctx, List<object> output)
        //{
        //    if (this.cumulation != null)
        //    {
        //        this.CallDecode(ctx, this.cumulation, output);
        //        this.DecodeLast(ctx, this.cumulation, output);
        //    }
        //    else
        //    {
        //        this.DecodeLast(ctx, Unpooled.Empty, output);
        //    }
        //}

        protected virtual void CallDecode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            if (null == context) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.context); }
            if (null == input) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.input); }
            if (null == output) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.output); }

            try
            {
                while (input.IsReadable())
                {
                    int initialOutputCount = output.Count;
                    if (initialOutputCount > 0)
                    {
                        for (int i = 0; i < initialOutputCount; i++)
                        {
                            context.FireChannelRead(output[i]);
                        }
                        output.Clear();

                        // Check if this handler was removed before continuing with decoding.
                        // If it was removed, it is not safe to continue to operate on the buffer.
                        //
                        // See:
                        // - https://github.com/netty/netty/issues/4635
                        if (context.Removed)
                        {
                            break;
                        }
                        initialOutputCount = 0;
                    }

                    int oldInputLength = input.ReadableBytes;
                    this.DecodeRemovalReentryProtection(context, input, output);

                    // Check if this handler was removed before continuing the loop.
                    // If it was removed, it is not safe to continue to operate on the buffer.
                    //
                    // See https://github.com/netty/netty/issues/1664
                    if (context.Removed)
                    {
                        break;
                    }

                    if (initialOutputCount == output.Count)
                    {
                        // no outgoing messages have been produced

                        if (oldInputLength == input.ReadableBytes)
                        {
                            break;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (oldInputLength == input.ReadableBytes)
                    {
                        ThrowHelper.ThrowDecoderException_ByteToMessageDecoder(this.GetType());
                    }

                    if (this.SingleDecode)
                    {
                        break;
                    }
                }
            }
            catch (DecoderException)
            {
                throw;
            }
            catch (Exception cause)
            {
                ThrowHelper.ThrowDecoderException(cause);
            }
        }

        protected internal abstract void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output);

        /**
         * Decode the from one {@link ByteBuf} to an other. This method will be called till either the input
         * {@link ByteBuf} has nothing to read when return from this method or till nothing was read from the input
         * {@link ByteBuf}.
         *
         * @param ctx           the {@link ChannelHandlerContext} which this {@link ByteToMessageDecoder} belongs to
         * @param in            the {@link ByteBuf} from which to read data
         * @param out           the {@link List} to which decoded messages should be added
         * @throws Exception    is thrown if an error occurs
         */
        protected void DecodeRemovalReentryProtection(IChannelHandlerContext ctx, IByteBuffer input, List<object> output)
        {
            this.decodeState = STATE_CALLING_CHILD_DECODE;
            try
            {
                this.Decode(ctx, input, output);
            }
            finally
            {
                var removePending = this.decodeState == STATE_HANDLER_REMOVED_PENDING;
                this.decodeState = STATE_INIT;
                if (removePending)
                {
                    this.HandlerRemoved(ctx);
                }
            }
        }

        protected virtual void DecodeLast(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            if (input.IsReadable())
            {
                // Only call decode() if there is something left in the buffer to decode.
                // See https://github.com/netty/netty/issues/4386
                this.Decode(context, input, output);
            }
        }

        static IByteBuffer ExpandCumulation(IByteBufferAllocator allocator, IByteBuffer cumulation, int readable)
        {
            IByteBuffer oldCumulation = cumulation;
            cumulation = allocator.Buffer(oldCumulation.ReadableBytes + readable);
            cumulation.WriteBytes(oldCumulation);
            oldCumulation.Release();
            return cumulation;
        }
    }
}