﻿/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// A <see cref="IHttp2FrameWriter"/> that supports all frame types defined by the HTTP/2 specification.
    /// </summary>
    public class DefaultHttp2FrameWriter : IHttp2FrameWriter, IHttp2FrameSizePolicy, IHttp2FrameWriterConfiguration
    {
        /// <summary>
        /// This buffer is allocated to the maximum size of the padding field, and filled with zeros.
        /// When padding is needed it can be taken as a slice of this buffer. Users should call <see cref="IReferenceCounted.Retain()"/>
        /// before using their slice.
        /// </summary>
        static readonly IByteBuffer ZeroBuffer =
            Unpooled.UnreleasableBuffer(Unpooled.DirectBuffer(Http2CodecUtil.MaxUnsignedByte).WriteZero(Http2CodecUtil.MaxUnsignedByte).AsReadOnly());

        private readonly IHttp2HeadersEncoder _headersEncoder;
        private int _maxFrameSize;

        public DefaultHttp2FrameWriter()
            : this(new DefaultHttp2HeadersEncoder())
        {
        }

        public DefaultHttp2FrameWriter(ISensitivityDetector headersSensitivityDetector)
            : this(new DefaultHttp2HeadersEncoder(headersSensitivityDetector))
        {
        }

        public DefaultHttp2FrameWriter(ISensitivityDetector headersSensitivityDetector, bool ignoreMaxHeaderListSize)
            : this(new DefaultHttp2HeadersEncoder(headersSensitivityDetector, ignoreMaxHeaderListSize))
        {
        }

        public DefaultHttp2FrameWriter(IHttp2HeadersEncoder headersEncoder)
        {
            _headersEncoder = headersEncoder;
            _maxFrameSize = Http2CodecUtil.DefaultMaxFrameSize;
        }

        public IHttp2FrameWriterConfiguration Configuration => this;

        public IHttp2HeadersEncoderConfiguration HeadersConfiguration => _headersEncoder.Configuration;

        public IHttp2FrameSizePolicy FrameSizePolicy => this;

        public void SetMaxFrameSize(int max)
        {
            if (!Http2CodecUtil.IsMaxFrameSizeValid(max))
            {
                ThrowHelper.ThrowConnectionError_InvalidMaxFrameSizeSpecifiedInSentSettings(max);
            }

            _maxFrameSize = max;
        }

        public int MaxFrameSize => _maxFrameSize;

        public virtual Task WriteDataAsync(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream, IPromise promise)
        {
            SimplePromiseAggregator promiseAggregator = new SimplePromiseAggregator(promise);
            IByteBuffer frameHeader = null;
            try
            {
                if ((uint)(streamId - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(ExceptionArgument.StreamID); }
                Http2CodecUtil.VerifyPadding(padding);

                int remainingData = data.ReadableBytes;
                Http2Flags flags = new Http2Flags();
                _ = flags.EndOfStream(false);
                _ = flags.PaddingPresent(false);
                // Fast path to write frames of payload size maxFrameSize first.
                if (remainingData > _maxFrameSize)
                {
                    frameHeader = ctx.Allocator.Buffer(Http2CodecUtil.FrameHeaderLength);
                    Http2CodecUtil.WriteFrameHeaderInternal(frameHeader, _maxFrameSize, Http2FrameTypes.Data, flags, streamId);
                    do
                    {
                        // Write the header.
                        _ = ctx.WriteAsync(frameHeader.RetainedSlice(), promiseAggregator.NewPromise());

                        // Write the payload.
                        _ = ctx.WriteAsync(data.ReadRetainedSlice(_maxFrameSize), promiseAggregator.NewPromise());

                        remainingData -= _maxFrameSize;
                        // Stop iterating if remainingData ==  _maxFrameSize so we can take care of reference counts below.
                    }
                    while (remainingData > _maxFrameSize);
                }

                if (0u >= (uint)padding)
                {
                    // Write the header.
                    if (frameHeader is object)
                    {
                        _ = frameHeader.Release();
                        frameHeader = null;
                    }

                    IByteBuffer frameHeader2 = ctx.Allocator.Buffer(Http2CodecUtil.FrameHeaderLength);
                    _ = flags.EndOfStream(endOfStream);
                    Http2CodecUtil.WriteFrameHeaderInternal(frameHeader2, remainingData, Http2FrameTypes.Data, flags, streamId);
                    _ = ctx.WriteAsync(frameHeader2, promiseAggregator.NewPromise());

                    // Write the payload.
                    IByteBuffer lastFrame = data.ReadSlice(remainingData);
                    data = null;
                    _ = ctx.WriteAsync(lastFrame, promiseAggregator.NewPromise());
                }
                else
                {
                    if (remainingData != _maxFrameSize)
                    {
                        if (frameHeader is object)
                        {
                            _ = frameHeader.Release();
                            frameHeader = null;
                        }
                    }
                    else
                    {
                        remainingData -= _maxFrameSize;
                        // Write the header.
                        IByteBuffer lastFrame;
                        if (frameHeader is null)
                        {
                            lastFrame = ctx.Allocator.Buffer(Http2CodecUtil.FrameHeaderLength);
                            Http2CodecUtil.WriteFrameHeaderInternal(lastFrame, _maxFrameSize, Http2FrameTypes.Data, flags, streamId);
                        }
                        else
                        {
                            lastFrame = frameHeader.Slice();
                            frameHeader = null;
                        }

                        _ = ctx.WriteAsync(lastFrame, promiseAggregator.NewPromise());

                        // Write the payload.
                        lastFrame = data.ReadableBytes != _maxFrameSize ? data.ReadSlice(_maxFrameSize) : data;
                        data = null;
                        _ = ctx.WriteAsync(lastFrame, promiseAggregator.NewPromise());
                    }

                    do
                    {
                        int frameDataBytes = Math.Min(remainingData, _maxFrameSize);
                        int framePaddingBytes = Math.Min(padding, Math.Max(0, (_maxFrameSize - 1) - frameDataBytes));

                        // Decrement the remaining counters.
                        padding -= framePaddingBytes;
                        remainingData -= frameDataBytes;

                        // Write the header.
                        IByteBuffer frameHeader2 = ctx.Allocator.Buffer(Http2CodecUtil.DataFrameHeaderLength);
                        _ = flags.EndOfStream(endOfStream && 0u >= (uint)remainingData && 0u >= (uint)padding);
                        _ = flags.PaddingPresent(framePaddingBytes > 0);
                        Http2CodecUtil.WriteFrameHeaderInternal(frameHeader2, framePaddingBytes + frameDataBytes, Http2FrameTypes.Data, flags, streamId);
                        WritePaddingLength(frameHeader2, framePaddingBytes);
                        _ = ctx.WriteAsync(frameHeader2, promiseAggregator.NewPromise());

                        // Write the payload.
                        if (frameDataBytes != 0)
                        {
                            if (0u >= (uint)remainingData)
                            {
                                IByteBuffer lastFrame = data.ReadSlice(frameDataBytes);
                                data = null;
                                _ = ctx.WriteAsync(lastFrame, promiseAggregator.NewPromise());
                            }
                            else
                            {
                                _ = ctx.WriteAsync(data.ReadRetainedSlice(frameDataBytes), promiseAggregator.NewPromise());
                            }
                        }

                        // Write the frame padding.
                        if (PaddingBytes(framePaddingBytes) > 0)
                        {
                            _ = ctx.WriteAsync(
                                ZeroBuffer.Slice(0, PaddingBytes(framePaddingBytes)),
                                promiseAggregator.NewPromise());
                        }
                    }
                    while (remainingData != 0 || padding != 0);
                }
            }
            catch (Exception cause)
            {
                if (frameHeader is object) { _ = frameHeader.Release(); }

                // Use a try/finally here in case the data has been released before calling this method. This is not
                // necessary above because we internally allocate frameHeader.
                try
                {
                    if (data is object) { _ = data.Release(); }
                }
                finally
                {
                    promiseAggregator.SetException(cause);
                    _ = promiseAggregator.DoneAllocatingPromises();
                }

                return promiseAggregator.Task;
            }

            _ = promiseAggregator.DoneAllocatingPromises();
            return promiseAggregator.Task;
        }

        public virtual Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId,
            IHttp2Headers headers, int padding, bool endOfStream, IPromise promise)
        {
            return WriteHeadersInternal(ctx, streamId, headers, padding, endOfStream, false, 0, 0, false, promise);
        }

        public virtual Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId,
            IHttp2Headers headers, int streamDependency, short weight, bool exclusive,
            int padding, bool endOfStream, IPromise promise)
        {
            return WriteHeadersInternal(
                ctx,
                streamId,
                headers,
                padding,
                endOfStream,
                true,
                streamDependency,
                weight,
                exclusive,
                promise);
        }

        public virtual Task WritePriorityAsync(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive, IPromise promise)
        {
            try
            {
                if ((uint)(streamId - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(ExceptionArgument.StreamID); }
                if ((uint)(streamDependency - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(ExceptionArgument.StreamDependency); }
                VerifyWeight(weight);

                IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.PriorityFrameLength);
                Http2CodecUtil.WriteFrameHeaderInternal(buf, Http2CodecUtil.PriorityEntryLength, Http2FrameTypes.Priority, new Http2Flags(), streamId);
                _ = buf.WriteInt(exclusive ? (int)(uint)(0x80000000L | streamDependency) : streamDependency);
                // Adjust the weight so that it fits into a single byte on the wire.
                _ = buf.WriteByte(weight - 1);
                return ctx.WriteAsync(buf, promise);
            }
            catch (Exception t)
            {
                promise.SetException(t);
                return promise.Task;
            }
        }

        public virtual Task WriteRstStreamAsync(IChannelHandlerContext ctx, int streamId, Http2Error errorCode, IPromise promise)
        {
            try
            {
                if ((uint)(streamId - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(ExceptionArgument.StreamID); }
                VerifyErrorCode((long)errorCode);

                IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.RstStreamFrameLength);
                Http2CodecUtil.WriteFrameHeaderInternal(buf, Http2CodecUtil.IntFieldLength, Http2FrameTypes.RstStream, new Http2Flags(), streamId);
                _ = buf.WriteInt((int)errorCode);
                return ctx.WriteAsync(buf, promise);
            }
            catch (Exception t)
            {
                promise.SetException(t);
                return promise.Task;
            }
        }

        public virtual Task WriteSettingsAsync(IChannelHandlerContext ctx, Http2Settings settings, IPromise promise)
        {
            try
            {
                if (settings is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.settings); }

                int payloadLength = Http2CodecUtil.SettingEntryLength * settings.Count;
                IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.FrameHeaderLength + settings.Count * Http2CodecUtil.SettingEntryLength);
                Http2CodecUtil.WriteFrameHeaderInternal(buf, payloadLength, Http2FrameTypes.Settings, new Http2Flags(), 0);
                foreach (KeyValuePair<char, long> entry in settings)
                {
                    _ = buf.WriteChar(entry.Key);
                    _ = buf.WriteInt((int)entry.Value);
                }

                return ctx.WriteAsync(buf, promise);
            }
            catch (Exception t)
            {
                promise.SetException(t);
                return promise.Task;
            }
        }

        public virtual Task WriteSettingsAckAsync(IChannelHandlerContext ctx, IPromise promise)
        {
            try
            {
                IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.FrameHeaderLength);
                Http2CodecUtil.WriteFrameHeaderInternal(buf, 0, Http2FrameTypes.Settings, new Http2Flags().Ack(true), 0);
                return ctx.WriteAsync(buf, promise);
            }
            catch (Exception t)
            {
                promise.SetException(t);
                return promise.Task;
            }
        }

        public virtual Task WritePingAsync(IChannelHandlerContext ctx, bool ack, long data, IPromise promise)
        {
            var flags = ack ? new Http2Flags().Ack(true) : new Http2Flags();
            var buf = ctx.Allocator.Buffer(Http2CodecUtil.FrameHeaderLength + Http2CodecUtil.PingFramePayloadLength);
            // Assume nothing below will throw until buf is written. That way we don't have to take care of ownership
            // in the catch block.
            Http2CodecUtil.WriteFrameHeaderInternal(buf, Http2CodecUtil.PingFramePayloadLength, Http2FrameTypes.Ping, flags, 0);
            _ = buf.WriteLong(data);
            return ctx.WriteAsync(buf, promise);
        }

        public virtual Task WritePushPromiseAsync(IChannelHandlerContext ctx, int streamId,
            int promisedStreamId, IHttp2Headers headers, int padding, IPromise promise)
        {
            IByteBuffer headerBlock = null;
            SimplePromiseAggregator promiseAggregator = new SimplePromiseAggregator(promise);
            try
            {
                if ((uint)(streamId - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(ExceptionArgument.StreamID); }
                if ((uint)(promisedStreamId - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(ExceptionArgument.PromisedStreamId); }
                Http2CodecUtil.VerifyPadding(padding);

                // Encode the entire header block into an intermediate buffer.
                headerBlock = ctx.Allocator.Buffer();
                _headersEncoder.EncodeHeaders(streamId, headers, headerBlock);

                // Read the first fragment (possibly everything).
                Http2Flags flags = new Http2Flags().PaddingPresent(padding > 0);
                // IntFieldLength is for the length of the promisedStreamId
                int nonFragmentLength = Http2CodecUtil.IntFieldLength + padding;
                int maxFragmentLength = _maxFrameSize - nonFragmentLength;
                IByteBuffer fragment = headerBlock.ReadRetainedSlice(Math.Min(headerBlock.ReadableBytes, maxFragmentLength));

                _ = flags.EndOfHeaders(!headerBlock.IsReadable());

                int payloadLength = fragment.ReadableBytes + nonFragmentLength;
                IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.PushPromiseFrameHeaderLength);
                Http2CodecUtil.WriteFrameHeaderInternal(buf, payloadLength, Http2FrameTypes.PushPromise, flags, streamId);
                WritePaddingLength(buf, padding);

                // Write out the promised stream ID.
                _ = buf.WriteInt(promisedStreamId);
                _ = ctx.WriteAsync(buf, promiseAggregator.NewPromise());

                // Write the first fragment.
                _ = ctx.WriteAsync(fragment, promiseAggregator.NewPromise());

                // Write out the padding, if any.
                if (PaddingBytes(padding) > 0)
                {
                    _ = ctx.WriteAsync(ZeroBuffer.Slice(0, PaddingBytes(padding)), promiseAggregator.NewPromise());
                }

                if (!flags.EndOfHeaders())
                {
                    _ = WriteContinuationFramesAsync(ctx, streamId, headerBlock, promiseAggregator);
                }
            }
            catch (Http2Exception e)
            {
                promiseAggregator.SetException(e);
            }
            catch (Exception t)
            {
                promiseAggregator.SetException(t);
                _ = promiseAggregator.DoneAllocatingPromises();
                throw;
            }
            finally
            {
                if (headerBlock is object) { _ = headerBlock.Release(); }
            }

            _ = promiseAggregator.DoneAllocatingPromises();
            return promiseAggregator.Task;
        }

        public virtual Task WriteGoAwayAsync(IChannelHandlerContext ctx, int lastStreamId,
            Http2Error errorCode, IByteBuffer debugData, IPromise promise)
        {
            SimplePromiseAggregator promiseAggregator = new SimplePromiseAggregator(promise);
            try
            {
                if ((uint)lastStreamId > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(ExceptionArgument.LastStreamId); }
                VerifyErrorCode((long)errorCode);

                int payloadLength = 8 + debugData.ReadableBytes;
                IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.GoAwayFrameHeaderLength);
                // Assume nothing below will throw until buf is written. That way we don't have to take care of ownership
                // in the catch block.
                Http2CodecUtil.WriteFrameHeaderInternal(buf, payloadLength, Http2FrameTypes.GoAway, new Http2Flags(), 0);
                _ = buf.WriteInt(lastStreamId);
                _ = buf.WriteInt((int)errorCode);
                _ = ctx.WriteAsync(buf, promiseAggregator.NewPromise());
            }
            catch (Exception t)
            {
                try
                {
                    _ = debugData.Release();
                }
                finally
                {
                    promiseAggregator.SetException(t);
                    _ = promiseAggregator.DoneAllocatingPromises();
                }

                return promiseAggregator.Task;
            }

            try
            {
                _ = ctx.WriteAsync(debugData, promiseAggregator.NewPromise());
            }
            catch (Exception t)
            {
                promiseAggregator.SetException(t);
            }

            _ = promiseAggregator.DoneAllocatingPromises();
            return promiseAggregator.Task;
        }

        public virtual Task WriteWindowUpdateAsync(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement, IPromise promise)
        {
            try
            {
                if ((uint)streamId > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(ExceptionArgument.StreamID); }
                VerifyWindowSizeIncrement(windowSizeIncrement);

                IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.WindowUpdateFrameLength);
                Http2CodecUtil.WriteFrameHeaderInternal(buf, Http2CodecUtil.IntFieldLength, Http2FrameTypes.WindowUpdate, new Http2Flags(), streamId);
                _ = buf.WriteInt(windowSizeIncrement);
                return ctx.WriteAsync(buf, promise);
            }
            catch (Exception t)
            {
                promise.SetException(t);
                return promise.Task;
            }
        }

        public virtual Task WriteFrameAsync(IChannelHandlerContext ctx, Http2FrameTypes frameType,
            int streamId, Http2Flags flags, IByteBuffer payload, IPromise promise)
        {
            SimplePromiseAggregator promiseAggregator = new SimplePromiseAggregator(promise);
            try
            {
                if ((uint)streamId > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(ExceptionArgument.StreamID); }
                IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.FrameHeaderLength);
                // Assume nothing below will throw until buf is written. That way we don't have to take care of ownership
                // in the catch block.
                Http2CodecUtil.WriteFrameHeaderInternal(buf, payload.ReadableBytes, frameType, flags, streamId);
                _ = ctx.WriteAsync(buf, promiseAggregator.NewPromise());
            }
            catch (Exception t)
            {
                try
                {
                    _ = payload.Release();
                }
                finally
                {
                    promiseAggregator.SetException(t);
                    _ = promiseAggregator.DoneAllocatingPromises();
                }

                return promiseAggregator.Task;
            }

            try
            {
                _ = ctx.WriteAsync(payload, promiseAggregator.NewPromise());
            }
            catch (Exception t)
            {
                promiseAggregator.SetException(t);
            }

            _ = promiseAggregator.DoneAllocatingPromises();
            return promiseAggregator.Task;
        }

        public void Dispose() => Close();

        protected virtual void Dispose(bool disposing)
        {
        }

        public virtual void Close()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        Task WriteHeadersInternal(IChannelHandlerContext ctx, int streamId,
            IHttp2Headers headers, int padding, bool endOfStream, bool hasPriority,
            int streamDependency, short weight, bool exclusive, IPromise promise)
        {
            IByteBuffer headerBlock = null;
            SimplePromiseAggregator promiseAggregator = new SimplePromiseAggregator(promise);
            try
            {
                if ((uint)(streamId - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(ExceptionArgument.StreamID); }
                if (hasPriority)
                {
                    if ((uint)streamDependency > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(ExceptionArgument.StreamDependency); }
                    Http2CodecUtil.VerifyPadding(padding);
                    VerifyWeight(weight);
                }

                // Encode the entire header block.
                headerBlock = ctx.Allocator.Buffer();
                _headersEncoder.EncodeHeaders(streamId, headers, headerBlock);

                Http2Flags flags =
                    new Http2Flags().EndOfStream(endOfStream).PriorityPresent(hasPriority).PaddingPresent(padding > 0);

                // Read the first fragment (possibly everything).
                int nonFragmentBytes = padding + flags.GetNumPriorityBytes();
                int maxFragmentLength = _maxFrameSize - nonFragmentBytes;
                IByteBuffer fragment = headerBlock.ReadRetainedSlice(Math.Min(headerBlock.ReadableBytes, maxFragmentLength));

                // Set the end of headers flag for the first frame.
                _ = flags.EndOfHeaders(!headerBlock.IsReadable());

                int payloadLength = fragment.ReadableBytes + nonFragmentBytes;
                IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.HeadersFrameHeaderLength);
                Http2CodecUtil.WriteFrameHeaderInternal(buf, payloadLength, Http2FrameTypes.Headers, flags, streamId);
                WritePaddingLength(buf, padding);

                if (hasPriority)
                {
                    _ = buf.WriteInt(exclusive ? (int)(uint)(0x80000000L | streamDependency) : streamDependency);

                    // Adjust the weight so that it fits into a single byte on the wire.
                    _ = buf.WriteByte(weight - 1);
                }

                _ = ctx.WriteAsync(buf, promiseAggregator.NewPromise());

                // Write the first fragment.
                _ = ctx.WriteAsync(fragment, promiseAggregator.NewPromise());

                // Write out the padding, if any.
                if (PaddingBytes(padding) > 0)
                {
                    _ = ctx.WriteAsync(ZeroBuffer.Slice(0, PaddingBytes(padding)), promiseAggregator.NewPromise());
                }

                if (!flags.EndOfHeaders())
                {
                    _ = WriteContinuationFramesAsync(ctx, streamId, headerBlock, promiseAggregator);
                }
            }
            catch (Http2Exception e)
            {
                promiseAggregator.SetException(e);
            }
            catch (Exception t)
            {
                promiseAggregator.SetException(t);
                _ = promiseAggregator.DoneAllocatingPromises();
                throw;
            }
            finally
            {
                _ = (headerBlock?.Release());
            }

            _ = promiseAggregator.DoneAllocatingPromises();
            return promiseAggregator.Task;
        }

        /// <summary>
        /// Writes as many continuation frames as needed until <paramref name="headerBlock"/> are consumed.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="streamId"></param>
        /// <param name="headerBlock"></param>
        /// <param name="promiseAggregator"></param>
        /// <returns></returns>
        Task WriteContinuationFramesAsync(IChannelHandlerContext ctx, int streamId,
            IByteBuffer headerBlock, SimplePromiseAggregator promiseAggregator)
        {
            Http2Flags flags = new Http2Flags();

            if (headerBlock.IsReadable())
            {
                // The frame header (and padding) only changes on the last frame, so allocate it once and re-use
                int fragmentReadableBytes = Math.Min(headerBlock.ReadableBytes, _maxFrameSize);
                IByteBuffer buf = ctx.Allocator.Buffer(Http2CodecUtil.ContinuationFrameHeaderLength);
                Http2CodecUtil.WriteFrameHeaderInternal(buf, fragmentReadableBytes, Http2FrameTypes.Continuation, flags, streamId);

                do
                {
                    fragmentReadableBytes = Math.Min(headerBlock.ReadableBytes, _maxFrameSize);
                    IByteBuffer fragment = headerBlock.ReadRetainedSlice(fragmentReadableBytes);

                    if (headerBlock.IsReadable())
                    {
                        _ = ctx.WriteAsync(buf.Retain(), promiseAggregator.NewPromise());
                    }
                    else
                    {
                        // The frame header is different for the last frame, so re-allocate and release the old buffer
                        flags = flags.EndOfHeaders(true);
                        _ = buf.Release();
                        buf = ctx.Allocator.Buffer(Http2CodecUtil.ContinuationFrameHeaderLength);
                        Http2CodecUtil.WriteFrameHeaderInternal(buf, fragmentReadableBytes, Http2FrameTypes.Continuation, flags, streamId);
                        _ = ctx.WriteAsync(buf, promiseAggregator.NewPromise());
                    }

                    _ = ctx.WriteAsync(fragment, promiseAggregator.NewPromise());
                }
                while (headerBlock.IsReadable());
            }

            return promiseAggregator.Task;
        }

        /// <summary>
        /// Returns the number of padding bytes that should be appended to the end of a frame.
        /// </summary>
        /// <param name="padding"></param>
        [MethodImpl(InlineMethod.AggressiveInlining)]
        static int PaddingBytes(int padding)
        {
            // The padding parameter contains the 1 byte pad length field as well as the trailing padding bytes.
            // Subtract 1, so to only get the number of padding bytes that need to be appended to the end of a frame.
            return padding - 1;
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        static void WritePaddingLength(IByteBuffer buf, int padding)
        {
            if (padding > 0)
            {
                // It is assumed that the padding length has been bounds checked before this
                // Minus 1, as the pad length field is included in the padding parameter and is 1 byte wide.
                _ = buf.WriteByte(padding - 1);
            }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        static void VerifyWeight(short weight)
        {
            if (weight < Http2CodecUtil.MinWeight || weight > Http2CodecUtil.MaxWeight)
            {
                ThrowHelper.ThrowArgumentException_InvalidWeight(weight);
            }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        static void VerifyErrorCode(long errorCode)
        {
            if (errorCode < 0 || errorCode > Http2CodecUtil.MaxUnsignedInt)
            {
                ThrowHelper.ThrowArgumentException_InvalidErrorCode(errorCode);
            }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        static void VerifyWindowSizeIncrement(int windowSizeIncrement)
        {
            if ((uint)windowSizeIncrement > SharedConstants.TooBigOrNegative)
            {
                ThrowHelper.ThrowArgumentException_PositiveOrZero(ExceptionArgument.windowSizeIncrement);
            }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        static void VerifyPingPayload(IByteBuffer data)
        {
            if (data is null || data.ReadableBytes != Http2CodecUtil.PingFramePayloadLength)
            {
                ThrowHelper.ThrowArgumentException_InvalidPingFramePayloadLength();
            }
        }
    }
}