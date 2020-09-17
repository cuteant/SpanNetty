// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using System.Net;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Transport.Libuv.Handles;
    using DotNetty.Transport.Libuv.Native;
    using Xunit;

    public sealed class GetSockNameTests : IDisposable
    {
        const int Port = 9881;

        Loop loop;
        IScheduleHandle server;
        int closeCount;
        int connectedCount;
        int connectionCount;

        public GetSockNameTests()
        {
            this.loop = new Loop();
        }

        [Fact]
        public void Tcp()
        {
            this.closeCount = 0;
            this.connectedCount = 0;
            this.connectionCount = 0;

            var anyEndPoint = new IPEndPoint(IPAddress.Loopback, Port);
            Tcp tcpServer = this.loop.CreateTcp();
            tcpServer.Listen(anyEndPoint, this.OnConnection);
            this.server = tcpServer;

            IPEndPoint localEndPoint = tcpServer.GetLocalEndPoint();
            Assert.NotNull(localEndPoint);
            Assert.Equal(anyEndPoint.Address, localEndPoint.Address);
            Assert.Equal(Port, localEndPoint.Port);

            var error = Assert.Throws<OperationException>(() => tcpServer.GetPeerEndPoint());
            Assert.Equal(ErrorCode.ENOTCONN, error.ErrorCode);

            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, Port);
            Tcp client = this.loop.CreateTcp().ConnectTo(remoteEndPoint, this.OnConnected);

            IPEndPoint endPoint = client.GetLocalEndPoint();
            Assert.NotNull(endPoint);
            Assert.Equal(anyEndPoint.AddressFamily, endPoint.AddressFamily);
            Assert.True(endPoint.Port > 0);

            this.loop.RunDefault();

            Assert.Equal(1, this.connectedCount);
            Assert.Equal(1, this.connectionCount);
            Assert.Equal(3, this.closeCount);
        }

        [Fact]
        public void Udp()
        {
            this.closeCount = 0;

            var anyEndPoint = new IPEndPoint(IPAddress.Any, Port);
            Udp udp = this.loop
                .CreateUdp()
                .ReceiveStart(anyEndPoint, this.OnReceive);
            this.server = udp;

            IPEndPoint localEndPoint = udp.GetLocalEndPoint();
            Assert.NotNull(localEndPoint);
            Assert.Equal(anyEndPoint.Address, localEndPoint.Address);
            Assert.Equal(Port, localEndPoint.Port);

            Udp client = this.loop.CreateUdp();

            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, Port);
            byte[] data = Encoding.UTF8.GetBytes("PING");
            client.QueueSend(data, remoteEndPoint, this.OnSendCompleted);

            this.loop.RunDefault();

            Assert.Equal(1, this.connectedCount);
            Assert.Equal(1, this.connectionCount);
            Assert.Equal(2, this.closeCount);
        }

        void OnSendCompleted(Udp udp, Exception exception)
        {
            if (exception == null)
            {
                this.connectedCount++;
            }

            udp.CloseHandle(this.OnClose);
        }

        void OnReceive(Udp udp, IDatagramReadCompletion completion)
        {
            if (completion.Error == null)
            {
                ReadableBuffer data = completion.Data;
                if (data.Count == 0)
                {
                    return;
                }

                IPEndPoint localEndPoint = udp.GetLocalEndPoint();
                if (Equals(localEndPoint.Address, IPAddress.Any) 
                    && localEndPoint.Port == Port)
                {
                    this.connectionCount++;
                }
            }

            udp.CloseHandle(this.OnClose);
        }

        void OnConnected(Tcp tcp, Exception exception)
        {
            if (exception == null)
            {
                IPEndPoint endPoint = tcp.GetLocalEndPoint();
                IPEndPoint remoteEndPoint = tcp.GetPeerEndPoint();

                if (Equals(endPoint.Address, IPAddress.Loopback) 
                    && Equals(remoteEndPoint.Address, IPAddress.Loopback) 
                    && remoteEndPoint.Port == Port)
                {
                    this.connectedCount++;
                }
            }

            tcp.CloseHandle(this.OnClose);
        }

        void OnConnection(Tcp tcp, Exception exception)
        {
            if (exception == null)
            {
                IPEndPoint endPoint = tcp.GetLocalEndPoint();
                IPEndPoint remoteEndPoint = tcp.GetPeerEndPoint();

                if (Equals(endPoint.Address, IPAddress.Loopback) 
                    && endPoint.Port == Port
                    && Equals(remoteEndPoint.Address, IPAddress.Loopback)
                    )
                {
                    this.connectionCount++;
                }
            }

            tcp.CloseHandle(this.OnClose);
            this.server.CloseHandle(this.OnClose);
        }

        void OnClose(IScheduleHandle handle)
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
