// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;
    using Xunit;

    public sealed class PipePendingInstancesTests : IDisposable
    {
        Loop loop;
        int connectionCount;

        [Fact]
        public void Run()
        {
            this.loop = new Loop();

            Pipe pipe = this.loop.CreatePipe();
            pipe.PendingInstances(8);

            string pipeName = GetPipeName();
            pipe.Bind(pipeName);

            pipe.PendingInstances(16);
            pipe.Listen(this.OnConnection);

            pipe.CloseHandle(OnClose);

            this.loop.RunDefault();

            Assert.Equal(0, this.connectionCount);
        }

        //"this will never be called"
        void OnConnection(Pipe pipe, Exception exception) => this.connectionCount++;

        static void OnClose(Pipe handle) => handle.Dispose();

        static string GetPipeName() => 
            Platform.IsWindows
            ? "\\\\?\\pipe\\uv-test3"
            : "/tmp/uv-test3-sock";

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
