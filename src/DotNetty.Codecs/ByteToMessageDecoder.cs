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

    /// <summary>
    /// <see cref="ChannelHandlerAdapter"/> which decodes bytes in a stream-like fashion from one <see cref="IByteBuffer"/> to an
    /// other Message type.
    ///
    /// For example here is an implementation which reads all readable bytes from
    /// the input <see cref="IByteBuffer"/> and create a new {<see cref="IByteBuffer"/>.
    ///
    /// <![CDATA[
    ///     public class SquareDecoder : ByteToMessageDecoder
    ///     {
    ///         public override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
    ///         {
    ///             output.add(input.ReadBytes(input.ReadableBytes));
    ///         }
    ///     }
    /// ]]>
    ///
    /// <c>Frame detection</c>
    /// <para>
    /// Generally frame detection should be handled earlier in the pipeline by adding a
    /// <see cref="DelimiterBasedFrameDecoder"/>, <see cref="FixedLengthFrameDecoder"/>, <see cref="LengthFieldBasedFrameDecoder"/>,
    /// or <see cref="LineBasedFrameDecoder"/>.
    /// </para>
    /// <para>
    /// If a custom frame decoder is required, then one needs to be careful when implementing
    /// one with <see cref="ByteToMessageDecoder"/>. Ensure there are enough bytes in the buffer for a
    /// complete frame by checking <see cref="IByteBuffer.ReadableBytes"/>. If there are not enough bytes
    /// for a complete frame, return without modifying the reader index to allow more bytes to arrive.
    /// </para>
    /// <para>
    /// To check for complete frames without modifying the reader index, use methods like <see cref="IByteBuffer.GetInt(int)"/>.
    /// One <c>MUST</c> use the reader index when using methods like <see cref="IByteBuffer.GetInt(int)"/>.
    /// For example calling <tt>input.GetInt(0)</tt> is assuming the frame starts at the beginning of the buffer, which
    /// is not always the case. Use <tt>input.GetInt(input.ReaderIndex)</tt> instead.
    /// <c>Pitfalls</c>
    /// </para>
    /// <para>
    /// Be aware that sub-classes of <see cref="ByteToMessageDecoder"/> <c>MUST NOT</c>
    /// annotated with <see cref="ChannelHandlerAdapter.IsSharable"/>.
    /// </para>
    /// Some methods such as <see cref="IByteBuffer.ReadBytes(int)"/> will cause a memory leak if the returned buffer
    /// is not released or added to the <tt>output</tt> <see cref="List{Object}"/>. Use derived buffers like <see cref="IByteBuffer.ReadSlice(int)"/>
    /// to avoid leaking memory.
    /// </summary>
    public abstract partial class ByteToMessageDecoder : ChannelHandlerAdapter
    {
        /// <summary>
        /// Cumulate the given <see cref="IByteBuffer"/>s and return the <see cref="IByteBuffer"/> that holds the cumulated bytes.
        /// The implementation is responsible to correctly handle the life-cycle of the given <see cref="IByteBuffer"/>s and so
        /// call <see cref="IReferenceCounted.Release()"/> if a <see cref="IByteBuffer"/> is fully consumed.
        /// </summary>
        /// <param name="alloc"></param>
        /// <param name="cumulation"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        public delegate IByteBuffer CumulationFunc(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer input);

        /// <summary>
        /// Cumulates instances of <see cref="IByteBuffer" /> by merging them into one <see cref="IByteBuffer" />, using memory copies.
        /// </summary>
        public static readonly CumulationFunc MergeCumulator = MergeCumulatorInternal;
        private static IByteBuffer MergeCumulatorInternal(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer input)
        {
            try
            {
                IByteBuffer buffer;
                if (cumulation.WriterIndex > cumulation.MaxCapacity - input.ReadableBytes ||
                    cumulation.ReferenceCount > 1 || cumulation.IsReadOnly)
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
                return buffer;
            }
            finally
            {
                // We must release in in all cases as otherwise it may produce a leak if writeBytes(...) throw
                // for whatever release (for example because of OutOfMemoryError)
                input.Release();
            }
        }

        /// <summary>
        /// Cumulate instances of <see cref="IByteBuffer" /> by add them to a <see cref="CompositeByteBuffer" /> and therefore
        /// avoiding memory copy when possible.
        /// </summary>
        /// <remarks>
        /// Be aware that <see cref="CompositeByteBuffer" /> use a more complex indexing implementation so depending on your use-case
        /// and the decoder implementation this may be slower then just use the <see cref="MergeCumulator" />.
        /// </remarks>
        public static readonly CumulationFunc CompositionCumulation = CompositionCumulationInternal;
        private static IByteBuffer CompositionCumulationInternal(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer input)
        {
            try
            {
                IByteBuffer buffer;
                if (cumulation.ReferenceCount > 1)
                {
                    // Expand cumulation (by replace it) when the refCnt is greater then 1 which may happen when the
                    // user use slice().retain() or duplicate().retain().
                    //
                    // See:
                    // - https://github.com/netty/netty/issues/2327
                    // - https://github.com/netty/netty/issues/1764
                    buffer = ExpandCumulation(alloc, cumulation, input.ReadableBytes);
                    buffer.WriteBytes(input);
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
                    input = null;
                    buffer = composite;
                }
                return buffer;
            }
            finally
            {
                // We must release if the ownership was not transferred as otherwise it may produce a leak if
                // writeBytes(...) throw for whatever release (for example because of OutOfMemoryError).
                input?.Release();
            }
        }

        const byte STATE_INIT = 0;
        const byte STATE_CALLING_CHILD_DECODE = 1;
        const byte STATE_HANDLER_REMOVED_PENDING = 2;

        private IByteBuffer _cumulation;
        private CumulationFunc _cumulator = MergeCumulator;
        private bool _first;

        /// <summary>
        /// This flag is used to determine if we need to call <see cref="IChannelHandlerContext.Read"/> to consume more data
        /// when <see cref="IChannelConfiguration.AutoRead"/> is <c>false</c>.
        /// </summary>
        private bool _firedChannelRead;

        /// <summary>
        /// A bitmask where the bits are defined as
        /// <see cref="STATE_INIT"/>
        /// <see cref="STATE_CALLING_CHILD_DECODE"/>
        /// <see cref="STATE_HANDLER_REMOVED_PENDING"/>
        /// </summary>
        private byte _decodeState = STATE_INIT;
        private int _discardAfterReads = 16;
        private int _numReads;

        protected ByteToMessageDecoder()
        {
            // ReSharper disable once DoNotCallOverridableMethodsInConstructor -- used for safety check only
            if (IsSharable)
            {
                CThrowHelper.ThrowInvalidOperationException_ByteToMessageDecoder();
            }
        }

        /// <summary>
        /// Determines whether only one message should be decoded per <see cref="ChannelRead" /> call.
        /// This may be useful if you need to do some protocol upgrade and want to make sure nothing is mixed up.
        /// 
        /// Default is <c>false</c> as this has performance impacts.
        /// </summary>
        public bool SingleDecode { get; set; }

        /// <summary>
        /// Set the <see cref="CumulationFunc"/> to use for cumulate the received <see cref="IByteBuffer"/>s.
        /// </summary>
        /// <param name="cumulationFunc"></param>
        public void SetCumulator(CumulationFunc cumulationFunc)
        {
            if (cumulationFunc is null) { CThrowHelper.ThrowArgumentNullException(CExceptionArgument.cumulationFunc); }

            _cumulator = cumulationFunc;
        }

        /// <summary>
        /// Set the number of reads after which <see cref="IByteBuffer.DiscardSomeReadBytes"/> are called and so free up memory.
        /// The default is <code>16</code>.
        /// </summary>
        /// <param name="discardAfterReads"></param>
        public void SetDiscardAfterReads(int discardAfterReads)
        {
            if ((uint)(discardAfterReads - 1) > SharedConstants.TooBigOrNegative) // <= 0
            {
                CThrowHelper.ThrowArgumentException_DiscardAfterReads();
            }
            _discardAfterReads = discardAfterReads;
        }

        /// <summary>
        /// Returns the actual number of readable bytes in the internal cumulative
        /// buffer of this decoder. You usually do not need to rely on this value
        /// to write a decoder. Use it only when you must use it at your own risk.
        /// This method is a shortcut to <see cref="IByteBuffer.ReadableBytes" /> of <see cref="InternalBuffer" />.
        /// </summary>
        protected int ActualReadableBytes => InternalBuffer.ReadableBytes;

        /// <summary>
        /// Returns the internal cumulative buffer of this decoder. You usually
        /// do not need to access the internal buffer directly to write a decoder.
        /// Use it only when you must use it at your own risk.
        /// </summary>
        protected IByteBuffer InternalBuffer
        {
            get
            {
                if (_cumulation is object)
                {
                    return _cumulation;
                }
                else
                {
                    return Unpooled.Empty;
                }
            }
        }

        /// <inheritdoc />
        public override void HandlerRemoved(IChannelHandlerContext context)
        {
            if (_decodeState == STATE_CALLING_CHILD_DECODE)
            {
                _decodeState = STATE_HANDLER_REMOVED_PENDING;
                return;
            }
            IByteBuffer buf = _cumulation;
            if (buf is object)
            {
                // Directly set this to null so we are sure we not access it in any other method here anymore.
                _cumulation = null;
                _numReads = 0;
                int readable = buf.ReadableBytes;
                if (readable > 0)
                {
                    context.FireChannelRead(buf);
                    context.FireChannelReadComplete();
                }
                else
                {
                    buf.Release();
                }
            }
            HandlerRemovedInternal(context);
        }

        /// <summary>
        /// Gets called after the <see cref="ByteToMessageDecoder"/> was removed from the actual context and it doesn't handle
        /// events anymore.
        /// </summary>
        /// <param name="context"></param>
        protected virtual void HandlerRemovedInternal(IChannelHandlerContext context)
        {
        }

        /// <inheritdoc />
        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (message is IByteBuffer data)
            {
                ThreadLocalObjectList output = ThreadLocalObjectList.NewInstance();
                try
                {
                    _first = _cumulation is null;
                    if (_first)
                    {
                        _cumulation = data;
                    }
                    else
                    {
                        _cumulation = _cumulator(context.Allocator, _cumulation, data);
                    }
                    CallDecode(context, _cumulation, output);
                }
                catch (DecoderException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    CThrowHelper.ThrowDecoderException(ex);
                }
                finally
                {
                    if (_cumulation is object && !_cumulation.IsReadable())
                    {
                        _numReads = 0;
                        _cumulation.Release();
                        _cumulation = null;
                    }
                    else if (++_numReads >= _discardAfterReads)
                    {
                        // We did enough reads already try to discard some bytes so we not risk to see a OOME.
                        // See https://github.com/netty/netty/issues/4275
                        _numReads = 0;
                        DiscardSomeReadBytes();
                    }

                    int size = output.Count;
                    _firedChannelRead |= (uint)size > 0u;

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

        /// <summary>
        /// Get <paramref name="numElements"/> out of the <paramref name="output"/> and forward these through the pipeline.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="output"></param>
        /// <param name="numElements"></param>
        protected static void FireChannelRead(IChannelHandlerContext ctx, List<object> output, int numElements)
        {
            for (int i = 0; i < numElements; i++)
            {
                ctx.FireChannelRead(output[i]);
            }
        }

        /// <inheritdoc />
        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            _numReads = 0;
            DiscardSomeReadBytes();
            if (!_firedChannelRead && !context.Channel.Configuration.AutoRead)
            {
                context.Read();
            }
            _firedChannelRead = false;
            context.FireChannelReadComplete();
        }

        protected void DiscardSomeReadBytes()
        {
            if (_cumulation is object && !_first && _cumulation.ReferenceCount == 1)
            {
                // discard some bytes if possible to make more room input the
                // buffer but only if the refCnt == 1  as otherwise the user may have
                // used slice().retain() or duplicate().retain().
                //
                // See:
                // - https://github.com/netty/netty/issues/2327
                // - https://github.com/netty/netty/issues/1764
                _cumulation.DiscardSomeReadBytes();
            }
        }

        /// <inheritdoc />
        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            ChannelInputClosed(ctx, true);
        }

        /// <inheritdoc />
        public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
        {
            if (evt is ChannelInputShutdownEvent)
            {
                // The decodeLast method is invoked when a channelInactive event is encountered.
                // This method is responsible for ending requests in some situations and must be called
                // when the input has been shutdown.
                ChannelInputClosed(ctx, false);
            }
            ctx.FireUserEventTriggered(evt);
        }

        private void ChannelInputClosed(IChannelHandlerContext ctx, bool callChannelInactive)
        {
            ThreadLocalObjectList output = ThreadLocalObjectList.NewInstance();
            try
            {
                ChannelInputClosed(ctx, output);
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
                    if (_cumulation is object)
                    {
                        _cumulation.Release();
                        _cumulation = null;
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

        /// <summary>
        /// Called when the input of the channel was closed which may be because it changed to inactive or because of
        /// <see cref="ChannelInputShutdownEvent"/>
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="output"></param>
        protected virtual void ChannelInputClosed(IChannelHandlerContext ctx, List<object> output)
        {
            if (_cumulation is object)
            {
                CallDecode(ctx, _cumulation, output);
                DecodeLast(ctx, _cumulation, output);
            }
            else
            {
                DecodeLast(ctx, Unpooled.Empty, output);
            }
        }

        /// <summary>
        /// Called once data should be decoded from the given <see cref="IByteBuffer"/>. This method will call
        /// <see cref="Decode(IChannelHandlerContext, IByteBuffer, List{object})"/> as long as decoding should take place.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="input"></param>
        /// <param name="output"></param>
        protected virtual void CallDecode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            if (context is null) { CThrowHelper.ThrowArgumentNullException(CExceptionArgument.context); }
            if (input is null) { CThrowHelper.ThrowArgumentNullException(CExceptionArgument.input); }
            if (output is null) { CThrowHelper.ThrowArgumentNullException(CExceptionArgument.output); }

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
                    DecodeRemovalReentryProtection(context, input, output);

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
                        CThrowHelper.ThrowDecoderException_ByteToMessageDecoder(GetType());
                    }

                    if (SingleDecode)
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
                CThrowHelper.ThrowDecoderException(cause);
            }
        }

        /// <summary>
        /// Decode the from one <see cref="IByteBuffer"/> to an other. This method will be called till either the input
        /// <see cref="IByteBuffer"/> has nothing to read when return from this method or till nothing was read from the input
        /// <see cref="IByteBuffer"/>.
        /// </summary>
        /// <param name="context">the <see cref="IChannelHandlerContext"/> which this <see cref="ByteToMessageDecoder"/> belongs to</param>
        /// <param name="input">the <see cref="IByteBuffer"/> from which to read data</param>
        /// <param name="output">the <see cref="List{Object}"/> to which decoded messages should be added</param>
        protected internal abstract void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output);

        /// <summary>
        /// Decode the from one <see cref="IByteBuffer"/> to an other. This method will be called till either the input
        /// <see cref="IByteBuffer"/> has nothing to read when return from this method or till nothing was read from the input
        /// <see cref="IByteBuffer"/>.
        /// </summary>
        /// <param name="ctx">the <see cref="IChannelHandlerContext"/> which this <see cref="ByteToMessageDecoder"/> belongs to</param>
        /// <param name="input">the <see cref="IByteBuffer"/> from which to read data</param>
        /// <param name="output">the <see cref="List{Object}"/> to which decoded messages should be added</param>
        protected void DecodeRemovalReentryProtection(IChannelHandlerContext ctx, IByteBuffer input, List<object> output)
        {
            _decodeState = STATE_CALLING_CHILD_DECODE;
            try
            {
                Decode(ctx, input, output);
            }
            finally
            {
                var removePending = _decodeState == STATE_HANDLER_REMOVED_PENDING;
                _decodeState = STATE_INIT;
                if (removePending)
                {
                    FireChannelRead(ctx, output, output.Count);
                    output.Clear();
                    HandlerRemoved(ctx);
                }
            }
        }

        /// <summary>
        /// Is called one last time when the <see cref="IChannelHandlerContext"/> goes in-active. Which means the
        /// <see cref="ChannelInactive(IChannelHandlerContext)"/> was triggered.
        /// 
        /// By default this will just call <see cref="Decode(IChannelHandlerContext, IByteBuffer, List{object})"/> but sub-classes may
        /// override this for some special cleanup operation.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="input"></param>
        /// <param name="output"></param>
        protected virtual void DecodeLast(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            if (input.IsReadable())
            {
                // Only call decode() if there is something left in the buffer to decode.
                // See https://github.com/netty/netty/issues/4386
                Decode(context, input, output);
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