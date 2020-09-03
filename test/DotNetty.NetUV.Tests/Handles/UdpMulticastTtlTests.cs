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

    public sealed class UdpMulticastTtlTests : IDisposable
    {
        const int Port = 8992;

        Loop loop;
        int closeCount;
        int serverSendCount;

        [Fact]
        public void Run()
        {
            this.closeCount = 0;
            this.serverSendCount = 0;

            this.loop = new Loop();

            var anyEndPoint = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);
            Udp server = this.loop
                .CreateUdp()
                .Bind(anyEndPoint)
                .MulticastTtl(32);

            /* server sends "PING" */
            byte[] data = Encoding.UTF8.GetBytes("PING");
            IPAddress address = IPAddress.Parse("239.255.0.1");
            var endPoint = new IPEndPoint(address, Port);
            server.QueueSend(data, endPoint, this.OnServerSendCompleted);

            this.loop.RunDefault();

            Assert.Equal(1, this.closeCount);
            Assert.Equal(1, this.serverSendCount);
        }

        void OnServerSendCompleted(Udp udp, Exception exception)
        {
            if (exception == null)
            {
                this.serverSendCount++;
            }
            else
            {
                var error = exception as OperationException;
                if (error != null 
                    && error.ErrorCode == ErrorCode.ENETUNREACH)
                {
                    this.serverSendCount++;
                }
            }

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
