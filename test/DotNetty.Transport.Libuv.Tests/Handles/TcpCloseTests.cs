// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using System.Net;
    using System.Text;
    using DotNetty.Transport.Libuv.Handles;
    using Xunit;

    public sealed class TcpCloseTests : IDisposable
    {
        const int Port = 9886;
        const int NumberOfWriteRequests = 32;

        readonly IPEndPoint endPoint;
        Loop loop;
        Tcp tcpServer;
        int writeCount;
        int closeCount;
        Exception writeError;
        Exception connectionError;

        public TcpCloseTests()
        {
            this.endPoint = new IPEndPoint(IPAddress.Loopback, Port);
            this.loop = new Loop();
            this.writeCount = 0;
            this.closeCount = 0;
        }

        [Fact]
        public void TcpClose()
        {
            this.tcpServer = this.StartServer();
            this.loop.CreateTcp().ConnectTo(this.endPoint, this.OnConnected);

            Assert.Equal(0, this.writeCount);
            Assert.Equal(0, this.closeCount);

            this.loop.RunDefault();

            Assert.Equal(NumberOfWriteRequests, this.writeCount);
            Assert.Equal(1, this.closeCount);
            Assert.Null(this.writeError);
            Assert.Null(this.connectionError);
        }

        void OnConnected(Tcp tcp, Exception exception)
        {
            Assert.True(exception == null);

            byte[] content = Encoding.UTF8.GetBytes("PING");
            for (int i = 0; i < NumberOfWriteRequests; i++)
            {
                tcp.QueueWrite(content, this.OnWriteCompleted);
            }

            tcp.CloseHandle(this.OnClose);
        }

        void OnClose(Tcp tcp)
        {
            tcp.Dispose();
            this.closeCount++;
        } 

        void OnWriteCompleted(Tcp tcp, Exception exception)
        {
            this.writeError = exception;
            this.writeCount++;
        }

        Tcp StartServer()
        {
            Tcp tcp = this.loop.CreateTcp()
                .Listen(this.endPoint, this.OnConnection);
            tcp.RemoveReference();

            return tcp;
        }

        void OnConnection(Tcp tcp, Exception exception) => 
            this.connectionError = exception;

        public void Dispose()
        {
            this.tcpServer?.Dispose();
            this.tcpServer = null;
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
