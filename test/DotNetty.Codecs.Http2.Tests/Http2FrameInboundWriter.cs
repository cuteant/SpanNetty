
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;

    /// <summary>
    /// Utility class which allows easy writing of HTTP2 frames via {@link EmbeddedChannel#writeInbound(Object...)}.
    /// </summary>
    sealed class Http2FrameInboundWriter
    {
        private readonly IChannelHandlerContext _ctx;
        private readonly IHttp2FrameWriter _writer;

        public Http2FrameInboundWriter(EmbeddedChannel channel)
            : this(channel, new DefaultHttp2FrameWriter())
        {
        }

        public Http2FrameInboundWriter(EmbeddedChannel channel, IHttp2FrameWriter writer)
        {
            _ctx = new WriteInboundChannelHandlerContext(channel);
            _writer = writer;
        }

        public void WriteInboundData(int streamId, IByteBuffer data, int padding, bool endStream)
        {
            _writer.WriteDataAsync(_ctx, streamId, data, padding, endStream, _ctx.NewPromise()).GetAwaiter().GetResult();
        }


        public void WriteInboundHeaders(int streamId, IHttp2Headers headers, int padding, bool endStream)
        {
            _writer.WriteHeadersAsync(_ctx, streamId, headers, padding, endStream, _ctx.NewPromise()).GetAwaiter().GetResult();
        }

        public void WriteInboundHeaders(int streamId, IHttp2Headers headers, int streamDependency, short weight, bool exclusive, int padding, bool endStream)
        {
            _writer.WriteHeadersAsync(_ctx, streamId, headers, streamDependency,
                   weight, exclusive, padding, endStream, _ctx.NewPromise()).GetAwaiter().GetResult();
        }

        public void WriteInboundPriority(int streamId, int streamDependency, short weight, bool exclusive)
        {
            _writer.WritePriorityAsync(_ctx, streamId, streamDependency, weight,
                   exclusive, _ctx.NewPromise()).GetAwaiter().GetResult();
        }

        public void WriteInboundRstStream(int streamId, Http2Error errorCode)
        {
            _writer.WriteRstStreamAsync(_ctx, streamId, errorCode, _ctx.NewPromise()).GetAwaiter().GetResult();
        }

        public void WriteInboundSettings(Http2Settings settings)
        {
            _writer.WriteSettingsAsync(_ctx, settings, _ctx.NewPromise()).GetAwaiter().GetResult();
        }

        public void WriteInboundSettingsAck()
        {
            _writer.WriteSettingsAckAsync(_ctx, _ctx.NewPromise()).GetAwaiter().GetResult();
        }

        public void WriteInboundPing(bool ack, long data)
        {
            _writer.WritePingAsync(_ctx, ack, data, _ctx.NewPromise()).GetAwaiter().GetResult();
        }

        public void WritePushPromise(int streamId, int promisedStreamId, IHttp2Headers headers, int padding)
        {
            _writer.WritePushPromiseAsync(_ctx, streamId, promisedStreamId,
                   headers, padding, _ctx.NewPromise()).GetAwaiter().GetResult();
        }

        public void WriteInboundGoAway(int lastStreamId, Http2Error errorCode, IByteBuffer debugData)
        {
            _writer.WriteGoAwayAsync(_ctx, lastStreamId, errorCode, debugData, _ctx.NewPromise()).GetAwaiter().GetResult();
        }

        public void WriteInboundWindowUpdate(int streamId, int windowSizeIncrement)
        {
            _writer.WriteWindowUpdateAsync(_ctx, streamId, windowSizeIncrement, _ctx.NewPromise()).GetAwaiter().GetResult();
        }

        public void WriteInboundFrame(Http2FrameTypes frameType, int streamId, Http2Flags flags, IByteBuffer payload)
        {
            _writer.WriteFrameAsync(_ctx, frameType, streamId, flags, payload, _ctx.NewPromise()).GetAwaiter().GetResult();
        }

        sealed class WriteInboundChannelHandlerContext : ChannelHandlerAdapter, IChannelHandlerContext
        {
            private readonly EmbeddedChannel _channel;

            public WriteInboundChannelHandlerContext(EmbeddedChannel channel)
            {
                _channel = channel;
            }

            public IChannel Channel => _channel;

            public IByteBufferAllocator Allocator => _channel.Allocator;

            public IEventExecutor Executor => _channel.EventLoop;

            public string Name => "WriteInbound";

            public IChannelHandler Handler => this;

            public bool Removed => false;
            public bool IsRemoved => false;

            public IChannelPipeline Pipeline => _channel.Pipeline;

            public Task BindAsync(EndPoint localAddress)
            {
                return _channel.BindAsync(localAddress);
            }

            public Task CloseAsync()
            {
                return _channel.CloseAsync();
            }

            public Task CloseAsync(IPromise promise)
            {
                return _channel.CloseAsync(promise);
            }

            public Task ConnectAsync(EndPoint remoteAddress)
            {
                return _channel.ConnectAsync(remoteAddress);
            }

            public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
            {
                return _channel.ConnectAsync(remoteAddress, localAddress);
            }

            public Task DeregisterAsync()
            {
                return _channel.DeregisterAsync();
            }

            public Task DeregisterAsync(IPromise promise)
            {
                return _channel.DeregisterAsync(promise);
            }

            public Task DisconnectAsync()
            {
                return _channel.DisconnectAsync();
            }

            public Task DisconnectAsync(IPromise promise)
            {
                return _channel.DisconnectAsync(promise);
            }

            public IChannelHandlerContext FireChannelActive()
            {
                _channel.Pipeline.FireChannelActive();
                return this;
            }

            public IChannelHandlerContext FireChannelInactive()
            {
                _channel.Pipeline.FireChannelInactive();
                return this;
            }

            public IChannelHandlerContext FireChannelRead(object message)
            {
                _channel.Pipeline.FireChannelRead(message);
                return this;
            }

            public IChannelHandlerContext FireChannelReadComplete()
            {
                _channel.Pipeline.FireChannelReadComplete();
                return this;
            }

            public IChannelHandlerContext FireChannelRegistered()
            {
                _channel.Pipeline.FireChannelRegistered();
                return this;
            }

            public IChannelHandlerContext FireChannelUnregistered()
            {
                _channel.Pipeline.FireChannelUnregistered();
                return this;
            }

            public IChannelHandlerContext FireChannelWritabilityChanged()
            {
                _channel.Pipeline.FireChannelWritabilityChanged();
                return this;
            }

            public IChannelHandlerContext FireExceptionCaught(Exception ex)
            {
                _channel.Pipeline.FireExceptionCaught(ex);
                return this;
            }

            public IChannelHandlerContext FireUserEventTriggered(object evt)
            {
                _channel.Pipeline.FireUserEventTriggered(evt);
                return this;
            }

            public IChannelHandlerContext Flush()
            {
                _channel.Pipeline.FireChannelReadComplete();
                return this;
            }

            public IAttribute<T> GetAttribute<T>(AttributeKey<T> key) where T : class
            {
                return _channel.GetAttribute<T>(key);
            }

            public bool HasAttribute<T>(AttributeKey<T> key) where T : class
            {
                return _channel.HasAttribute<T>(key);
            }

            public IPromise NewPromise()
            {
                return _channel.NewPromise();
            }

            public IPromise NewPromise(object state)
            {
                return _channel.NewPromise(state);
            }

            public IChannelHandlerContext Read()
            {
                _channel.Pipeline.Read();
                return this;
            }

            public IPromise VoidPromise()
            {
                return _channel.VoidPromise();
            }

            public Task WriteAndFlushAsync(object message)
            {
                return _channel.WriteAndFlushAsync(message, NewPromise());
            }

            public Task WriteAndFlushAsync(object message, IPromise promise)
            {
                try
                {
                    _channel.WriteInbound(message);
                    _channel.RunPendingTasks();
                    promise.Complete();
                }
                catch (Exception exc)
                {
                    promise.TrySetException(exc);
                }
                return promise.Task;
            }

            public Task WriteAsync(object message)
            {
                return _channel.WriteAsync(message, NewPromise());
            }

            public Task WriteAsync(object message, IPromise promise)
            {
                return WriteAndFlushAsync(message, promise);
            }
        }
    }
}
