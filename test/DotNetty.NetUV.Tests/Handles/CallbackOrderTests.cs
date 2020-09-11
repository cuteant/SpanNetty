// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using DotNetty.NetUV.Handles;
    using Xunit;

    public sealed class CallbackOrderTests : IDisposable
    {
        Loop loop;
        Idle idle;
        Timer timer;

        int idleCalled;
        int timerCalled;
        bool idleCallbackCheck;
        bool timerCallbackCheck;

        void OnIdle(Idle handle)
        {
            this.idleCallbackCheck = (this.idleCalled == 0 && this.timerCalled == 1);
            handle.Stop();
            this.idleCalled++;
        }

        void OnTimer(Timer handle)
        {
            this.timerCallbackCheck = (this.idleCalled == 0 && this.timerCalled == 0);
            handle.Stop();
            this.timerCalled++;
        }

        void NextTick(Idle handle)
        {
            handle.Stop();

            this.idle = this.loop.CreateIdle();
            this.idle.Start(this.OnIdle);

            this.timer = this.loop.CreateTimer();
            this.timer.Start(this.OnTimer, 0, 0);
        }

        [Fact]
        public void Run()
        {
            this.loop = new Loop();
            this.idleCallbackCheck = false;
            this.timerCallbackCheck = false;
            this.idleCalled = 0;
            this.timerCalled = 0;

            Idle idleStart = this.loop.CreateIdle();
            idleStart.Start(this.NextTick);

            Assert.Equal(0, this.idleCalled);
            Assert.Equal(0, this.timerCalled);

            this.loop.RunDefault();

            Assert.Equal(1, this.idleCalled);
            Assert.Equal(1, this.timerCalled);

            Assert.True(this.timerCallbackCheck);
            Assert.True(this.idleCallbackCheck);
        }

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }

    }
}
