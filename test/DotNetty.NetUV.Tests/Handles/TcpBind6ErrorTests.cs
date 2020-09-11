// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Net;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;
    using Xunit;

    public sealed class TcpBind6ErrorTests : IDisposable
    {
        const int Port = 9888;
        Loop loop;
        int closeCount;

        public TcpBind6ErrorTests()
        {
            this.loop = new Loop();
            this.closeCount = 0;
        }

        [Fact]
        public void AddressInUse()
        {
            if (!Platform.OSSupportsIPv6)
            {
                return;
            }

            IPAddress address = IPAddress.Parse("::");
            var endPoint = new IPEndPoint(address, Port);

            Tcp tcp1 = this.loop.CreateTcp().Bind(endPoint);
            Tcp tcp2 = this.loop.CreateTcp().Bind(endPoint);

            tcp1.Listen(OnConnection);
            Assert.Throws<OperationException>(() => tcp2.Listen(OnConnection));

            tcp1.CloseHandle(this.OnClose);
            tcp2.CloseHandle(this.OnClose);
            this.loop.RunDefault();
            Assert.Equal(2, this.closeCount);
        }

        [Fact]
        public void AddressNotAvailable()
        {
            if (!Platform.OSSupportsIPv6)
            {
                return;
            }

            IPAddress address = IPAddress.Parse("4:4:4:4:4:4:4:4");
            var endPoint = new IPEndPoint(address, Port);
            Tcp tcp = this.loop.CreateTcp();
            Assert.Throws<OperationException>(() => tcp.Bind(endPoint));

            tcp.CloseHandle(this.OnClose);
            this.loop.RunDefault();
            Assert.Equal(1, this.closeCount);
        }

        [Fact]
        public void Invalid()
        {
            if (!Platform.OSSupportsIPv6)
            {
                return;
            }

            IPAddress address = IPAddress.Parse("::");
            var endPoint1 = new IPEndPoint(address, Port);
            var endPoint2 = new IPEndPoint(address, Port + 1);

            Tcp tcp = this.loop.CreateTcp();
            Assert.Equal(tcp.Bind(endPoint1), tcp);

            Assert.Throws<OperationException>(() => tcp.Bind(endPoint2));
            tcp.CloseHandle(this.OnClose);
            this.loop.RunDefault();
            Assert.Equal(1, this.closeCount);
        }

        [Fact]
        public void LocalHost()
        {
            if (!Platform.OSSupportsIPv6)
            {
                return;
            }

            var endPoint = new IPEndPoint(IPAddress.IPv6Loopback, Port);
            Tcp tcp = this.loop.CreateTcp();

            try
            {
                tcp.Bind(endPoint);
            }
            catch (OperationException exception)
            {
                // IPv6 loop back not available happens on some Linux
                Assert.Equal(ErrorCode.EADDRNOTAVAIL, exception.ErrorCode);
                return;
            }

            tcp.CloseHandle(this.OnClose);

            this.loop.RunDefault();
            Assert.Equal(1, this.closeCount);
        }

        static void OnConnection(Tcp tcp, Exception exception) =>
            Assert.True(exception == null);

        void OnClose(Tcp tcp)
        {
            tcp.Dispose();
            this.closeCount++;
        }

        public void Dispose()
        {
            this.loop.Dispose();
            this.loop = null;
        }
    }
}
