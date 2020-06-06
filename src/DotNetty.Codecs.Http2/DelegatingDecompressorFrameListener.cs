// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics;
    using DotNetty.Buffers;
    using DotNetty.Codecs;
    using DotNetty.Codecs.Compression;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;

    /// <summary>
    /// An HTTP2 frame listener that will decompress data frames according to the <c>content-encoding</c> header for each
    /// stream. The decompression provided by this class will be applied to the data for the entire stream.
    /// </summary>
    public class DelegatingDecompressorFrameListener : Http2FrameListenerDecorator
    {
        private readonly IHttp2Connection _connection;
        private readonly bool _strict;
        private bool _flowControllerInitialized;
        private readonly IHttp2ConnectionPropertyKey _propertyKey;

        public DelegatingDecompressorFrameListener(IHttp2Connection connection, IHttp2FrameListener listener)
            : this(connection, listener, true)
        {
        }

        public DelegatingDecompressorFrameListener(IHttp2Connection connection, IHttp2FrameListener listener, bool strict)
            : base(listener)
        {
            _connection = connection;
            _strict = strict;

            _propertyKey = connection.NewKey();
            _connection.AddListener(new DelegatingConnectionAdapter(this));
        }

        public override int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
        {
            IHttp2Stream stream = _connection.Stream(streamId);
            Http2Decompressor decompressor = Decompressor(stream);
            if (decompressor is null)
            {
                // The decompressor may be null if no compatible encoding type was found in this stream's headers
                return _listener.OnDataRead(ctx, streamId, data, padding, endOfStream);
            }

            EmbeddedChannel channel = decompressor.Decompressor;
            int compressedBytes = data.ReadableBytes + padding;
            decompressor.IncrementCompressedBytes(compressedBytes);
            try
            {
                // call retain here as it will call release after its written to the channel
                channel.WriteInbound(data.Retain());
                var buf = NextReadableBuf(channel);
                if (buf is null && endOfStream && channel.Finish())
                {
                    buf = NextReadableBuf(channel);
                }
                if (buf is null)
                {
                    if (endOfStream)
                    {
                        _listener.OnDataRead(ctx, streamId, Unpooled.Empty, padding, true);
                    }
                    // No new decompressed data was extracted from the compressed data. This means the application could
                    // not be provided with data and thus could not return how many bytes were processed. We will assume
                    // there is more data coming which will complete the decompression block. To allow for more data we
                    // return all bytes to the flow control window (so the peer can send more data).
                    decompressor.IncrementDecompressedBytes(compressedBytes);
                    return compressedBytes;
                }
                try
                {
                    IHttp2LocalFlowController flowController = _connection.Local.FlowController;
                    decompressor.IncrementDecompressedBytes(padding);
                    while (true)
                    {
                        var nextBuf = NextReadableBuf(channel);
                        var decompressedEndOfStream = nextBuf is null && endOfStream;
                        if (decompressedEndOfStream && channel.Finish())
                        {
                            nextBuf = NextReadableBuf(channel);
                            decompressedEndOfStream = nextBuf is null;
                        }

                        decompressor.IncrementDecompressedBytes(buf.ReadableBytes);
                        // Immediately return the bytes back to the flow controller. ConsumedBytesConverter will convert
                        // from the decompressed amount which the user knows about to the compressed amount which flow
                        // control knows about.
                        flowController.ConsumeBytes(stream,
                                _listener.OnDataRead(ctx, streamId, buf, padding, decompressedEndOfStream));
                        if (nextBuf is null)
                        {
                            break;
                        }

                        padding = 0; // Padding is only communicated once on the first iteration.
                        buf.Release();
                        buf = nextBuf;
                    }
                    // We consume bytes each time we call the listener to ensure if multiple frames are decompressed
                    // that the bytes are accounted for immediately. Otherwise the user may see an inconsistent state of
                    // flow control.
                    return 0;
                }
                finally
                {
                    buf.Release();
                }
            }
            catch (Http2Exception)
            {
                throw;
            }
            catch (Exception t)
            {
                return ThrowHelper.ThrowStreamError_DecompressorErrorDetectedWhileDelegatingDataReadOnStream(stream.Id, t);
            }

        }

        public override void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
        {
            InitDecompressor(ctx, streamId, headers, endOfStream);
            _listener.OnHeadersRead(ctx, streamId, headers, padding, endOfStream);
        }

        public override void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
        {
            InitDecompressor(ctx, streamId, headers, endOfStream);
            _listener.OnHeadersRead(ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream);
        }

        /// <summary>
        /// Returns a new <see cref="EmbeddedChannel"/> that decodes the HTTP2 message content encoded in the specified
        /// <paramref name="contentEncoding"/>.
        /// </summary>
        /// <param name="ctx">The context</param>
        /// <param name="contentEncoding">the value of the <c>content-encoding</c> header</param>
        /// <returns>a new <see cref="ByteToMessageDecoder"/> if the specified encoding is supported. <c>null</c> otherwise
        /// (alternatively, you can throw a <see cref="Http2Exception"/> to block unknown encoding).</returns>
        /// <exception cref="Http2Exception">If the specified encoding is not not supported and warrants an exception.</exception>
        protected EmbeddedChannel NewContentDecompressor(IChannelHandlerContext ctx, ICharSequence contentEncoding)
        {
            var channel = ctx.Channel;
            if (HttpHeaderValues.Gzip.ContentEqualsIgnoreCase(contentEncoding) ||
                HttpHeaderValues.XGzip.ContentEqualsIgnoreCase(contentEncoding))
            {
                return new EmbeddedChannel(channel.Id, channel.Metadata.HasDisconnect,
                        channel.Configuration, ZlibCodecFactory.NewZlibDecoder(ZlibWrapper.Gzip));
            }
            if (HttpHeaderValues.Deflate.ContentEqualsIgnoreCase(contentEncoding) ||
                HttpHeaderValues.XDeflate.ContentEqualsIgnoreCase(contentEncoding))
            {
                ZlibWrapper wrapper = _strict ? ZlibWrapper.Zlib : ZlibWrapper.ZlibOrNone;
                // To be strict, 'deflate' means ZLIB, but some servers were not implemented correctly.
                return new EmbeddedChannel(channel.Id, channel.Metadata.HasDisconnect,
                        channel.Configuration, ZlibCodecFactory.NewZlibDecoder(wrapper));
            }
            // 'identity' or unsupported
            return null;
        }

        /// <summary>
        /// Returns the expected content encoding of the decoded content. This getMethod returns <c>"identity"</c> by
        /// default, which is the case for most decompressors.
        /// </summary>
        /// <param name="contentEncoding">the value of the <c>content-encoding</c> header</param>
        /// <returns>the expected content encoding of the new content.</returns>
        /// <exception cref="Http2Exception">if the <paramref name="contentEncoding"/> is not supported and warrants an exception</exception>
        protected virtual ICharSequence GetTargetContentEncoding(ICharSequence contentEncoding)
        {
            return HttpHeaderValues.Identity;
        }


        /// <summary>
        /// Checks if a new decompressor object is needed for the stream identified by <paramref name="streamId"/>.
        /// This method will modify the <c>content-encoding</c> header contained in <paramref name="headers"/>.
        /// </summary>
        /// <param name="ctx">The context</param>
        /// <param name="streamId">The identifier for the headers inside <paramref name="headers"/></param>
        /// <param name="headers">Object representing headers which have been read</param>
        /// <param name="endOfStream">Indicates if the stream has ended</param>
        /// <exception cref="Http2Exception">If the <c>content-encoding</c> is not supported</exception>
        private void InitDecompressor(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, bool endOfStream)
        {
            var stream = _connection.Stream(streamId);
            if (stream is null) { return; }

            Http2Decompressor decompressor = Decompressor(stream);
            if (decompressor is null && !endOfStream)
            {
                // Determine the content encoding.
                if (!headers.TryGet(HttpHeaderNames.ContentEncoding, out var contentEncoding))
                {
                    contentEncoding = HttpHeaderValues.Identity;
                }
                EmbeddedChannel channel = NewContentDecompressor(ctx, contentEncoding);
                if (channel is object)
                {
                    decompressor = new Http2Decompressor(channel);
                    stream.SetProperty(_propertyKey, decompressor);
                    // Decode the content and remove or replace the existing headers
                    // so that the message looks like a decoded message.
                    var targetContentEncoding = GetTargetContentEncoding(contentEncoding);
                    if (HttpHeaderValues.Identity.ContentEqualsIgnoreCase(targetContentEncoding))
                    {
                        headers.Remove(HttpHeaderNames.ContentEncoding);
                    }
                    else
                    {
                        headers.Set(HttpHeaderNames.ContentEncoding, targetContentEncoding);
                    }
                }
            }

            if (decompressor is object)
            {
                // The content length will be for the compressed data. Since we will decompress the data
                // this content-length will not be correct. Instead of queuing messages or delaying sending
                // header frames...just remove the content-length header
                headers.Remove(HttpHeaderNames.ContentLength);

                // The first time that we initialize a decompressor, decorate the local flow controller to
                // properly convert consumed bytes.
                if (!_flowControllerInitialized)
                {
                    _flowControllerInitialized = true;
                    var localEndpoint = _connection.Local;
                    localEndpoint.FlowController = new ConsumedBytesConverter(this, localEndpoint.FlowController);
                }
            }
        }

        Http2Decompressor Decompressor(IHttp2Stream stream)
        {
            return stream?.GetProperty<Http2Decompressor>(_propertyKey);
        }

        /// <summary>
        /// Release remaining content from the <see cref="EmbeddedChannel"/>.
        /// </summary>
        /// <param name="decompressor">The decompressor for <c>stream</c></param>
        private static void Cleanup(Http2Decompressor decompressor)
        {
            decompressor.Decompressor.FinishAndReleaseAll();
        }

        /// <summary>
        /// Read the next decompressed <see cref="IByteBuffer"/> from the <see cref="EmbeddedChannel"/>
        /// or <c>null</c> if one does not exist.
        /// </summary>
        /// <param name="decompressor">The channel to read from</param>
        /// <returns>The next decoded <see cref="IByteBuffer"/> from the <see cref="EmbeddedChannel"/> or <c>null</c> if one does not exist</returns>
        private static IByteBuffer NextReadableBuf(EmbeddedChannel decompressor)
        {
            while (true)
            {
                var buf = decompressor.ReadInbound<IByteBuffer>();
                if (buf is null) { return null; }
                if (!buf.IsReadable())
                {
                    buf.Release();
                    continue;
                }
                return buf;
            }
        }

        sealed class DelegatingConnectionAdapter : Http2ConnectionAdapter
        {
            readonly DelegatingDecompressorFrameListener _frameListener;

            public DelegatingConnectionAdapter(DelegatingDecompressorFrameListener frameListener)
            {
                _frameListener = frameListener;
            }

            public override void OnStreamRemoved(IHttp2Stream stream)
            {
                Http2Decompressor decompressor = _frameListener.Decompressor(stream);
                if (decompressor is object)
                {
                    Cleanup(decompressor);
                }
            }
        }

        /// <summary>
        /// A decorator around the local flow controller that converts consumed bytes from uncompressed to compressed.
        /// </summary>
        sealed class ConsumedBytesConverter : IHttp2LocalFlowController
        {
            readonly IHttp2LocalFlowController _flowController;
            readonly DelegatingDecompressorFrameListener _frameListener;

            public ConsumedBytesConverter(DelegatingDecompressorFrameListener frameListener, IHttp2LocalFlowController flowController)
            {
                if (flowController is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.flowController); }
                _flowController = flowController;
                _frameListener = frameListener;
            }

            public void SetChannelHandlerContext(IChannelHandlerContext ctx)
            {
                _flowController.SetChannelHandlerContext(ctx);
            }

            public bool ConsumeBytes(IHttp2Stream stream, int numBytes)
            {
                Http2Decompressor decompressor = _frameListener.Decompressor(stream);
                if (decompressor is object)
                {
                    // Convert the decompressed bytes to compressed (on the wire) bytes.
                    numBytes = decompressor.ConsumeBytes(stream.Id, numBytes);
                }
                try
                {
                    return _flowController.ConsumeBytes(stream, numBytes);
                }
                catch (Http2Exception)
                {
                    throw;
                }
                catch (Exception t)
                {
                    // The stream should be closed at this point. We have already changed our state tracking the compressed
                    // bytes, and there is no guarantee we can recover if the underlying flow controller throws.
                    return ThrowHelper.ThrowStreamError_ErrorWhileReturningBytesToFlowControlWindow(stream.Id, t);
                }
            }

            public IHttp2LocalFlowController FrameWriter(IHttp2FrameWriter frameWriter)
            {
                return _flowController.FrameWriter(frameWriter);
            }

            public void IncrementWindowSize(IHttp2Stream stream, int delta)
            {
                _flowController.IncrementWindowSize(stream, delta);
            }

            public int GetInitialWindowSize(IHttp2Stream stream)
            {
                return _flowController.GetInitialWindowSize(stream);
            }

            public void SetInitialWindowSize(int newWindowSize)
            {
                _flowController.SetInitialWindowSize(newWindowSize);
            }

            public int InitialWindowSize => _flowController.InitialWindowSize;

            public void ReceiveFlowControlledFrame(IHttp2Stream stream, IByteBuffer data, int padding, bool endOfStream)
            {
                _flowController.ReceiveFlowControlledFrame(stream, data, padding, endOfStream);
            }

            public int UnconsumedBytes(IHttp2Stream stream)
            {
                return _flowController.UnconsumedBytes(stream);
            }

            public int GetWindowSize(IHttp2Stream stream)
            {
                return _flowController.GetWindowSize(stream);
            }
        }

        /// <summary>
        /// Provides the state for stream <c>DATA</c> frame decompression.
        /// </summary>
        sealed class Http2Decompressor
        {
            private readonly EmbeddedChannel _decompressor;
            private int _compressed;
            private int _decompressed;

            public Http2Decompressor(EmbeddedChannel decompressor)
            {
                _decompressor = decompressor;
            }

            /// <summary>
            /// Responsible for taking compressed bytes in and producing decompressed bytes.
            /// </summary>
            public EmbeddedChannel Decompressor => _decompressor;

            /// <summary>
            /// Increment the number of bytes received prior to doing any decompression.
            /// </summary>
            /// <param name="delta"></param>
            public void IncrementCompressedBytes(int delta)
            {
                Debug.Assert(delta >= 0);
                _compressed += delta;
            }

            /// <summary>
            /// Increment the number of bytes after the decompression process.
            /// </summary>
            /// <param name="delta"></param>
            public void IncrementDecompressedBytes(int delta)
            {
                Debug.Assert(delta >= 0);
                _decompressed += delta;
            }

            /// <summary>
            /// Determines the ratio between {@code numBytes} and <see cref="Http2Decompressor._decompressed"/>.
            /// This ratio is used to decrement <see cref="Http2Decompressor._decompressed"/> and
            /// <see cref="Http2Decompressor._compressed"/>.
            /// </summary>
            /// <param name="streamId">the stream ID</param>
            /// <param name="decompressedBytes">The number of post-decompressed bytes to return to flow control</param>
            /// <returns>The number of pre-decompressed bytes that have been consumed.</returns>
            public int ConsumeBytes(int streamId, int decompressedBytes)
            {
                if ((uint)decompressedBytes > SharedConstants.TooBigOrNegative)
                {
                    ThrowHelper.ThrowArgumentException_PositiveOrZero(decompressedBytes, ExceptionArgument.decompressedBytes);
                }
                if ((uint)(_decompressed - decompressedBytes) > SharedConstants.TooBigOrNegative)
                {
                    ThrowHelper.ThrowStreamError_AttemptingToReturnTooManyBytesForStream(
                        streamId, _decompressed, decompressedBytes);
                }
                double consumedRatio = decompressedBytes / (double)_decompressed;
                int consumedCompressed = Math.Min(_compressed, (int)Math.Ceiling(_compressed * consumedRatio));
                if ((uint)(_compressed - consumedCompressed) > SharedConstants.TooBigOrNegative)
                {
                    ThrowHelper.ThrowStreamError_OverflowWhenConvertingDecompressedBytesToCompressedBytesForStream(
                        streamId, decompressedBytes, _decompressed, _compressed, consumedCompressed);
                }
                _decompressed -= decompressedBytes;
                _compressed -= consumedCompressed;

                return consumedCompressed;
            }
        }
    }
}
