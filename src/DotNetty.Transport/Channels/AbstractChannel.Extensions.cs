// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using DotNetty.Common.Utilities;

    partial class AbstractChannel<TChannel, TUnsafe> : DefaultAttributeMap, IChannel
        where TChannel : AbstractChannel<TChannel, TUnsafe>
        where TUnsafe : AbstractChannel<TChannel, TUnsafe>.AbstractUnsafe, new()
    {
    }
}