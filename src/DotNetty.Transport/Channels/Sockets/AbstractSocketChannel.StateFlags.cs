// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    partial class AbstractSocketChannel<TChannel, TUnsafe>
    {
        protected static class StateFlags
        {
            public const int Open = 1;
            public const int ReadScheduled = 1 << 1;
            public const int WriteScheduled = 1 << 2;
            public const int Active = 1 << 3;
            // todo: add input shutdown and read pending here as well?
        }
    }
}