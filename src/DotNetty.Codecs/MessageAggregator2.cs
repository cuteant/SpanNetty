// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <inheritdoc />
    /// <summary>
    /// An abstract <see cref="T:DotNetty.Transport.Channels.IChannelHandler" /> that aggregates a series of message objects 
    /// into a single aggregated message.
    /// 'A series of messages' is composed of the following:
    /// a single start message which optionally contains the first part of the content, and
    /// 1 or more content messages. The content of the aggregated message will be the merged 
    /// content of the start message and its following content messages. If this aggregator 
    /// encounters a content message where { @link #isLastContentMessage(ByteBufHolder)}
    /// return true for, the aggregator will finish the aggregation and produce the aggregated 
    /// message and expect another start message.
    /// </summary>
    /// <typeparam name="TMessage">The type that covers both start message and content message</typeparam>
    /// <typeparam name="TStart">The type of the start message</typeparam>
    /// <typeparam name="TContent">The type of the content message</typeparam>
    /// <typeparam name="TOutput">The type of the aggregated message</typeparam>
    public abstract class MessageAggregator2<TMessage, TStart, TContent, TOutput> : MessageToMessageDecoder2<TMessage>
        where TMessage : class
        where TContent : IByteBufferHolder
        where TOutput : IByteBufferHolder
    {
        const int DefaultMaxCompositebufferComponents = 1024;

        protected int maxCumulationBufferComponents = DefaultMaxCompositebufferComponents;

        protected TOutput currentMessage;
        protected bool handlingOversizedMessage;

        IChannelHandlerContext handlerContext;

        protected MessageAggregator2(int maxContentLength)
        {
            ValidateMaxContentLength(maxContentLength);
            this.MaxContentLength = maxContentLength;
        }

        static void ValidateMaxContentLength(int maxContentLength)
        {
            if (maxContentLength < 0)
            {
                CThrowHelper.ThrowArgumentException_MaxContentLength(maxContentLength);
            }
        }

        public override bool TryAcceptInboundMessage(object msg, out TMessage message)
        {
            // No need to match last and full types because they are subset of first and middle types.
            if (!base.TryAcceptInboundMessage(msg, out message))
            {
                return false;
            }

            return (this.IsContentMessage(message) || this.IsStartMessage(message))
                && !this.IsAggregated(message);
        }

        protected abstract bool IsStartMessage(TMessage msg);

        protected abstract bool IsContentMessage(TMessage msg);

        protected abstract bool IsLastContentMessage(TContent msg);

        protected abstract bool IsAggregated(TMessage msg);

        public int MaxContentLength { get; }

        public int MaxCumulationBufferComponents
        {
            get => this.maxCumulationBufferComponents;
            set
            {
                if (value < 2)
                {
                    CThrowHelper.ThrowArgumentException_MaxCumulationBufferComponents(value);
                }
                if (this.handlerContext is object)
                {
                    CThrowHelper.ThrowInvalidOperationException_DecoderProperties();
                }

                this.maxCumulationBufferComponents = value;
            }
        }

        protected IChannelHandlerContext HandlerContext()
        {
            if (this.handlerContext == null)
            {
                CThrowHelper.ThrowInvalidOperationException_NotAddedToAPipelineYet();
            }

            return this.handlerContext;
        }

        protected internal override void Decode(IChannelHandlerContext context, TMessage message, List<object> output)
        {
            if (this.IsStartMessage(message))
            {
                this.handlingOversizedMessage = false;
                if (this.currentMessage is object)
                {
                    this.currentMessage.Release();
                    this.currentMessage = default;

                    CThrowHelper.ThrowMessageAggregationException_StartMessage();
                }

                var m = As<TStart>(message);
                //Debug.Assert(m is object);

                // Send the continue response if necessary(e.g. 'Expect: 100-continue' header)
                // Check before content length. Failing an expectation may result in a different response being sent.
                object continueResponse = this.NewContinueResponse(m, this.MaxContentLength, context.Pipeline);
                if (continueResponse is object)
                {
                    // Make sure to call this before writing, otherwise reference counts may be invalid.
                    bool closeAfterWrite = this.CloseAfterContinueResponse(continueResponse);
                    this.handlingOversizedMessage = this.IgnoreContentAfterContinueResponse(continueResponse);

                    Task task = context
                        .WriteAndFlushAsync(continueResponse)
#if NET40
                        .ContinueWith(t => s_continueResponseWriteAction(t, context), TaskContinuationOptions.ExecuteSynchronously);
#else
                        .ContinueWith(s_continueResponseWriteAction, context, TaskContinuationOptions.ExecuteSynchronously);
#endif

                    if (closeAfterWrite)
                    {
#if NET40
                        task.ContinueWith(t => s_closeAfterWriteAction(t, context), TaskContinuationOptions.ExecuteSynchronously);
#else
                        task.ContinueWith(s_closeAfterWriteAction, context, TaskContinuationOptions.ExecuteSynchronously);
#endif
                        return;
                    }

                    if (this.handlingOversizedMessage)
                    {
                        return;
                    }
                }
                else if (this.IsContentLengthInvalid(m, this.MaxContentLength))
                {
                    // if content length is set, preemptively close if it's too large
                    this.InvokeHandleOversizedMessage(context, m);
                    return;
                }

                if (m is IDecoderResultProvider provider && !provider.Result.IsSuccess)
                {
                    TOutput aggregated;
                    if (m is IByteBufferHolder holder)
                    {
                        aggregated = this.BeginAggregation(m, (IByteBuffer)holder.Content.Retain());
                    }
                    else
                    {
                        aggregated = this.BeginAggregation(m, Unpooled.Empty);
                    }
                    this.FinishAggregation(aggregated);
                    output.Add(aggregated);
                    return;
                }

                // A streamed message - initialize the cumulative buffer, and wait for incoming chunks.
                CompositeByteBuffer content = context.Allocator.CompositeBuffer(this.maxCumulationBufferComponents);
                if (m is IByteBufferHolder bufferHolder)
                {
                    AppendPartialContent(content, bufferHolder.Content);
                }
                this.currentMessage = this.BeginAggregation(m, content);
            }
            else if (this.IsContentMessage(message))
            {
                if (this.currentMessage == null)
                {
                    // it is possible that a TooLongFrameException was already thrown but we can still discard data
                    // until the begging of the next request/response.
                    return;
                }

                // Merge the received chunk into the content of the current message.
                var content = (CompositeByteBuffer)this.currentMessage.Content;

                var m = As<TContent>(message);

                // Handle oversized message.
                if (content.ReadableBytes > this.MaxContentLength - m.Content.ReadableBytes)
                {
                    // By convention, full message type extends first message type.
                    //@SuppressWarnings("unchecked")
                    var s = As<TStart>(this.currentMessage);
                    //Debug.Assert(s is object);

                    this.InvokeHandleOversizedMessage(context, s);
                    return;
                }

                // Append the content of the chunk.
                AppendPartialContent(content, m.Content);

                // Give the subtypes a chance to merge additional information such as trailing headers.
                this.Aggregate(this.currentMessage, m);

                bool last;
                if (m is IDecoderResultProvider provider)
                {
                    DecoderResult decoderResult = provider.Result;
                    if (!decoderResult.IsSuccess)
                    {
                        if (this.currentMessage is IDecoderResultProvider resultProvider)
                        {
                            resultProvider.Result = DecoderResult.Failure(decoderResult.Cause);
                        }

                        last = true;
                    }
                    else
                    {
                        last = this.IsLastContentMessage(m);
                    }
                }
                else
                {
                    last = this.IsLastContentMessage(m);
                }

                if (last)
                {
                    this.FinishAggregation(this.currentMessage);

                    // All done
                    output.Add(this.currentMessage);
                    this.currentMessage = default;
                }
            }
            else
            {
                CThrowHelper.ThrowMessageAggregationException_UnknownAggregationState();
            }
        }

        static readonly Action<Task, object> s_closeAfterWriteAction = CloseAfterWriteAction;
        static void CloseAfterWriteAction(Task task, object state)
        {
            var ctx = (IChannelHandlerContext)state;
            ctx.Channel.CloseAsync();
        }

        static readonly Action<Task, object> s_continueResponseWriteAction = ContinueResponseWriteAction;
        static void ContinueResponseWriteAction(Task task, object state)
        {
            if (task.IsFaulted)
            {
                var ctx = (IChannelHandlerContext)state;
                ctx.FireExceptionCaught(task.Exception);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        static T As<T>(object obj) => (T)obj;

        protected static void AppendPartialContent(CompositeByteBuffer content, IByteBuffer partialContent)
        {
            if (!partialContent.IsReadable())
            {
                return;
            }

            content.AddComponent((IByteBuffer)partialContent.Retain());
            content.SetWriterIndex(content.WriterIndex + partialContent.ReadableBytes);
        }

        protected abstract bool IsContentLengthInvalid(TStart start, int maxContentLength);

        protected abstract object NewContinueResponse(TStart start, int maxContentLength, IChannelPipeline pipeline);

        protected abstract bool CloseAfterContinueResponse(object msg);

        protected abstract bool IgnoreContentAfterContinueResponse(object msg);

        protected abstract TOutput BeginAggregation(TStart start, IByteBuffer content);

        protected virtual void Aggregate(TOutput aggregated, TContent content)
        {
        }

        protected virtual void FinishAggregation(TOutput aggregated)
        {
        }

        protected void InvokeHandleOversizedMessage(IChannelHandlerContext ctx, TStart oversized)
        {
            this.handlingOversizedMessage = true;
            this.currentMessage = default;
            try
            {
                this.HandleOversizedMessage(ctx, oversized);
            }
            finally
            {
                // Release the message in case it is a full one.
                ReferenceCountUtil.Release(oversized);
            }
        }

        protected virtual void HandleOversizedMessage(IChannelHandlerContext ctx, TStart oversized) =>
            ctx.FireExceptionCaught(CThrowHelper.GetTooLongFrameException(this.MaxContentLength));

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            // We might need keep reading the channel until the full message is aggregated.
            //
            // See https://github.com/netty/netty/issues/6583
            if (this.currentMessage is object && !this.handlerContext.Channel.Configuration.AutoRead)
            {
                context.Read();
            }

            context.FireChannelReadComplete();
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            try
            {
                // release current message if it is not null as it may be a left-over
                base.ChannelInactive(context);
            }
            finally
            {
                this.ReleaseCurrentMessage();
            }
        }

        public override void HandlerAdded(IChannelHandlerContext context) => this.handlerContext = context;

        public override void HandlerRemoved(IChannelHandlerContext context)
        {
            try
            {
                base.HandlerRemoved(context);
            }
            finally
            {
                // release current message if it is not null as it may be a left-over as there is not much more we can do in
                // this case
                this.ReleaseCurrentMessage();
            }
        }

        void ReleaseCurrentMessage()
        {
            if (this.currentMessage == null)
            {
                return;
            }

            this.currentMessage.Release();
            this.currentMessage = default;
            this.handlingOversizedMessage = false;
        }
    }
}
