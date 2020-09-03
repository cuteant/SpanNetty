// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Net;
    using System.Text;
    using DotNetty.NetUV.Handles;
    using Xunit;

    public sealed class UdpSendUnreachableTests : IDisposable
    {
        Loop loop;
        Udp client;

        int timerCount;
        int closeCount;
        int clientSendCount;
        int clientReceiveCount;
        Exception receiveError;
        Exception sendError;

        [Fact]
        public void Run()
        {
            this.timerCount = 0;
            this.closeCount = 0;
            this.clientSendCount = 0;
            this.clientReceiveCount = 0;

            IPAddress address = IPAddress.Parse("127.0.0.1");
            var endPoint1 = new IPEndPoint(address, TestHelper.TestPort);
            var endPoint2 = new IPEndPoint(address, TestHelper.TestPort2);

            this.loop = new Loop();
            this.loop
                .CreateTimer()
                .Start(this.OnTimer, 1000, 0);

            this.client = this.loop
                .CreateUdp()
                .Bind(endPoint2);

            // Client read should not get any results
            this.client.ReceiveStart(this.OnReceive);

            byte[] data1 = Encoding.UTF8.GetBytes("PING");
            byte[] data2 = Encoding.UTF8.GetBytes("PANG");
            this.client.QueueSend(data1, endPoint1, this.OnSendCompleted);
            this.client.QueueSend(data2, endPoint1, this.OnSendCompleted);

            this.loop.RunDefault();

            Assert.Null(this.receiveError);
            Assert.Null(this.sendError);

            Assert.Equal(1, this.timerCount);
            Assert.Equal(2, this.clientSendCount);
            Assert.Equal(0, this.clientReceiveCount);
            Assert.Equal(2, this.closeCount);
        }

        void OnReceive(Udp udp, IDatagramReadCompletion completion)
        {
            this.receiveError = completion.Error;
            this.clientReceiveCount++;
        }

        void OnSendCompleted(Udp udp, Exception exception)
        {
            this.sendError = exception;
            this.clientSendCount++;
        }

        void OnTimer(Timer handle)
        {
            this.timerCount++;
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
