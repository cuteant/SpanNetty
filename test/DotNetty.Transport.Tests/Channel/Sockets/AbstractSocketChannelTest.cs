// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Channel.Sockets
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit;
    using Xunit.Abstractions;

    [Collection("Transport Tests")]
    public abstract class AbstractSocketChannelTest<TChannel> : TestBase
        where TChannel : class, IChannel
    {
        public AbstractSocketChannelTest(ITestOutputHelper output)
            : base(output)
        {
        }

        protected abstract TChannel NewSocketChannel();

        protected abstract Socket NewSocket();
    }
}