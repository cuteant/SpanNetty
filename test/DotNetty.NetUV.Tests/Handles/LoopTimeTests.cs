// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using DotNetty.NetUV.Handles;
    using Xunit;

    public sealed class LoopTimeTests : IDisposable
    {
        Loop loop;

        [Fact]
        public void UpdateTime()
        {
            this.loop = new Loop();
            long start = this.loop.Now;

            while (this.loop.Now - start < 1000)
            {
                this.loop.RunNoWait();
            }
        }

        static void OnTimer(Timer handle) => handle.Dispose();

        [Fact]
        public void BackendTimeout()
        {
            this.loop = new Loop();
            Timer timer = this.loop.CreateTimer();
            Assert.False(this.loop.IsAlive);
            Assert.Equal(0, this.loop.GetBackendTimeout());

            timer.Start(OnTimer, 1000, 0);  /* 1 sec */
            long timeout = this.loop.GetBackendTimeout();
            Assert.True(timeout > 100, $"BackendTimeout {timeout} should be > 100");  /* 0.1 sec */

            timeout = this.loop.GetBackendTimeout();
            Assert.True(timeout <= 1000, $"BackendTimeout {timeout} should be <= 1000");   /* 1 sec */

            this.loop.RunDefault();
            Assert.Equal(0, this.loop.GetBackendTimeout());
        }

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
