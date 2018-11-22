
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /**
     * Interface that allows for running a operation that throws a {@link Http2Exception}.
     */
    public interface IHttp2Runnable
    {
        void Run();
    }

    /**
     * Utilities for the integration tests.
     */
    public static class Http2TestUtil
    {
        /**
         * Runs the given operation within the event loop thread of the given {@link Channel}.
         */
        public static void RunInChannel(IChannel channel, IHttp2Runnable runnable)
        {
            channel.EventLoop.Execute(() =>
            {
                try
                {
                    runnable.Run();
                }
                catch (Http2Exception ex)
                {
                    throw new Http2RuntimeException(ex.Message, ex);
                }
            });
        }
        public static void RunInChannel(IChannel channel, Action action)
        {
            channel.EventLoop.Execute(() =>
            {
                try
                {
                    action();
                }
                catch (Http2Exception ex)
                {
                    throw new Http2RuntimeException(ex.Message, ex);
                }
            });
        }

        /**
         * Returns a byte array filled with random data.
         */
        public static byte[] RandomBytes()
        {
            return RandomBytes(100);
        }

        /**
         * Returns a byte array filled with random data.
         */
        public static byte[] RandomBytes(int size)
        {
            byte[] data = new byte[size];
            new Random().NextBytes(data);
            return data;
        }

        /**
         * Returns an {@link AsciiString} that wraps a randomly-filled byte array.
         */
        public static AsciiString RandomString()
        {
            return new AsciiString(RandomBytes(), false);
        }

        public static ICharSequence Of(string s)
        {
            return new StringCharSequence(s);
        }

        internal static HpackEncoder NewTestEncoder()
        {
            try
            {
                return NewTestEncoder(true, Http2CodecUtil.MaxHeaderListSize, Http2CodecUtil.MaxHeaderTableSize);
            }
            catch (Http2Exception e)
            {
                throw new Exception("max size not allowed?", e);
            }
        }

        internal static HpackEncoder NewTestEncoder(bool ignoreMaxHeaderListSize, long maxHeaderListSize, long maxHeaderTableSize)
        {
            HpackEncoder hpackEncoder = new HpackEncoder();
            var buf = Unpooled.Buffer();
            try
            {
                hpackEncoder.SetMaxHeaderTableSize(buf, maxHeaderTableSize);
                hpackEncoder.SetMaxHeaderListSize(maxHeaderListSize);
            }
            finally
            {
                buf.Release();
            }
            return hpackEncoder;
        }

        internal static HpackDecoder NewTestDecoder()
        {
            try
            {
                return NewTestDecoder(Http2CodecUtil.MaxHeaderListSize, Http2CodecUtil.MaxHeaderTableSize);
            }
            catch (Http2Exception e)
            {
                throw new Exception("max size not allowed?", e);
            }
        }

        internal static HpackDecoder NewTestDecoder(long maxHeaderListSize, long maxHeaderTableSize)
        {
            HpackDecoder hpackDecoder = new HpackDecoder(maxHeaderListSize, 32);
            hpackDecoder.SetMaxHeaderTableSize(maxHeaderTableSize);
            return hpackDecoder;
        }

        class FrameAdapter : ByteToMessageDecoder
        {
            private readonly IHttp2Connection connection;
            private readonly IHttp2FrameListener listener;
            private readonly DefaultHttp2FrameReader reader;
            private readonly CountdownEvent latch;

            public FrameAdapter(IHttp2FrameListener listener, CountdownEvent latch)
                : this(null, listener, latch)
            {
            }

            public FrameAdapter(IHttp2Connection connection, IHttp2FrameListener listener, CountdownEvent latch)
                : this(connection, new DefaultHttp2FrameReader(false), listener, latch)
            {
            }

            public FrameAdapter(IHttp2Connection connection, DefaultHttp2FrameReader reader, IHttp2FrameListener listener,
                CountdownEvent latch)
            {
                this.connection = connection;
                this.listener = listener;
                this.reader = reader;
                this.latch = latch;
            }

            IHttp2Stream GetOrCreateStream(int streamId, bool halfClosed)
            {
                return GetOrCreateStream(this.connection, streamId, halfClosed);
            }

            public static IHttp2Stream GetOrCreateStream(IHttp2Connection connection, int streamId, bool halfClosed)
            {
                if (connection != null)
                {
                    var stream = connection.Stream(streamId);
                    if (stream == null)
                    {
                        if (connection.IsServer && streamId % 2 == 0 || !connection.IsServer && streamId % 2 != 0)
                        {
                            stream = connection.Local.CreateStream(streamId, halfClosed);
                        }
                        else
                        {
                            stream = connection.Remote.CreateStream(streamId, halfClosed);
                        }
                    }
                    return stream;
                }
                return null;
            }

            private void CloseStream(IHttp2Stream stream)
            {
                CloseStream(stream, false);
            }

            protected void CloseStream(IHttp2Stream stream, bool dataRead)
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }

            protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
            {
                this.reader.ReadFrame(context, input, new TempHttp2FrameListener(this, this.listener, this.latch));
            }

            sealed class TempHttp2FrameListener : IHttp2FrameListener
            {
                private readonly FrameAdapter frameAdapter;
                private readonly IHttp2FrameListener listener;
                private readonly CountdownEvent latch;

                public TempHttp2FrameListener(FrameAdapter frameAdapter, IHttp2FrameListener listener, CountdownEvent latch)
                {
                    this.frameAdapter = frameAdapter;
                    this.listener = listener;
                    this.latch = latch;
                }

                public int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
                {
                    var stream = this.frameAdapter.GetOrCreateStream(streamId, endOfStream);
                    int processed = listener.OnDataRead(ctx, streamId, data, padding, endOfStream);
                    if (endOfStream)
                    {
                        this.frameAdapter.CloseStream(stream, true);
                    }
                    this.latch.SafeSignal();
                    return processed;
                }

                public void OnGoAwayRead(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
                {
                    this.listener.OnGoAwayRead(ctx, lastStreamId, errorCode, debugData);
                    this.latch.SafeSignal();
                }

                public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
                {
                    var stream = this.frameAdapter.GetOrCreateStream(streamId, endOfStream);
                    this.listener.OnHeadersRead(ctx, streamId, headers, padding, endOfStream);
                    if (endOfStream)
                    {
                        this.frameAdapter.CloseStream(stream);
                    }
                    this.latch.SafeSignal();
                }

                public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
                {
                    var stream = this.frameAdapter.GetOrCreateStream(streamId, endOfStream);
                    this.listener.OnHeadersRead(ctx, streamId, headers, streamDependency, weight, exclusive, padding,
                            endOfStream);
                    if (endOfStream)
                    {
                        this.frameAdapter.CloseStream(stream);
                    }
                    this.latch.SafeSignal();
                }

                public void OnPingAckRead(IChannelHandlerContext ctx, long data)
                {
                    this.listener.OnPingAckRead(ctx, data);
                    this.latch.SafeSignal();
                }

                public void OnPingRead(IChannelHandlerContext ctx, long data)
                {
                    this.listener.OnPingRead(ctx, data);
                    this.latch.SafeSignal();
                }

                public void OnPriorityRead(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive)
                {
                    this.listener.OnPriorityRead(ctx, streamId, streamDependency, weight, exclusive);
                    this.latch.SafeSignal();
                }

                public void OnPushPromiseRead(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding)
                {
                    this.frameAdapter.GetOrCreateStream(promisedStreamId, false);
                    this.listener.OnPushPromiseRead(ctx, streamId, promisedStreamId, headers, padding);
                    this.latch.SafeSignal();
                }

                public void OnRstStreamRead(IChannelHandlerContext ctx, int streamId, Http2Error errorCode)
                {
                    var stream = this.frameAdapter.GetOrCreateStream(streamId, false);
                    this.listener.OnRstStreamRead(ctx, streamId, errorCode);
                    this.frameAdapter.CloseStream(stream);
                    this.latch.SafeSignal();
                }

                public void OnSettingsAckRead(IChannelHandlerContext ctx)
                {
                    this.listener.OnSettingsAckRead(ctx);
                    this.latch.SafeSignal();
                }

                public void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings)
                {
                    this.listener.OnSettingsRead(ctx, settings);
                    this.latch.SafeSignal();
                }

                public void OnUnknownFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload)
                {
                    this.listener.OnUnknownFrame(ctx, frameType, streamId, flags, payload);
                    this.latch.SafeSignal();
                }

                public void OnWindowUpdateRead(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement)
                {
                    this.frameAdapter.GetOrCreateStream(streamId, false);
                    this.listener.OnWindowUpdateRead(ctx, streamId, windowSizeIncrement);
                    this.latch.SafeSignal();
                }
            }
        }

        /**
         * A decorator around a {@link Http2FrameListener} that counts down the latch so that we can await the completion of
         * the request.
         */
        internal sealed class FrameCountDown : IHttp2FrameListener
        {
            private readonly IHttp2FrameListener listener;
            private readonly CountdownEvent messageLatch;
            private readonly CountdownEvent settingsAckLatch;
            private readonly CountdownEvent dataLatch;
            private readonly CountdownEvent trailersLatch;
            private readonly CountdownEvent goAwayLatch;

            public FrameCountDown(IHttp2FrameListener listener, CountdownEvent settingsAckLatch, CountdownEvent messageLatch)
                : this(listener, settingsAckLatch, messageLatch, null, null)
            {
            }

            public FrameCountDown(IHttp2FrameListener listener, CountdownEvent settingsAckLatch, CountdownEvent messageLatch,
                CountdownEvent dataLatch, CountdownEvent trailersLatch)
                : this(listener, settingsAckLatch, messageLatch, dataLatch, trailersLatch, messageLatch)
            {
            }

            public FrameCountDown(IHttp2FrameListener listener, CountdownEvent settingsAckLatch, CountdownEvent messageLatch,
                CountdownEvent dataLatch, CountdownEvent trailersLatch, CountdownEvent goAwayLatch)
            {
                this.listener = listener;
                this.messageLatch = messageLatch;
                this.settingsAckLatch = settingsAckLatch;
                this.dataLatch = dataLatch;
                this.trailersLatch = trailersLatch;
                this.goAwayLatch = goAwayLatch;
            }

            public int OnDataRead(IChannelHandlerContext ctx, int streamId, IByteBuffer data, int padding, bool endOfStream)
            {
                int numBytes = data.ReadableBytes;
                int processed = this.listener.OnDataRead(ctx, streamId, data, padding, endOfStream);
                this.messageLatch.SafeSignal();
                if (this.dataLatch != null)
                {
                    for (int i = 0; i < numBytes; ++i)
                    {
                        this.dataLatch.SafeSignal();
                    }
                }
                return processed;
            }

            public void OnGoAwayRead(IChannelHandlerContext ctx, int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
            {
                this.listener.OnGoAwayRead(ctx, lastStreamId, errorCode, debugData);
                this.goAwayLatch.SafeSignal();
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int padding, bool endOfStream)
            {
                this.listener.OnHeadersRead(ctx, streamId, headers, padding, endOfStream);
                this.messageLatch.SafeSignal();
                if (this.trailersLatch != null && endOfStream)
                {
                    this.trailersLatch.SafeSignal();
                }
            }

            public void OnHeadersRead(IChannelHandlerContext ctx, int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endOfStream)
            {
                this.listener.OnHeadersRead(ctx, streamId, headers, streamDependency, weight, exclusive, padding, endOfStream);
                this.messageLatch.SafeSignal();
                if (this.trailersLatch != null && endOfStream)
                {
                    this.trailersLatch.SafeSignal();
                }
            }

            public void OnPingAckRead(IChannelHandlerContext ctx, long data)
            {
                this.listener.OnPingAckRead(ctx, data);
                this.messageLatch.SafeSignal();
            }

            public void OnPingRead(IChannelHandlerContext ctx, long data)
            {
                this.listener.OnPingRead(ctx, data);
                this.messageLatch.SafeSignal();
            }

            public void OnPriorityRead(IChannelHandlerContext ctx, int streamId, int streamDependency, short weight, bool exclusive)
            {
                this.listener.OnPriorityRead(ctx, streamId, streamDependency, weight, exclusive);
                this.messageLatch.SafeSignal();
            }

            public void OnPushPromiseRead(IChannelHandlerContext ctx, int streamId, int promisedStreamId, IHttp2Headers headers, int padding)
            {
                this.listener.OnPushPromiseRead(ctx, streamId, promisedStreamId, headers, padding);
                this.messageLatch.SafeSignal();
            }

            public void OnRstStreamRead(IChannelHandlerContext ctx, int streamId, Http2Error errorCode)
            {
                this.listener.OnRstStreamRead(ctx, streamId, errorCode);
                this.messageLatch.SafeSignal();
            }

            public void OnSettingsAckRead(IChannelHandlerContext ctx)
            {
                this.listener.OnSettingsAckRead(ctx);
                this.settingsAckLatch.SafeSignal();
            }

            public void OnSettingsRead(IChannelHandlerContext ctx, Http2Settings settings)
            {
                this.listener.OnSettingsRead(ctx, settings);
                this.messageLatch.SafeSignal();
            }

            public void OnUnknownFrame(IChannelHandlerContext ctx, Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload)
            {
                this.listener.OnUnknownFrame(ctx, frameType, streamId, flags, payload);
                this.messageLatch.SafeSignal();
            }

            public void OnWindowUpdateRead(IChannelHandlerContext ctx, int streamId, int windowSizeIncrement)
            {
                this.listener.OnWindowUpdateRead(ctx, streamId, windowSizeIncrement);
                this.messageLatch.SafeSignal();
            }
        }

        public static IPromise NewVoidPromise(IChannel channel)
        {
            return new VoidChannelPromise(channel, true);
        }

        internal sealed class TestStreamByteDistributorStreamState : IStreamByteDistributorStreamState
        {
            readonly IHttp2Stream stream;
            internal bool isWriteAllowed;
            internal long pendingBytes;
            internal bool hasFrame;

            public TestStreamByteDistributorStreamState(IHttp2Stream stream, long pendingBytes,
                bool hasFrame, bool isWriteAllowed)
            {
                this.stream = stream;
                this.isWriteAllowed = isWriteAllowed;
                this.pendingBytes = pendingBytes;
                this.hasFrame = hasFrame;
            }

            public IHttp2Stream Stream => this.stream;

            public long PendingBytes => this.pendingBytes;

            public bool HasFrame => this.hasFrame;

            public int WindowSize => this.isWriteAllowed ? (int)Math.Min(pendingBytes, int.MaxValue) : -1;
        }
    }
}
