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

    public sealed class UdpSendAndReceiveTests : IDisposable
    {
        const int Port = 8997;
        Loop loop;

        int closeCount;
        int clientReceiveCount;
        int clientSendCount;
        int serverReceiveCount;
        int serverSendCount;
        Exception serverSendError;


        [Fact]
        public void Run()
        {
            this.closeCount = 0;
            this.clientReceiveCount = 0;
            this.clientSendCount = 0;
            this.serverReceiveCount = 0;
            this.serverSendCount = 0;

            this.loop = new Loop();

            var anyEndPoint = new IPEndPoint(IPAddress.Any, Port);
            this.loop
                .CreateUdp()
                .ReceiveStart(anyEndPoint, this.OnServerReceive);

            Udp client = this.loop.CreateUdp();

            byte[] data = Encoding.UTF8.GetBytes("PING");
            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, Port);
            client.QueueSend(data, remoteEndPoint, this.OnClientSendCompleted);

            this.loop.RunDefault();

            Assert.Equal(1, this.clientSendCount);
            Assert.Equal(1, this.serverSendCount);
            Assert.Equal(1, this.serverReceiveCount);
            Assert.Equal(1, this.clientReceiveCount);
            Assert.Equal(2, this.closeCount);

            Assert.Null(this.serverSendError);
        }

        void OnClientReceive(Udp udp, IDatagramReadCompletion completion)
        {
            ReadableBuffer buffer = completion.Data;
            string message = buffer.ReadString(Encoding.UTF8);
            if (message == "PONG")
            {
                this.clientReceiveCount++;
            }

            udp.CloseHandle(this.OnClose);
        }

        void OnClientSendCompleted(Udp udp, Exception exception)
        {
            if (exception == null)
            {
                udp.ReceiveStart(this.OnClientReceive);
            }

            this.clientSendCount++;
        }

        void OnServerReceive(Udp udp, IDatagramReadCompletion completion)
        {
            ReadableBuffer buffer = completion.Data;
            string message = buffer.ReadString(Encoding.UTF8);
            if (message == "PING")
            {
                this.serverReceiveCount++;
            }

            udp.ReceiveStop();
            byte[] data = Encoding.UTF8.GetBytes("PONG");
            udp.QueueSend(data, completion.RemoteEndPoint, this.OnServerSendCompleted);
        }

        void OnServerSendCompleted(Udp udp, Exception exception)
        {
            this.serverSendError = exception;
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
