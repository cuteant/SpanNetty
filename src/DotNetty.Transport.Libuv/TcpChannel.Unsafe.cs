// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;

    partial class TcpChannel<TChannel>
    {
        public sealed class TcpChannelUnsafe : NativeChannelUnsafe
        {
            public TcpChannelUnsafe() : base() //TcpChannel channel) : base(channel)
            {
            }

            public override IntPtr UnsafeHandle => _channel._tcp.Handle;
        }
    }
}
