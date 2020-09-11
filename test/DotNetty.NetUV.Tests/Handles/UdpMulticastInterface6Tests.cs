// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Net;
    using System.Text;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;
    using Xunit;

    public sealed class UdpMulticastInterface6Tests : IDisposable
    {
        const int Port = 8988;

        Loop loop;

        int closeCount;
        int serverSendCount;
        Exception sendError;

        [Fact]
        public void Run()
        {
            if (!Platform.OSSupportsIPv6)
            {
                return;
            }

            this.closeCount = 0;
            this.serverSendCount = 0;

            this.loop = new Loop();
            var endPoint = new IPEndPoint(IPAddress.Parse("::1"), Port);

            var anyEndPoint = new IPEndPoint(IPAddress.Parse("::"), Port);
            Udp server = this.loop.CreateUdp();

            try
            {
                server.Bind(anyEndPoint).MulticastInterface(IPAddress.IPv6Loopback);
                byte[] data = Encoding.UTF8.GetBytes("PING");
                server.QueueSend(data, endPoint, this.OnServerSendCompleted);

                this.loop.RunDefault();

                Assert.Equal(1, this.closeCount);
                Assert.Equal(1, this.serverSendCount);
            }
            catch (OperationException exception)
            {
                this.sendError = exception;
            }

            if (Platform.IsWindows)
            {
                Assert.Null(this.sendError);
            }
            else
            {
                if (this.sendError is object) // Azure DevOps(Linux) sendError is null
                {
                    Assert.IsType<OperationException>(this.sendError);
                    var error = (OperationException)this.sendError;
                    Assert.Equal(ErrorCode.EADDRNOTAVAIL, error.ErrorCode);
                }
            }
        }

        void OnServerSendCompleted(Udp udp, Exception exception)
        {
            this.sendError = exception;
            this.serverSendCount++;
            udp.CloseHandle(this.OnClose);
        }

        void OnClose(Udp handle)
        {
            handle.Dispose();
            this.closeCount++;
        }

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
