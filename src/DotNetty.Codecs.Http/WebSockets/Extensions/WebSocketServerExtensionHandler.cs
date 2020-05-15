// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class WebSocketServerExtensionHandler : ChannelHandlerAdapter
    {
        readonly List<IWebSocketServerExtensionHandshaker> extensionHandshakers;

        List<IWebSocketServerExtension> validExtensions;

        public WebSocketServerExtensionHandler(params IWebSocketServerExtensionHandshaker[] extensionHandshakers)
        {
            if (extensionHandshakers is null || 0u >= (uint)extensionHandshakers.Length) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.extensionHandshakers); }

            this.extensionHandshakers = new List<IWebSocketServerExtensionHandshaker>(extensionHandshakers);
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

                        foreach (WebSocketExtensionData extensionData in extensions)
                        {
                            IWebSocketServerExtension validExtension = null;
                            foreach (IWebSocketServerExtensionHandshaker extensionHandshaker in this.extensionHandshakers)
                            {
                                validExtension = extensionHandshaker.HandshakeExtension(extensionData);
                                if (validExtension is object)
                                {
                                    break;
                                }
                            }

                            if (validExtension is object && 0u >= (uint)(validExtension.Rsv & rsv))
                            {
                                if (this.validExtensions is null)
                                {
                                    this.validExtensions = new List<IWebSocketServerExtension>(1);
                                }

                                rsv = rsv | validExtension.Rsv;
                                this.validExtensions.Add(validExtension);
                            }
                        }
                    }
                }
            }

            base.ChannelRead(ctx, msg);
        }

        public override void Write(IChannelHandlerContext ctx, object msg, IPromise promise)
        {
            if (msg is IHttpResponse response && WebSocketExtensionUtil.IsWebsocketUpgrade(response.Headers))
            {
                if (this.validExtensions is object)
                {
                    string headerValue = null;
                    if (response.Headers.TryGet(HttpHeaderNames.SecWebsocketExtensions, out ICharSequence value))
                    {
                        headerValue = value?.ToString();
                    }

                    foreach (IWebSocketServerExtension extension in this.validExtensions)
                    {
                        WebSocketExtensionData extensionData = extension.NewReponseData();
                        headerValue = WebSocketExtensionUtil.AppendExtension(headerValue,
                            extensionData.Name, extensionData.Parameters);
                    }

                    if (headerValue is object)
                    {
                        response.Headers.Set(HttpHeaderNames.SecWebsocketExtensions, headerValue);
                    }
                }

                promise = promise.Unvoid();
                promise.Task.ContinueWith(s_switchWebSocketExtensionHandlerAction, Tuple.Create(ctx, this.validExtensions), TaskContinuationOptions.ExecuteSynchronously);

            }

            base.Write(ctx, msg, promise);
        }

        static readonly Action<Task, object> s_switchWebSocketExtensionHandlerAction = SwitchWebSocketExtensionHandler;
        static void SwitchWebSocketExtensionHandler(Task promise, object state)
        {
            var wrapped = (Tuple<IChannelHandlerContext, List<IWebSocketServerExtension>>)state;
            var ctx = wrapped.Item1;
            var validExtensions = wrapped.Item2;
            var pipeline = ctx.Pipeline;
            if (promise.IsSuccess() && validExtensions is object)
            {
                foreach (IWebSocketServerExtension extension in validExtensions)
                {
                    WebSocketExtensionDecoder decoder = extension.NewExtensionDecoder();
                    WebSocketExtensionEncoder encoder = extension.NewExtensionEncoder();
                    pipeline.AddAfter(ctx.Name, decoder.GetType().Name, decoder);
                    pipeline.AddAfter(ctx.Name, encoder.GetType().Name, encoder);
                }
            }
            pipeline.Remove(ctx.Name);
        }
    }
}
