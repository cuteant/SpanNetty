// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Net;
    using DotNetty.NetUV.Handles;
    using Xunit;

    public sealed class MultipleListenTests : IDisposable
    {
        const int Port = 9191;
        IPEndPoint endPoint;

        Loop loop;
        Tcp server;
        Tcp client;

        int connection;
        int connected;
        int closeCount;
        Exception connectionError;

        public MultipleListenTests()
        {
            this.loop = new Loop();
            this.endPoint = new IPEndPoint(IPAddress.Loopback, Port);
        }

        [Fact]
        public void Run()
        {
            this.connection = 0;
            this.connected = 0;
            this.closeCount = 0;

            this.StartServer();

            this.client = this.loop
                .CreateTcp()
                .ConnectTo(this.endPoint, this.OnConnected);

            this.loop.RunDefault();
            Assert.Equal(1, this.connection);
            Assert.Equal(1, this.connected);
            Assert.Equal(3, this.closeCount);
            Assert.Null(this.connectionError);
        }

        void OnConnected(Tcp tcp, Exception exception)
        {
            this.connected++;
            tcp.CloseHandle(this.OnClose);
        }

        void StartServer()
        {
            this.server = this.loop.CreateTcp();
            this.server.Bind(this.endPoint);

            // Listen called twice
            this.server.Listen(this.OnConnection);
            this.server.Listen(this.OnConnection);
        }

        void OnConnection(Tcp tcp, Exception exception)
        {
            this.connectionError = exception;
            this.connection++;

            tcp.CloseHandle(this.OnClose);
            this.server.CloseHandle(this.OnClose);
        }

        void OnClose(Tcp handle)
        {
            this.closeCount++;
            handle.Dispose();
        }

        public void Dispose()
        {
            this.client?.Dispose();
            this.client = null;

            this.server?.Dispose();
            this.server = null;

            this.loop?.Dispose();
            this.loop = null;
            this.endPoint = null;
        }
    }
}
