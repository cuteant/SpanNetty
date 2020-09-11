// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Net;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;
    using Xunit;

    public sealed class ConnectionFailTests : IDisposable
    {
        const int Port = 9989;

        Loop loop;
        Tcp tcp;
        Timer timer;

        IPEndPoint endPoint;
        bool closeHandle;

        int closeCount;
        int connectCount;
        int timerCount;
        int timerCloseCount;

        bool connectionErrorValid;
        bool timerCheckValid;

        public ConnectionFailTests()
        {
            this.loop = new Loop();
            this.endPoint = new IPEndPoint(IPAddress.Loopback, Port);
        }

        //
        // This test attempts to connect to a port where no server is running. 
        // We expect an error.
        //
        [Fact]
        public void NoServerListening()
        {
            this.connectionErrorValid = false;
            this.closeCount = 0;
            this.closeHandle = true;

            var localEndPoint = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);

            /* There should be no servers listening on this port. */
            this.tcp = this.loop
                .CreateTcp()
                .Bind(localEndPoint)
                .ConnectTo(this.endPoint, this.OnConnected);

            this.loop.RunDefault();

            Assert.Equal(1, this.connectCount);
            Assert.Equal(1, this.closeCount);
            Assert.True(this.connectionErrorValid);
        }

        //
        // This test is the same as the first except it check that the close
        // callback of the tcp handle hasn't been made after the failed connection
        // attempt.
        //
        [Fact]
        public void ShouldNotCloseHandle()
        {
            this.connectionErrorValid = false;
            this.closeCount = 0;
            this.closeHandle = false;

            var localEndPoint = new IPEndPoint(IPAddress.Any, IPEndPoint.MinPort);

            this.timer = this.loop.CreateTimer();

            /* There should be no servers listening on this port. */
            this.tcp = this.loop
                .CreateTcp()
                .Bind(localEndPoint)
                .ConnectTo(this.endPoint, this.OnConnected);

            this.loop.RunDefault();

            Assert.Equal(1, this.timerCount);
            Assert.Equal(1, this.timerCloseCount);
            Assert.True(this.timerCheckValid);
        }

        void OnTimer(Timer handle)
        {
            this.timerCount++;

            /*
             * These are the important asserts. The connection callback has been made,
             * but libuv hasn't automatically closed the socket. The user must
             * uv_close the handle manually.
             */
            this.timerCheckValid = this.closeCount == 0 && this.connectCount == 1;

            /* Close the tcp handle. */
            this.tcp.CloseHandle(this.OnClose);

            /* Close the timer. */
            handle.CloseHandle(this.OnClose);
        }

        void OnConnected(Tcp handle, Exception exception)
        {
            var error = exception as OperationException;

            this.connectionErrorValid =
                error != null
                && error.ErrorCode == ErrorCode.ECONNREFUSED
                && this.closeCount == 0;

            this.connectCount++;

            if (this.closeHandle)
            {
                handle.CloseHandle(this.OnClose);
            }
            else
            {
                this.timer.Start(this.OnTimer, 100, 0);
            }
        }

        void OnClose(Timer handle)
        {
            handle.Dispose();
            this.timerCloseCount++;
        }

        void OnClose(Tcp handle)
        {
            handle.Dispose();
            this.closeCount++;
        }

        public void Dispose()
        {
            this.endPoint = null;

            this.tcp?.Dispose();
            this.tcp = null;

            this.timer?.Dispose();
            this.timer = null;

            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
