// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using DotNetty.NetUV.Handles;
    using Xunit;

    public sealed class LoopStopTests : IDisposable
    {
        const int NumberOfticks = 10;
        Loop loop;

        int timerCalled;
        int prepareCalled;

        void OnPrepare(Prepare handle)
        {
            this.prepareCalled++;
            if (this.prepareCalled == NumberOfticks)
            {
                handle.Stop();
            }
        }

        void OnTimer(Timer handle)
        {
            this.timerCalled++;
            if (this.timerCalled == 1)
            {
                this.loop?.Stop();
            }
            else if (this.timerCalled == NumberOfticks)
            {
                handle.Stop();
            }
        }

        [Fact]
        public void Stop()
        {
            this.loop = new Loop();

            Prepare prepare = this.loop.CreatePrepare();
            prepare.Start(this.OnPrepare);

            Timer timer = this.loop.CreateTimer();
            timer.Start(this.OnTimer, 100, 100);

            this.loop.RunDefault();
            Assert.Equal(1, this.timerCalled);

            this.loop.RunNoWait();
            Assert.True(this.prepareCalled > 1);

            this.loop.RunDefault();
            Assert.Equal(10, this.timerCalled);
            Assert.Equal(10, this.prepareCalled);
        }

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
