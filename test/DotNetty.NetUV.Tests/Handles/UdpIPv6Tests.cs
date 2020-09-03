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

    public sealed class UdpIPv6Tests : IDisposable
    {
        const int Port = 9899;
        Loop loop;
        Udp server;
        Udp client;

        int sendCount;
        int receiveCount;
        int closeCount;

        public UdpIPv6Tests()
        {
            this.loop = new Loop();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Run(bool dualStack)
        {
            if (!Platform.OSSupportsIPv6)
            {
                return;
            }

            this.sendCount = 0;
            this.receiveCount = 0;
            this.closeCount = 0;

            var endPoint = new IPEndPoint(IPAddress.IPv6Any, Port);
            this.server = this.loop
                .CreateUdp()
                .ReceiveStart(endPoint, this.OnReceive, dualStack); // Dual

            byte[] data = Encoding.UTF8.GetBytes("PING");
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, Port);
            this.client = this.loop.CreateUdp();
            this.client.QueueSend(data, remoteEndPoint, this.OnSendCompleted);

            this.loop.CreateTimer()
                .Start(this.OnTimer, 500, 0);

            this.loop.RunDefault();

            Assert.Equal(1, this.sendCount);

            // IPv6 only should not receive from IPv4
            Assert.Equal(!dualStack ? 0 : 1, this.receiveCount);
            Assert.Equal(3, this.closeCount);
        }

        void OnSendCompleted(Udp udp, Exception exception)
        {
            if (exception == null)
            {
                this.sendCount++;
            }
        }

        void OnReceive(Udp udp, IDatagramReadCompletion completion)
        {
            if (completion.Error == null 
                && completion.Data.Count > 0)
            {
                this.receiveCount++;
            }
        }

        void OnTimer(Timer handle)
        {
            this.server?.CloseHandle(this.OnClose);
            this.client?.CloseHandle(this.OnClose);
            handle.CloseHandle(this.OnClose);
        }

        void OnClose(ScheduleHandle handle)
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
