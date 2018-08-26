// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.WebSockets
{
    using System;
    using DotNetty.Codecs.Http.WebSockets;

    public class WebSocketClientHandshaker13Test : WebSocketClientHandshaker07Test
    {
        protected override WebSocketClientHandshaker NewHandshaker(Uri uri, string subprotocol, HttpHeaders headers) => 
            new WebSocketClientHandshaker13(uri, WebSocketVersion.V13, subprotocol, false, headers, 1024);
    }
}
