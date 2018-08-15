// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    partial class AbstractServerChannel<TChannel, TUnsafe> : AbstractChannel<TChannel, TUnsafe>, IServerChannel
        where TChannel : AbstractServerChannel<TChannel, TUnsafe>
        where TUnsafe : AbstractServerChannel<TChannel, TUnsafe>.DefaultServerUnsafe, new()
    {
    }
}