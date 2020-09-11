// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using DotNetty.NetUV.Handles;
    using Xunit;

    public sealed class LoopRunNoWaitTests : IDisposable
    {
        Loop loop;
        int timerCalled;

        void OnTimer(Timer handle) => this.timerCalled++;

        [Fact]
        public void NoWait()
        {
            this.loop = new Loop();

            Timer timer = this.loop.CreateTimer();
            timer.Start(this.OnTimer, 100, 100);

            int result = this.loop.RunNoWait();
            Assert.True(result != 0, "Loop run nowait should return non zero.");
            Assert.Equal(0, this.timerCalled);
        }

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
