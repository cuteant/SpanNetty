// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using DotNetty.Transport.Libuv.Handles;
    using Xunit;

    public sealed class CloseOrderTests : IDisposable
    {
        Loop loop;
        Timer timer2;

        int closeCount;
        int checkCount;
        int timerCount;
        bool checkResult;

        [Fact]
        public void Run()
        {
            this.loop = new Loop();
            this.closeCount = 0;

            this.loop
                .CreateCheck()
                .Start(this.OnCheck);

            this.loop
                .CreateTimer()
                .Start(this.OnTimer, 0, 0);

            this.timer2 = this.loop
                .CreateTimer()
                .Start(this.OnTimer, 100000, 0);

            Assert.Equal(0, this.closeCount);
            Assert.Equal(0, this.checkCount);
            Assert.Equal(0, this.timerCount);

            this.loop.RunDefault();

            Assert.Equal(3, this.closeCount);
            Assert.Equal(1, this.checkCount);
            Assert.Equal(1, this.timerCount);
            Assert.True(this.checkResult);
        }

        // check_cb should run before any close_cb
        void OnCheck(Check check)
        {
            this.checkResult = 
                this.checkCount == 0 
                && this.timerCount == 1 
                && this.closeCount == 0;

            check.CloseHandle(this.OnClose);
            this.timer2.CloseHandle(this.OnClose);
            this.checkCount++;
        }

        void OnTimer(Timer timer)
        {
            this.timerCount++;
            timer.CloseHandle(this.OnClose);
        }

        void OnClose(IScheduleHandle handle)
        {
            handle.Dispose();
            this.closeCount++;
        }

        public void Dispose()
        {
            this.timer2?.Dispose();
            this.timer2 = null;

            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
