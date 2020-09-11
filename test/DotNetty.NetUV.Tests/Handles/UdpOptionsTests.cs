// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;
    using Xunit;

    public sealed class UdpOptionsTests : IDisposable
    {
        const int Port = 8999;

        Loop loop;

        public UdpOptionsTests()
        {
            this.loop = new Loop();
        }

        public static IEnumerable<object[]> IpFamilyCases()
        {
            yield return new object[] { "0.0.0.0" };
            if (Platform.OSSupportsIPv6)
            {
                yield return new object[] { "::" };
            }
        }

        [Theory]
        [MemberData(nameof(IpFamilyCases))]
        public void IpFamily(string ipString)
        {
            var endPoint = new IPEndPoint(IPAddress.Parse(ipString), Port);
            Udp udp = this.loop.CreateUdp();

            /* don't keep the loop alive */
            udp.RemoveReference();
            udp.Bind(endPoint);

            udp.Broadcast(true);
            udp.Broadcast(true);
            udp.Broadcast(false);
            udp.Broadcast(false);

            /* values 1-255 should work */
            for (int i = 1; i <= 255; i++)
            {
                udp.Ttl(i);
            }

            var invalidTtls = new [] { -1, 0, 256 };
            foreach (int i in invalidTtls)
            {
                var error = Assert.Throws<OperationException>(() => udp.Ttl(i));
                Assert.Equal(ErrorCode.EINVAL, error.ErrorCode);
            }

            udp.MulticastLoopback(true);
            udp.MulticastLoopback(true);
            udp.MulticastLoopback(false);
            udp.MulticastLoopback(false);

            /* values 0-255 should work */
            for (int i = 0; i <= 255; i++)
            {
                udp.MulticastTtl(i);
            }

            /* anything >255 should fail */
            var exception = Assert.Throws<OperationException>(() => udp.MulticastTtl(256));
            Assert.Equal(ErrorCode.EINVAL, exception.ErrorCode);
            /* don't test ttl=-1, it's a valid value on some platforms */

            this.loop.RunDefault();
        }

        [Fact]
        public void NoBind()
        {
            Udp udp = this.loop.CreateUdp();

            var error = Assert.Throws<OperationException>(() => udp.MulticastTtl(32));
            Assert.Equal(ErrorCode.EBADF, error.ErrorCode);

            error = Assert.Throws<OperationException>(() => udp.Broadcast(true));
            Assert.Equal(ErrorCode.EBADF, error.ErrorCode);

            error = Assert.Throws<OperationException>(() => udp.Ttl(1));
            Assert.Equal(ErrorCode.EBADF, error.ErrorCode);

            error = Assert.Throws<OperationException>(() => udp.MulticastInterface(IPAddress.Any));
            Assert.Equal(ErrorCode.EBADF, error.ErrorCode);

            udp.CloseHandle(OnClose);

            this.loop.RunDefault();
        }

        static void OnClose(Udp handle) => handle.Dispose();

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
