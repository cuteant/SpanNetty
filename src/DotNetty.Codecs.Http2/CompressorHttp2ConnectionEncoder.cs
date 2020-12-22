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
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Compression;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;

    public class CompressorHttp2ConnectionEncoder : DecoratingHttp2ConnectionEncoder
    {
        public const int DefaultCompressionLevel = 6;
        public const int DefaultWindowBits = 15;
        public const int DefaultMemLevel = 8;

        private readonly int _compressionLevel;
        private readonly int _windowBits;
        private readonly int _memLevel;
        private readonly IHttp2ConnectionPropertyKey _propertyKey;

        public CompressorHttp2ConnectionEncoder(IHttp2ConnectionEncoder encoder)
            : this(encoder, DefaultCompressionLevel, DefaultWindowBits, DefaultMemLevel)
        {
        }

        public CompressorHttp2ConnectionEncoder(IHttp2ConnectionEncoder encoder, int compressionLevel, int windowBits, int memLevel)
            : base(encoder)
        {
            if (compressionLevel < 0 || compressionLevel > 9)
            {
                ThrowHelper.ThrowArgumentException_InvalidCompressionLevel(compressionLevel);
            }
            if (windowBits < 9 || windowBits > 15)
            {
                ThrowHelper.ThrowArgumentException_InvalidWindowBits(windowBits);
            }
            if (memLevel < 1 || memLevel > 9)
            {
                ThrowHelper.ThrowArgumentException_InvalidMemLevel(memLevel);
            }
            _compressionLevel = compressionLevel;
            _windowBits = windowBits;
            _memLevel = memLevel;

            var connect = Connection;
            _propertyKey = connect.NewKey();
            connect.AddListener(new DelegatingConnectionAdapter(this));
        }

        public override Task WriteDataAsync(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream, IPromise promise)
        {
            IHttp2Stream stream = Connection.Stream(streamId);
            EmbeddedChannel channel = stream?.GetProperty<EmbeddedChannel>(_propertyKey);
            if (channel is null)
            {
                // The compressor may be null if no compatible encoding type was found in this stream's headers
                return base.WriteDataAsync(ctx, streamId, data, padding, endOfStream, promise);
            }

            try
            {
                // The channel will release the buffer after being written
                _ = channel.WriteOutbound(data);
                var buf = NextReadableBuf(channel);
                if (buf is null)
                {
                    if (endOfStream)
                    {
                        if (channel.Finish())
                        {
                            buf = NextReadableBuf(channel);
                        }
                        return base.WriteDataAsync(ctx, streamId, buf ?? Unpooled.Empty, padding,
                                true, promise);
                    }
                    // END_STREAM is not set and the assumption is data is still forthcoming.
                    promise.Complete();
                    return promise.Task;
                }

                PromiseCombiner combiner = new PromiseCombiner(ctx.Executor);
                while (true)
                {
                    var nextBuf = NextReadableBuf(channel);
                    var compressedEndOfStream = nextBuf is null && endOfStream;
                    if (compressedEndOfStream && channel.Finish())
                    {
                        nextBuf = NextReadableBuf(channel);
                        compressedEndOfStream = nextBuf is null;
                    }

                    var bufPromise = ctx.NewPromise();
                    combiner.Add(bufPromise.Task);
                    _ = base.WriteDataAsync(ctx, streamId, buf, padding, compressedEndOfStream, bufPromise);

                    if (nextBuf is null) { break; }

                    padding = 0; // Padding is only communicated once on the first iteration
                    buf = nextBuf;
                }
                combiner.Finish(promise);
            }
            catch (Exception cause)
            {
                _ = promise.TrySetException(cause);
            }
            finally
            {
                if (endOfStream)
                {
                    Cleanup(stream, channel);
                }
            }

            return promise.Task;
        }

        public override Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream, IPromise promise)
        {
            try
            {
                // Determine if compression is required and sanitize the headers.
                EmbeddedChannel compressor = NewCompressor(ctx, headers, endOfStream);

                // Write the headers and create the stream object.
                var future = base.WriteHeadersAsync(ctx, streamId, headers, padding, endOfStream, promise);

                // After the stream object has been created, then attach the compressor as a property for data compression.
                BindCompressorToStream(compressor, streamId);

                return future;
            }
            catch (Exception e)
            {
                _ = promise.TrySetException(e);
            }
            return promise.Task;
        }

        public override Task WriteHeadersAsync(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream, IPromise promise)
        {
            try
            {
                // Determine if compression is required and sanitize the headers.
                EmbeddedChannel compressor = NewCompressor(ctx, headers, endOfStream);

                // Write the headers and create the stream object.
                var future = base.WriteHeadersAsync(ctx, streamId, headers, streamDependency, weight, exclusive,
                    padding, endOfStream, promise);

                // After the stream object has been created, then attach the compressor as a property for data compression.
                BindCompressorToStream(compressor, streamId);

                return future;
            }
            catch (Exception e)
            {
                _ = promise.TrySetException(e);
            }
            return promise.Task;
        }

        /// <summary>
        /// Returns a new <see cref="EmbeddedChannel"/> that encodes the HTTP2 message content encoded in the specified
        /// <paramref name="contentEncoding"/>.
        /// </summary>
        /// <param name="ctx">the context.</param>
        /// <param name="contentEncoding">the value of the <c>content-encoding</c> header</param>
        /// <returns>a new <see cref="ByteToMessageDecoder"/> if the specified encoding is supported. <c>null</c> otherwise
        /// (alternatively, you can throw a <see cref="Http2Exception"/> to block unknown encoding).</returns>
        /// <exception cref="Http2Exception">If the specified encoding is not not supported and warrants an exception.</exception>
        protected virtual EmbeddedChannel NewContentCompressor(IChannelHandlerContext ctx, ICharSequence contentEncoding)
        {
            if (HttpHeaderValues.Gzip.ContentEqualsIgnoreCase(contentEncoding) ||
                HttpHeaderValues.XGzip.ContentEqualsIgnoreCase(contentEncoding))
            {
                return NewCompressionChannel(ctx, ZlibWrapper.Gzip);
            }
            if (HttpHeaderValues.Deflate.ContentEqualsIgnoreCase(contentEncoding) ||
                HttpHeaderValues.XDeflate.ContentEqualsIgnoreCase(contentEncoding))
            {
                return NewCompressionChannel(ctx, ZlibWrapper.Zlib);
            }
            // 'identity' or unsupported
            return null;
        }

        /// <summary>
        /// Returns the expected content encoding of the decoded content. Returning <paramref name="contentEncoding"/> is the default
        /// behavior, which is the case for most compressors.
        /// </summary>
        /// <param name="contentEncoding">the value of the <c>content-encoding</c> header</param>
        /// <returns>the expected content encoding of the new content.</returns>
        /// <exception cref="Http2Exception">if the <paramref name="contentEncoding"/> is not supported and warrants an exception</exception>
        protected virtual ICharSequence GetTargetContentEncoding(ICharSequence contentEncoding)
        {
            return contentEncoding;
        }

        /// <summary>
        /// Generate a new instance of an <see cref="EmbeddedChannel"/> capable of compressing data
        /// </summary>
        /// <param name="ctx">the context.</param>
        /// <param name="wrapper">Defines what type of encoder should be used</param>
        /// <returns></returns>
        private EmbeddedChannel NewCompressionChannel(IChannelHandlerContext ctx, ZlibWrapper wrapper)
        {
            var channel = ctx.Channel;
            return new EmbeddedChannel(channel.Id, channel.Metadata.HasDisconnect,
                channel.Configuration, ZlibCodecFactory.NewZlibEncoder(wrapper, _compressionLevel, _windowBits,
                _memLevel));
        }


        /// <summary>
        /// Checks if a new compressor object is needed for the stream identified by <c>streamId</c>. This method will
        /// modify the <c>content-encoding</c> header contained in <paramref name="headers"/>.
        /// </summary>
        /// <param name="ctx">the context.</param>
        /// <param name="headers">Object representing headers which are to be written</param>
        /// <param name="endOfStream">Indicates if the stream has ended</param>
        /// <exception cref="Http2Exception">if any problems occur during initialization.</exception>
        /// <returns>The channel used to compress data.</returns>
        private EmbeddedChannel NewCompressor(IChannelHandlerContext ctx, IHttp2Headers headers, bool endOfStream)
        {
            if (endOfStream) { return null; }

            
            if (!headers.TryGet(HttpHeaderNames.ContentEncoding, out var encoding))
            {
                encoding = HttpHeaderValues.Identity;
            }
            var compressor = NewContentCompressor(ctx, encoding);
            if (compressor is object)
            {
                var targetContentEncoding = GetTargetContentEncoding(encoding);
                if (HttpHeaderValues.Identity.ContentEqualsIgnoreCase(targetContentEncoding))
                {
                    _ = headers.Remove(HttpHeaderNames.ContentEncoding);
                }
                else
                {
                    _ = headers.Set(HttpHeaderNames.ContentEncoding, targetContentEncoding);
                }

                // The content length will be for the decompressed data. Since we will compress the data
                // this content-length will not be correct. Instead of queuing messages or delaying sending
                // header frames...just remove the content-length header
                _ = headers.Remove(HttpHeaderNames.ContentLength);
            }

            return compressor;
        }


        /// <summary>
        /// Called after the super class has written the headers and created any associated stream objects.
        /// </summary>
        /// <param name="compressor">The compressor associated with the stream identified by <paramref name="streamId"/>.</param>
        /// <param name="streamId">The stream id for which the headers were written.</param>
        private void BindCompressorToStream(EmbeddedChannel compressor, int streamId)
        {
            if (compressor is object)
            {
                var stream = Connection.Stream(streamId);
                if (stream is object)
                {
                    _ = stream.SetProperty(_propertyKey, compressor);
                }
            }
        }

        /// <summary>
        /// Release remaining content from <see cref="EmbeddedChannel"/> and remove the compressor from the <see cref="IHttp2Stream"/>.
        /// </summary>
        /// <param name="stream">The stream for which <paramref name="compressor"/> is the compressor for</param>
        /// <param name="compressor">The compressor for <paramref name="stream"/></param>
        void Cleanup(IHttp2Stream stream, EmbeddedChannel compressor)
        {
            _ = compressor.FinishAndReleaseAll();
            _ = stream.RemoveProperty(_propertyKey);
        }

        /// <summary>
        /// Read the next compressed <see cref="IByteBuffer"/> from the <see cref="EmbeddedChannel"/> or <c>null</c> if one does not exist.
        /// </summary>
        /// <param name="compressor">The channel to read from</param>
        /// <returns>The next decoded <see cref="IByteBuffer"/> from the <see cref="EmbeddedChannel"/> or <c>null</c> if one does not exist</returns>
        private static IByteBuffer NextReadableBuf(EmbeddedChannel compressor)
        {
            while (true)
            {
                var buf = compressor.ReadOutbound<IByteBuffer>();
                if (buf is null) { return null; }
                if (!buf.IsReadable())
                {
                    _ = buf.Release();
                    continue;
                }
                return buf;
            }
        }

        sealed class DelegatingConnectionAdapter : Http2ConnectionAdapter
        {
            readonly CompressorHttp2ConnectionEncoder _encoder;

            public DelegatingConnectionAdapter(CompressorHttp2ConnectionEncoder encoder) => _encoder = encoder;

            public override void OnStreamRemoved(IHttp2Stream stream)
            {
                var compressor = stream.GetProperty<EmbeddedChannel>(_encoder._propertyKey);
                if (compressor is object)
                {
                    _encoder.Cleanup(stream, compressor);
                }
            }
        }
    }
}
