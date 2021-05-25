// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Handles;
    using DotNetty.Transport.Libuv.Native;
    using Xunit;

    public sealed class PipeConnectPrepareTests : IDisposable
    {
        Loop loop;
        Pipe pipe;
        Prepare prepare;
        int closeCount;
        int connectedCount;

        [Fact]
        public void Run()
        {
            this.loop = new Loop();

            this.pipe = this.loop.CreatePipe();
            this.prepare = this.loop
                .CreatePrepare()
                .Start(this.OnPrepare);

            this.loop.RunDefault();

            Assert.Equal(1, this.connectedCount);
            Assert.Equal(2, this.closeCount);
        }

        void OnPrepare(Prepare handle) => 
            this.pipe.ConnectTo(GetBadPipeName(), this.OnConnected);

        void OnConnected(Pipe handle, Exception exception)
        {
            var error = exception as OperationException;
            if (error != null 
                && error.ErrorCode == ErrorCode.ENOENT)
            {
                this.connectedCount++;
            }

            handle.CloseHandle(this.OnClose);
            this.prepare.CloseHandle(this.OnClose);
        }

        static string GetBadPipeName() => 
            Platform.IsWindows 
            ? "bad-pipe" 
            : "/path/to/unix/socket/that/really/should/not/be/there";

        void OnClose(IScheduleHandle handle)
        {
            handle.Dispose();
            this.closeCount++;
        }

        public void Dispose()
        {
            this.prepare?.Dispose();
            this.prepare = null;

            this.pipe?.Dispose();
            this.pipe = null;

            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
