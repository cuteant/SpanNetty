namespace Http2Helloworld.Client
{
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http2;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Process <see cref="IFullHttpResponse"/> translated from HTTP/2 frames
    /// </summary>
    public class HttpResponseHandler : SimpleChannelInboundHandler2<IFullHttpResponse>
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<HttpResponseHandler>();

        readonly ConcurrentDictionary<int, KeyValuePair<Task, IPromise>> streamidPromiseMap;

        public HttpResponseHandler()
        {
            // Use a concurrent map because we add and iterate from the main thread (just for the purposes of the example),
            // but Netty also does a get on the map when messages are received in a EventLoop thread.
            this.streamidPromiseMap = new ConcurrentDictionary<int, KeyValuePair<Task, IPromise>>();
        }

        public KeyValuePair<Task, IPromise> Put(int streamId, Task writeFuture, IPromise promise)
        {
            var item = new KeyValuePair<Task, IPromise>(writeFuture, promise);
            this.streamidPromiseMap.TryAdd(streamId, item);
            return item;
        }

        public async Task AwaitResponses(TimeSpan timeout)
        {
            var keys = this.streamidPromiseMap.Keys;
            foreach (var key in keys)
            {
                if (!this.streamidPromiseMap.TryGetValue(key, out var entry))
                {
                    continue;
                }

                var writeFuture = entry.Key;
                if (!await TaskUtil.WaitAsync(writeFuture, timeout))
                {
                    throw new InvalidOperationException($"Timed out waiting to write for stream id: {key}");
                }

                if (!writeFuture.IsSuccess())
                {
                    var cause = writeFuture.Exception.InnerException;
                    throw new Http2RuntimeException(cause.Message, cause);
                }

                var promise = entry.Value;
                if (!await TaskUtil.WaitAsync(promise.Task, timeout))
                {
                    throw new InvalidOperationException($"Timed out waiting for response on stream id {key}");
                }

                if (!promise.IsSuccess)
                {
                    var cause = promise.Task.Exception.InnerException;
                    throw new Http2RuntimeException(cause.Message, cause);
                }

                s_logger.LogInformation("---Stream id: " + key + " received---");
                this.streamidPromiseMap.TryRemove(key, out _);
            }
        }

        protected override void ChannelRead0(IChannelHandlerContext context, IFullHttpResponse message)
        {
            if (!message.Headers.TryGetInt(HttpConversionUtil.ExtensionHeaderNames.StreamId, out var streamId))
            {
                s_logger.LogError($"HttpResponseHandler unexpected message received: {message}");
                return;
            }


            if (!this.streamidPromiseMap.TryGetValue(streamId, out var entry))
            {
                s_logger.LogError($"Message received for unknown stream id {streamId}");
            }
            else
            {
                // Do stuff with the message (for now just print it)
                var content = message.Content;
                if (content.IsReadable())
                {
                    int contentLength = content.ReadableBytes;
                    byte[] arr = new byte[contentLength];
                    content.ReadBytes(arr);
                    s_logger.LogInformation(Encoding.UTF8.GetString(arr, 0, contentLength));
                }

                entry.Value.TryComplete();
            }
        }
    }
}
