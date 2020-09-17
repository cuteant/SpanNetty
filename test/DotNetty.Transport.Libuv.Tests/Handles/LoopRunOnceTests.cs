// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using DotNetty.Transport.Libuv.Handles;
    using Xunit;

    public sealed class LoopRunOnceTests : IDisposable
    {
        const int NumberOfTicks = 64;

        Loop loop;
        int idleCounter;

        void OnIdle(Idle handle)
        {
            if (handle != null)
            {
                this.idleCounter++;

                if (this.idleCounter == NumberOfTicks)
                {
                    handle.Stop();
                }
            }
        }

        [Fact]
        public void Once()
        {
            this.loop = new Loop();

            Idle idle = this.loop.CreateIdle();
            idle.Start(this.OnIdle);

            while (this.loop.RunOnce() != 0)
            {
                Assert.True(idle.IsValid);
            }
                

            Assert.Equal(NumberOfTicks, this.idleCounter);
        }

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
