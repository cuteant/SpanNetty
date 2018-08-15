// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    partial class AbstractSocketMessageChannel<TChannel, TUnsafe> : AbstractSocketChannel<TChannel, TUnsafe>
        where TChannel : AbstractSocketMessageChannel<TChannel, TUnsafe>
        where TUnsafe : AbstractSocketMessageChannel<TChannel, TUnsafe>.SocketMessageUnsafe, new()
    {
    }
}