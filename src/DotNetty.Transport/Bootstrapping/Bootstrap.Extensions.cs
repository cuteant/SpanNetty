// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Bootstrapping
{
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;

    partial class Bootstrap
    {
        private INameResolver InternalResolver
        {
            [MethodImpl(InlineMethod.Value)]
            get => Volatile.Read(ref _resolver);
            set => Interlocked.Exchange(ref _resolver, value);
        }

        private EndPoint InternalRemoteAddress
        {
            [MethodImpl(InlineMethod.Value)]
            get => Volatile.Read(ref _remoteAddress);
            set => Interlocked.Exchange(ref _remoteAddress, value);
        }
    }
}