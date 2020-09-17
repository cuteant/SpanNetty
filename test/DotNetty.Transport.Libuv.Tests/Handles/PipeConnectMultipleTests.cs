// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Handles;
    using Xunit;

    public sealed class PipeConnectMultipleTests : IDisposable
    {
        const int NumberOfClients = 4;
        Loop loop;
        int connectionCount;
        int connectedCount;
        Pipe listener;
        List<Pipe> clients;

        [Fact]
        public void Run()
        {
            this.loop = new Loop();

            string pipeName = GetPipeName();
            this.listener = this.loop
                .CreatePipe()
                .Listen(pipeName, this.OnConnection);

            this.clients = new List<Pipe>();
            for (int i = 0; i < NumberOfClients; i++)
            {
                Pipe pipe = this.loop.CreatePipe()
                    .ConnectTo(pipeName, this.OnConnected);
                this.clients.Add(pipe);
            }

            this.loop.RunDefault();

            Assert.Equal(NumberOfClients, this.connectionCount);
            Assert.Equal(NumberOfClients, this.connectedCount);
        }

        void OnConnected(Pipe pipe, Exception exception)
        {
            if (exception == null)
            {
                this.connectedCount++;

                if (this.connectionCount == NumberOfClients 
                    && this.connectedCount == NumberOfClients)
                {
                    this.loop.Stop();
                }
            }
            else
            {
                pipe.CloseHandle(OnClose);
            }
        }

        void OnConnection(Pipe pipe, Exception exception)
        {
            if (exception == null)
            {
                this.connectionCount++;

                if (this.connectionCount == NumberOfClients
                    && this.connectedCount == NumberOfClients)
                {
                    this.loop.Stop();
                }
            }
            else
            {
                pipe.CloseHandle(OnClose);
            }
        }

        static void OnClose(Pipe handle) => handle.Dispose();

        static string GetPipeName() => Platform.IsWindows
                ? "\\\\?\\pipe\\uv-test1"
                : "/tmp/uv-test1-sock";

        public void Dispose()
        {
            this.listener?.Dispose();
            this.listener = null;

            this.clients?.ForEach(x => x.Dispose());
            this.clients?.Clear();
            this.clients = null;

            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
