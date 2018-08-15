// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    partial class AbstractSocketByteChannel<TChannel, TUnsafe> : AbstractSocketChannel<TChannel, TUnsafe>
        where TChannel : AbstractSocketByteChannel<TChannel, TUnsafe>
        where TUnsafe : AbstractSocketByteChannel<TChannel, TUnsafe>.SocketByteChannelUnsafe, new()
    {
        private static void OnFlushSync(object channel)
        {
            ((TChannel)channel).Flush();
        }
    }
}