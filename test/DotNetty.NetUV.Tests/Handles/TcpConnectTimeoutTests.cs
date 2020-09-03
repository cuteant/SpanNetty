// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Net;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;
    using Xunit;

    public sealed class TcpConnectTimeoutTests : IDisposable
    {
        Loop loop;
        Tcp tcp;

        int connectCount;
        int closeCount;
        Exception connectedError;

        public TcpConnectTimeoutTests()
        {
            this.loop = new Loop();
            this.connectCount = 0;
            this.closeCount = 0;
        }

        /* Verify that connecting to an unreachable address or port doesn't hang
         * the event loop.
         */
        [Fact]
        public void Run()
        {
            IPAddress ipAddress = IPAddress.Parse("8.8.8.8");
            var endPoint = new IPEndPoint(ipAddress, 9999);

            this.loop
                .CreateTimer()
                .Start(this.OnTimer, 50, 0);

            this.tcp = this.loop.CreateTcp();

            try
            {
                this.tcp = this.loop
                    .CreateTcp()
                    .ConnectTo(endPoint, this.OnConnected);
            }
            catch (OperationException exception)
            {
                // Skip
                if (exception.ErrorCode == ErrorCode.ENETUNREACH)
                {
                    return;
                }
            }

            this.loop.RunDefault();
            Assert.Equal(2, this.closeCount);
            Assert.Equal(1, this.connectCount);

            Assert.NotNull(this.connectedError);
            Assert.IsType<OperationException>(this.connectedError);
            var operationException = (OperationException)this.connectedError;
            Assert.Equal(ErrorCode.ECANCELED, operationException.ErrorCode);
        }

        void OnConnected(Tcp tcpClient, Exception exception)
        {
            this.connectedError = exception;
            this.connectCount++;
        }

        void OnTimer(Timer timer)
        {
            this.tcp.CloseHandle(this.OnClose);
            timer.CloseHandle(this.OnClose);
        }

        void OnClose(Timer timer)
        {
            timer.Dispose();
            this.closeCount++;
        }

        void OnClose(Tcp tcpClient)
        {
            tcpClient.Dispose();
            this.closeCount++;
        }

        public void Dispose()
        {
            this.tcp.Dispose();
            this.tcp = null;

            this.loop.Dispose();
            this.loop = null;
        }
    }
}
