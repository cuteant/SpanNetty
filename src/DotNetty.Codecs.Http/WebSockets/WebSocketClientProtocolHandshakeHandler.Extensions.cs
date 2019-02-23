// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    partial class WebSocketClientProtocolHandshakeHandler
    {
        static readonly Action<Task, object> FireUserEventTriggeredAction = OnFireUserEventTriggered;

        static void OnFireUserEventTriggered(Task t, object state)
        {
            var ctx = (IChannelHandlerContext)state;
            if (t.IsSuccess())
            {
                ctx.FireUserEventTriggered(WebSocketClientProtocolHandler.ClientHandshakeStateEvent.HandshakeIssued);
            }
            else
            {
                ctx.FireExceptionCaught(t.Exception);
            }
        }
    }
}
