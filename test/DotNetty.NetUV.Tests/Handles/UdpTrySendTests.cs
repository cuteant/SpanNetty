// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Net;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;
    using Xunit;

    public sealed class UdpTrySendTests : IDisposable
    {
        const int Port = 8993;

        Loop loop;
        Udp client;
        Udp server;
        int closeCount;
        int serverReceiveCount;
        Exception receiveError;

        [Fact]
        public void Run()
        {
            if (Platform.IsWindows)
            {
                // As of libuv 1.9.1 on Windows, udp_try_send is not yet implemented.
                return;
            }

            this.closeCount = 0;
            this.serverReceiveCount = 0;

            this.loop = new Loop();

            var anyEndPoint = new IPEndPoint(IPAddress.Any, Port);
            this.server = this.loop
                .CreateUdp()
                .ReceiveStart(anyEndPoint, this.OnServerReceive);

            var remoteEndPoint = new IPEndPoint(IPAddress.Loopback, Port);

            this.client = this.loop.CreateUdp();

            // Message too big
            var data = new byte[64 * 1024];
            var error = Assert.Throws<OperationException>(() => this.client.TrySend(remoteEndPoint, data));
            Assert.Equal(ErrorCode.EMSGSIZE, error.ErrorCode);

            // Normal message
            data = Encoding.UTF8.GetBytes("EXIT");
            this.client.TrySend(remoteEndPoint, data);

            this.loop.RunDefault();

            Assert.Null(this.receiveError);
            Assert.Equal(2, this.closeCount);
            Assert.Equal(1, this.serverReceiveCount);
        }

        void OnServerReceive(Udp udp, IDatagramReadCompletion completion)
        {
            this.receiveError = completion.Error;

            ReadableBuffer data = completion.Data;
            string message = data.ReadString(Encoding.UTF8);
            if (message == "EXIT")
            {
                this.serverReceiveCount++;
            }

            udp.CloseHandle(this.OnClose);
            this.client?.CloseHandle(this.OnClose);

            this.server.ReceiveStop();
            this.server.CloseHandle(this.OnClose);
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
