// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    partial class WebSocketServerProtocolHandshakeHandler
    {
        static readonly Action<Task, object> FireUserEventTriggeredAction = OnFireUserEventTriggered;
        static readonly Action<Task, object> CloseOnCompleteAction = CloseOnComplete;

        static void OnFireUserEventTriggered(Task t, object state)
        {
            var wrapped = (Tuple<IChannelHandlerContext, IFullHttpRequest, WebSocketServerHandshaker>)state;
            if (t.Status == TaskStatus.RanToCompletion)
            {
                wrapped.Item1.FireUserEventTriggered(new WebSocketServerProtocolHandler.HandshakeComplete(
                    wrapped.Item2.Uri, wrapped.Item2.Headers, wrapped.Item3.SelectedSubprotocol));
            }
            else
            {
                wrapped.Item1.FireExceptionCaught(t.Exception);
            }
        }

        static void CloseOnComplete(Task t, object c) => ((IChannel)c).CloseAsync();
    }
}
