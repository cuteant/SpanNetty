// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// This handler negotiates and initializes the WebSocket Extensions.
    ///
    /// It negotiates the extensions based on the client desired order,
    /// ensures that the successfully negotiated extensions are consistent between them,
    /// and initializes the channel pipeline with the extension decoder and encoder.
    ///
    /// Find a basic implementation for compression extensions at
    /// <tt>io.netty.handler.codec.http.websocketx.extensions.compression.WebSocketServerCompressionHandler</tt>.
    /// </summary>
    public class WebSocketServerExtensionHandler : ChannelHandlerAdapter
    {
        private static readonly Action<Task, object> s_switchWebSocketExtensionHandlerAction = SwitchWebSocketExtensionHandler;
        private static readonly Action<Task, object> s_removeWebSocketExtensionHandlerAction = RemoveWebSocketExtensionHandler;

        private readonly List<IWebSocketServerExtensionHandshaker> _extensionHandshakers;

        private List<IWebSocketServerExtension> _validExtensions;

        public WebSocketServerExtensionHandler(params IWebSocketServerExtensionHandshaker[] extensionHandshakers)
        {
            if (extensionHandshakers is null || 0u >= (uint)extensionHandshakers.Length) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.extensionHandshakers); }

            _extensionHandshakers = new List<IWebSocketServerExtensionHandshaker>(extensionHandshakers);
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            if (msg is IHttpRequest request)
            {
                if (WebSocketExtensionUtil.IsWebsocketUpgrade(request.Headers))
                {
                    if (request.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value)
                        && value is object)
                    {
                        string extensionsHeader = value.ToString();
                        List<WebSocketExtensionData> extensions =
                            WebSocketExtensionUtil.ExtractExtensions(extensionsHeader);
                        int rsv = 0;

                        for (int i = 0; i < extensions.Count; i++)
                        {
                            WebSocketExtensionData extensionData = extensions[i];
                            IWebSocketServerExtension validExtension = null;
                            for (int j = 0; j < _extensionHandshakers.Count; j++)
                            {
                                validExtension = _extensionHandshakers[j].HandshakeExtension(extensionData);
                                if (validExtension is object)
                                {
                                    break;
                                }
                            }

                            if (validExtension is object && 0u >= (uint)(validExtension.Rsv & rsv))
                            {
                                if (_validExtensions is null)
                                {
                                    _validExtensions = CreateValidExtensions();
                                }

                                rsv |= validExtension.Rsv;
                                _validExtensions.Add(validExtension);
                            }
                        }
                    }
                }
            }

            base.ChannelRead(ctx, msg);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static List<IWebSocketServerExtension> CreateValidExtensions()
        {
            return new List<IWebSocketServerExtension>(1);
        }

        public override void Write(IChannelHandlerContext ctx, object msg, IPromise promise)
        {
            if (msg is IHttpResponse response)
            {
                var headers = response.Headers;
                if (WebSocketExtensionUtil.IsWebsocketUpgrade(headers))
                {
                    if (_validExtensions is object)
                    {
                        string headerValue = null;
                        if (headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value))
                        {
                            headerValue = value?.ToString();
                        }

                        for (int i = 0; i < _validExtensions.Count; i++)
                        {
                            WebSocketExtensionData extensionData = _validExtensions[i].NewReponseData();
                            headerValue = WebSocketExtensionUtil.AppendExtension(headerValue,
                                extensionData.Name, extensionData.Parameters);
                        }

                        _ = promise.Task.ContinueWith(s_switchWebSocketExtensionHandlerAction, Tuple.Create(ctx, _validExtensions), TaskContinuationOptions.ExecuteSynchronously);

                        if (headerValue is object)
                        {
                            _ = headers.Set(HttpHeaderNames.SecWebsocketExtensions, headerValue);
                        }
                    }
                }
                _ = promise.Task.ContinueWith(s_removeWebSocketExtensionHandlerAction, Tuple.Create(ctx, this), TaskContinuationOptions.ExecuteSynchronously);
            }

            base.Write(ctx, msg, promise);
        }

        private static void SwitchWebSocketExtensionHandler(Task promise, object state)
        {
            var wrapped = (Tuple<IChannelHandlerContext, List<IWebSocketServerExtension>>)state;
            var ctx = wrapped.Item1;
            var validExtensions = wrapped.Item2;
            var pipeline = ctx.Pipeline;
            if (promise.IsSuccess())
            {
                for (int i = 0; i < validExtensions.Count; i++)
                {
                    IWebSocketServerExtension extension = validExtensions[i];
                    WebSocketExtensionDecoder decoder = extension.NewExtensionDecoder();
                    WebSocketExtensionEncoder encoder = extension.NewExtensionEncoder();
                    _ = pipeline
                        .AddAfter(ctx.Name, decoder.GetType().Name, decoder)
                        .AddAfter(ctx.Name, encoder.GetType().Name, encoder);
                }
            }
        }

        private static void RemoveWebSocketExtensionHandler(Task future, object state)
        {
            var wrapped = (Tuple<IChannelHandlerContext, WebSocketServerExtensionHandler>)state;
            if (future.IsSuccess())
            {
                _ = wrapped.Item1.Pipeline.Remove(wrapped.Item2);
            }
        }
    }
}
