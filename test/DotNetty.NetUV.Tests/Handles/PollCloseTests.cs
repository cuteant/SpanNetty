// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using DotNetty.NetUV.Handles;
    using Xunit;

    public sealed class PollCloseTests : IDisposable
    {
        const int NumberOfSockets = 64;
        Loop loop;

        int closeCount;
        int pollCount;
        List<Socket> sockets;

        [Fact]
        public void Run()
        {
            this.sockets = new List<Socket>();
            this.loop = new Loop();

            var handles = new Poll[NumberOfSockets];
            for (int i = 0; i < NumberOfSockets; i++)
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                this.sockets.Add(socket);
                IntPtr handle = TestHelper.GetHandle(socket);

                handles[i] = this.loop
                    .CreatePoll(handle)
                    .Start(PollMask.Readable, this.OnPoll);
            }

            foreach (Poll poll in handles)
            {
                poll.CloseHandle(this.OnClose);
            }

            this.loop.RunDefault();

            Assert.Equal(0, this.pollCount);
            Assert.Equal(NumberOfSockets, this.closeCount);
        }

        void OnPoll(Poll poll, PollStatus status)
        {
            poll.CloseHandle(this.OnClose);
            this.pollCount++;
        }

        void OnClose(Poll handle)
        {
            handle.Dispose();
            this.closeCount++;
        }

        public void Dispose()
        {
            if (this.sockets != null)
            {
                foreach (Socket socket in this.sockets)
                {
                    socket.Dispose();
                }

                this.sockets.Clear();
                this.sockets = null;
            }

            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
