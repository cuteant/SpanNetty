// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWhenPossible
namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    partial class WebSocketClientHandshaker
    {
        static readonly Action<object, object> RemoveHandlerAction = OnRemoveHandler;
        static readonly Action<Task, object> LinkOutcomeContinuationAction = LinkOutcomeContinuation;

        static void OnRemoveHandler(object p, object h) => ((IChannelPipeline)p).Remove((IChannelHandler)h);

        static void LinkOutcomeContinuation(Task t, object state)
        {
            var wrapped = (Tuple<IPromise, IChannelPipeline, WebSocketClientHandshaker>)state;
            switch (t.Status)
            {
                case TaskStatus.RanToCompletion:
                    IChannelPipeline p = wrapped.Item2;
                    IChannelHandlerContext ctx = p.Context<HttpRequestEncoder>() ?? p.Context<HttpClientCodec>();
                    if (ctx == null)
                    {
                        wrapped.Item1.TrySetException(ThrowHelper.GetInvalidOperationException<HttpRequestEncoder>());
                        return;
                    }

                    p.AddAfter(ctx.Name, "ws-encoder", wrapped.Item3.NewWebSocketEncoder());
                    wrapped.Item1.TryComplete();
                    break;
                case TaskStatus.Canceled:
                    wrapped.Item1.TrySetCanceled();
                    break;
                case TaskStatus.Faulted:
                    wrapped.Item1.TrySetException(t.Exception.InnerExceptions);
                    break;
                default:
                    ThrowHelper.ThrowArgumentOutOfRangeException(); break;
            }
        }
    }
}
