// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Handles;
    using DotNetty.Transport.Libuv.Native;
    using Xunit;

    public sealed class PipeServerCloseTests : IDisposable
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

            string pipeName = GetPipeName();
            this.listener = this.loop
                .CreatePipe()
                .Listen(pipeName, this.OnConnection);

            this.loop
                .CreatePipe()
                .ConnectTo(pipeName, this.OnConnected);

            this.loop.RunDefault();

            Assert.Equal(0, this.connectionError);
            Assert.Equal(1, this.connectedCount);
            Assert.Equal(2, this.closeCount);
        }

        void OnConnected(Pipe pipe, Exception exception)
        {
            if (exception == null)
            {
                this.connectedCount++;
            }

            pipe.CloseHandle(this.OnClose);
            this.listener.CloseHandle(this.OnClose);
        }

        void OnConnection(Pipe pipe, Exception exception)
        {
            // This function *may* be called, depending on whether accept or the
            // connection callback is called first.
            //
            if (exception != null)
            {
                this.connectionError++;
            }
        }

        void OnClose(Pipe handle)
        {
            handle.Dispose();
            this.closeCount++;
        }

        static string GetPipeName() => 
            Platform.IsWindows
            ? "\\\\?\\pipe\\uv-test4"
            : "/tmp/uv-test4-sock";

        public void Dispose()
        {
            this.listener?.Dispose();
            this.listener = null;

            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
