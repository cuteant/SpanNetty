// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using System.Net;
    using System.Text;
    using DotNetty.Transport.Libuv.Handles;
    using DotNetty.Transport.Libuv.Native;
    using Xunit;

    public sealed class UdpMulticastInterfaceTests : IDisposable
    {
        const int Port = 8899;

        Loop loop;
        bool sendErrorValid;
        int closeCount;
        int sendCount;

        public UdpMulticastInterfaceTests()
        {
            this.loop = new Loop();
        }

        [Fact]
        public void Run()
        {
            this.sendErrorValid = false;
            this.closeCount = 0;
            this.sendCount = 0;

            var anyEndPoint = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);
            var endPoint = new IPEndPoint(IPAddress.Parse("239.255.0.1"), Port);
            Udp udp = this.loop
                .CreateUdp()
                .Bind(anyEndPoint)
                .MulticastInterface(IPAddress.Any);

            byte[] data = Encoding.UTF8.GetBytes("PING");
            udp.QueueSend(data, endPoint, this.OnSendCompleted);

            /* run the loop till all events are processed */
            this.loop.RunDefault();

            Assert.Equal(1, this.sendCount);
            Assert.Equal(1, this.closeCount);
            Assert.True(this.sendErrorValid);
        }

        void OnSendCompleted(Udp udp, Exception exception)
        {
            if (exception == null)
            {
                this.sendErrorValid = true;
            }
            else
            {
                var error = exception as OperationException;
                if (error != null)
                {
                    this.sendErrorValid = error.ErrorCode == ErrorCode.ENETUNREACH;
                }
            }

            this.sendCount++;
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
