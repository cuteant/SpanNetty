// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;

    partial class AbstractChannel<TChannel, TUnsafe> : DefaultAttributeMap, IChannel
        where TChannel : AbstractChannel<TChannel, TUnsafe>
        where TUnsafe : AbstractChannel<TChannel, TUnsafe>.AbstractUnsafe, new()
    {
        public bool Equals(IChannel other) => ReferenceEquals(this, other);

        partial class AbstractUnsafe
        {
            static readonly Action<object, object> RegisterAction = OnRegister;

            private static void OnRegister(object u, object p)
            {
                ((AbstractUnsafe)u).Register0((TaskCompletionSource)p);
            }
        }
    }
}