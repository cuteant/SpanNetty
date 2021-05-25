// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using DotNetty.Transport.Libuv.Handles;
    using Xunit;

    /*
     * Purpose of this test is to check semantics of starting and stopping
     * prepare, check and idle watchers.
     *
     * - A watcher must be able to safely stop or close itself;
     * - Once a watcher is stopped or closed its callback should never be called.
     * - If a watcher is closed, it is implicitly stopped and its close_cb should
     *   be called exactly once.
     * - A watcher can safely start and stop other watchers of the same type.
     * - Prepare and check watchers are called once per event loop iterations.
     * - All active idle watchers are queued when the event loop has no more work
     *   to do. This is done repeatedly until all idle watchers are inactive.
     * - If a watcher starts another watcher of the same type its callback is not
     *   immediately queued. For check and prepare watchers, that means that if
     *   a watcher makes another of the same type active, it'll not be called until
     *   the next event loop iteration. For idle. watchers this means that the
     *   newly activated idle watcher might not be queued immediately.
     * - Prepare, check, idle watchers keep the event loop alive even when they're
     *   not active.
     *
     * This is what the test globally does:
     *
     * - prepare_1 is always active and counts event loop iterations. It also
     *   creates and starts prepare_2 every other iteration. Finally it verifies
     *   that no idle watchers are active before polling.
     * - prepare_2 is started by prepare_1 every other iteration. It immediately
     *   stops itself. It verifies that a watcher is not queued immediately
     *   if created by another watcher of the same type.
     * - There's a check watcher that stops the event loop after a certain number
     *   of iterations. It starts a varying number of idle_1 watchers.
     * - Idle_1 watchers stop themselves after being called a few times. All idle_1
     *   watchers try to start the idle_2 watcher if it is not already started or
     *   awaiting its close callback.
     * - The idle_2 watcher always exists but immediately closes itself after
     *   being started by a check_1 watcher. It verifies that a watcher is
     *   implicitly stopped when closed, and that a watcher can close itself
     *   safely.
     * - There is a repeating timer. It does not keep the event loop alive
     *   (ev_unref) but makes sure that the loop keeps polling the system for
     *   events.
     */

    public sealed class LoopHandleTests : IDisposable
    {
        const int Iterations = 21;
        const int IdleCount = 7;
        const int Timeout = 100;

        Loop loop;

        Prepare prepare1;
        Prepare prepare2;
        bool prepare2CallbackCheck = true;

        Check check;
        int checkCalled;

        Timer timer;

        int loopIteration;
        int prepare1Called;
        int prepare2Called;

        Idle[] idle1;
        int idles1Active;
        int idle1Called;

        Idle idle2;
        int idle2Called;
        int idle2Started;

        static void OnTimer(Timer handle) => handle.Dispose();

        void OnIdle2(Idle handle)
        {
            this.idle2Called++;
            handle.Dispose();
        }

        void OnIdle1(Idle handle)
        {
            /* Init idle 2 and make it active */
            if (this.idle2 == null 
                || !this.idle2.IsActive)
            {
                this.idle2 = this.loop.CreateIdle();
                this.idle2.Start(this.OnIdle2);

                this.idle2Started++;
            }

            this.idle1Called++;

            if (this.idle1Called % 5 == 0)
            {
                handle.Stop();
                this.idles1Active--;
            }
        }

        void CheckCallback(Check handle)
        {
            if (this.loopIteration < Iterations)
            {
                /* Make some idle watchers active */
                for (int i = 0; i < (this.loopIteration % IdleCount); i++)
                {
                    this.idle1[i].Start(this.OnIdle1);
                    this.idles1Active++;
                }
            }
            else
            {
                this.prepare1?.Dispose();
                this.check?.Dispose();
                this.prepare2?.Dispose();

                for (int i = 0; i < IdleCount; i++)
                {
                    this.idle1[i].Dispose();
                }

                /* This handle is closed/recreated every time, close it only if it is */
                /* active.*/
                this.idle2?.Dispose();
            }

            this.checkCalled++;
        }

        void Prepare2Callback(Prepare handle)
        {
            /* prepare2 gets started by prepare1 when (loop_iteration % 2 == 0), */
            /* and it stops itself immediately. A started watcher is not queued */
            /* until the next round, so when this callback is made */
            /* (loop_iteration % 2 == 0) cannot be true. */
            this.prepare2CallbackCheck &= this.loopIteration % 2 != 0;

            handle.Stop();
            this.prepare2Called++;
        }

        void Prepare1Callback(Prepare handle)
        {
            if (this.loopIteration % 2 == 0)
            {
                this.prepare2?.Start(this.Prepare2Callback);
            }

            this.prepare1Called++;
            this.loopIteration++;
        }

        [Fact]
        public void Handles()
        {
            this.loop = new Loop();

            /* initialize only, prepare2 is started by prepare1 callback */
            this.prepare2 = this.loop.CreatePrepare();

            this.idle1 = new Idle[IdleCount];
            for (int i = 0; i < IdleCount; i++)
            {
                /* don't init or start idle_2, both is done by idle_1_cb */
                this.idle1[i] = this.loop.CreateIdle();
            }

            this.prepare1 = this.loop.CreatePrepare();
            this.prepare1.Start(this.Prepare1Callback);

            this.check = this.loop.CreateCheck();
            this.check.Start(this.CheckCallback);

            /* the timer callback is there to keep the event loop polling */
            /* unref it as it is not supposed to keep the loop alive */
            this.timer = this.loop.CreateTimer();
            this.timer.Start(OnTimer, Timeout, Timeout);
            this.timer.RemoveReference();

            this.loop.RunDefault();

            Assert.Equal(Iterations, this.loopIteration);

            Assert.Equal(Iterations, this.prepare1Called);
            Assert.Equal((int)Math.Floor(Iterations / 2d), this.prepare2Called);
            Assert.True(this.prepare2CallbackCheck);

            Assert.Equal(Iterations, this.checkCalled);

            Assert.True(this.idles1Active > 0);
            Assert.True(this.idle1Called > 0);
            Assert.True(this.idle2Called <= this.idle2Started);

            for (int i = 0; i < IdleCount; i++)
            {
                Assert.False(this.idle1[i].IsActive);
            }
            Assert.False(this.idle2.IsActive);
        }

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
