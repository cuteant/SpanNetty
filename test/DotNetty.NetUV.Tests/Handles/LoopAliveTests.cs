// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Requests;
    using Xunit;

    public sealed class LoopAliveTests : IDisposable
    {
        Loop loop;

        int timerCount;
        int workCount;
        int afterWorkCount;

        [Fact]
        public void IsAlive()
        {
            this.loop = new Loop(); // New loop should not be alive
            Assert.False(this.loop.IsAlive);

            // loops with handles are alive
            Timer timer = this.loop.CreateTimer();
            timer.Start(this.OnTimer, 100, 0);
            Assert.True(this.loop.IsAlive);

            // loop run should not be alive
            this.loop.RunDefault();
            Assert.Equal(1, this.timerCount); // Timer should fire
            Assert.False(this.loop.IsAlive);

            // loops with requests are alive
            Work request = this.loop.CreateWorkRequest(this.OnWork, this.OnAfterWork);
            Assert.NotNull(request);
            Assert.True(this.loop.IsAlive);

            this.loop.RunDefault();

            Assert.False(this.loop.IsAlive);
            Assert.Equal(1, this.workCount);
            Assert.Equal(1, this.afterWorkCount);
        }

        void OnTimer(Timer handle) => this.timerCount++;

        void OnWork(Work work) => this.workCount++;

        void OnAfterWork(Work work) => this.afterWorkCount++;

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        } 
    }
}
