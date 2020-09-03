// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;
    using Xunit;

    public sealed class PipeConnectErrorTests : IDisposable
    {
        Loop loop;
        int connectCount;
        int closeCount;
        bool badPipeErrorValid;

        public PipeConnectErrorTests()
        {
            this.loop = new Loop();
        }

        [Fact]
        public void BadName()
        {
            this.connectCount = 0;
            this.closeCount = 0;
            this.badPipeErrorValid = false;

            Pipe pipe = this.loop.CreatePipe();
            string name = GetBadPipeName();
            pipe.ConnectTo(name, this.OnConnectBadPipe);

            this.loop.RunDefault();

            Assert.Equal(1, this.connectCount);
            Assert.Equal(1, this.closeCount);
            Assert.True(this.badPipeErrorValid);
        }

        void OnConnectBadPipe(Pipe pipe, Exception exception)
        {
            var error = exception as OperationException;
            if (error != null)
            {
                this.badPipeErrorValid = error.ErrorCode == ErrorCode.ENOENT;
            }

            this.connectCount++;
            pipe.CloseHandle(this.OnClose);
        }

        void OnClose(Pipe handle)
        {
            handle.Dispose();
            this.closeCount++;
        }

        static string GetBadPipeName() =>
            Platform.IsWindows
            ? "bad-pipe" 
            : "/path/to/unix/socket/that/really/should/not/be/there";


        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
