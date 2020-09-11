// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using DotNetty.NetUV.Handles;
    using Xunit;

    public sealed class TcpFlagsTests : IDisposable
    {
        Loop loop;

        public TcpFlagsTests()
        {
            this.loop = new Loop();
        }

        [Fact]
        public void Run()
        {
            Tcp tcp = this.loop.CreateTcp();
            tcp.NoDelay(true);
            tcp.KeepAlive(true, 60);

            tcp.CloseHandle(OnClose);

            int result = this.loop.RunDefault();
            Assert.Equal(0, result);
        }

        static void OnClose(Tcp tcp) => tcp.Dispose();

        public void Dispose()
        {
            this.loop.Dispose();
            this.loop = null;
        }
    }
}
