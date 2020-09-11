// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using DotNetty.NetUV.Handles;
    using Xunit;

    public sealed class IdleTests : IDisposable
    {
        Loop loop;
        Idle idle;
        Check check;

        int idleCount;
        int checkCount;
        int timerCount;
        int closeCount;

        void OnIdle(Idle handle) => this.idleCount++;

        void OnCheck(Check handle) => this.checkCount++;

        void OnTimer(Timer handle)
        {
            this.idle?.CloseHandle(this.OnClose);
            this.check?.CloseHandle(this.OnClose);
            handle.CloseHandle(this.OnClose);

            this.timerCount++;
        }

        [Fact]
        public void IdleStarvation()
        {
            this.loop = new Loop();

            this.idle = this.loop.CreateIdle().Start(this.OnIdle);
            this.check = this.loop.CreateCheck().Start(this.OnCheck);
            this.loop.CreateTimer().Start(this.OnTimer, 50, 0);

            this.loop.RunDefault();

            Assert.True(this.idleCount > 0);
            Assert.Equal(1, this.timerCount);
            Assert.True(this.checkCount > 0);
            Assert.Equal(3, this.closeCount);
        }

        void OnClose(ScheduleHandle handle)
        {
            handle.Dispose();
            this.closeCount++;
        }

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
