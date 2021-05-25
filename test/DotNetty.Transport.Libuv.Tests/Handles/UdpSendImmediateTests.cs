// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using System.Net;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Libuv.Handles;
    using Xunit;

    public sealed class UdpSendImmediateTests : IDisposable
    {
        const int Port = 8996;

        Loop loop;
        int closeCount;
        int clientSendCount;
        int serverReceiveCount;

        [Fact]
        public void Run()
        {
            this.closeCount = 0;
            this.clientSendCount = 0;
            this.serverReceiveCount = 0;

            this.loop = new Loop();

            var anyEndPoint = new IPEndPoint(IPAddress.Any, Port);
            this.loop
                .CreateUdp()
                .ReceiveStart(anyEndPoint, this.OnReceive);

            Udp client = this.loop.CreateUdp();

            byte[] data1 = Encoding.UTF8.GetBytes("PING");
            byte[] data2 = Encoding.UTF8.GetBytes("PANG");
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, Port);
            client.QueueSend(data1, remoteEndPoint, this.OnSendCompleted);
            client.QueueSend(data2, remoteEndPoint, this.OnSendCompleted);

            this.loop.RunDefault();

            Assert.Equal(2, this.clientSendCount);
            Assert.Equal(2, this.serverReceiveCount);
            Assert.Equal(2, this.closeCount);
        }

        void OnSendCompleted(Udp udp, Exception exception)
        {
            if (exception == null)
            {
                this.clientSendCount++;
            }

            if (this.clientSendCount == 2)
            {
                udp.CloseHandle(this.OnClose);
            }
        }

        void OnReceive(Udp udp, IDatagramReadCompletion completion)
        {
            if (completion.Error != null
                || completion.RemoteEndPoint == null)
            {
                return;
            }

            ReadableBuffer buffer = completion.Data;
            string message = buffer.ReadString(Encoding.UTF8);
            if (message == "PING" 
                || message == "PANG")
            {
                this.serverReceiveCount++;
            }

            if (this.serverReceiveCount == 2)
            {
                udp.CloseHandle(this.OnClose);
            }
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
