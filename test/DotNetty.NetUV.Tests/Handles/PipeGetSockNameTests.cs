// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;
    using Xunit;

    public sealed class PipeGetSockNameTests : IDisposable
    {
        Loop loop;
        Pipe listener;
        int connectionError;
        int connectedCount;
        int closeCount;

        [Fact]
        public void Run()
        {
            this.loop = new Loop();

            Pipe server = this.loop.CreatePipe();

            var error = Assert.Throws<OperationException>(() => server.GetSocketName());
            Assert.Equal(ErrorCode.EBADF, error.ErrorCode);

            error = Assert.Throws<OperationException>(() => server.GetPeerName());
            Assert.Equal(ErrorCode.EBADF, error.ErrorCode);

            string pipeName = GetPipeName();
            server.Bind(pipeName);

            string name = server.GetSocketName();
            Assert.Equal(pipeName, name);

            error = Assert.Throws<OperationException>(() => server.GetPeerName());
            Assert.Equal(ErrorCode.ENOTCONN, error.ErrorCode);

            this.listener = server.Listen(this.OnConnection);

            Pipe client = this.loop
                .CreatePipe()
                .ConnectTo(pipeName, this.OnConnected);

            name = client.GetSocketName();
            Assert.True(string.IsNullOrEmpty(name));

            name = client.GetPeerName();
            Assert.Equal(pipeName, name);

            this.loop.RunDefault();

            Assert.Equal(0, this.connectionError);
            Assert.Equal(1, this.connectedCount);
            Assert.Equal(2, this.closeCount);
        }

        void OnConnected(Pipe pipe, Exception exception)
        {
            if (exception == null)
            {
                string peerName = pipe.GetPeerName();
                string sockName = pipe.GetSocketName();
                if (peerName == GetPipeName() 
                    && string.IsNullOrEmpty(sockName))
                {
                    this.connectedCount++;
                }
            }

            this.listener.CloseHandle(this.OnClose);
            pipe.CloseHandle(this.OnClose);
        }

        void OnClose(StreamHandle handle)
        {
            handle.Dispose();
            this.closeCount++;
        }

        void OnConnection(Pipe pipe, Exception exception)
        {
            // This function *may* be called, depending on whether accept or the
            // connection callback is called first.
            if (exception != null)
            {
                this.connectionError++;
            }
        }

        static string GetPipeName() =>
            Platform.IsWindows
            ? "\\\\?\\pipe\\uv-test2"
            : "/tmp/uv-test2-sock";

        public void Dispose()
        {
            this.listener?.Dispose();
            this.listener = null;

            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
