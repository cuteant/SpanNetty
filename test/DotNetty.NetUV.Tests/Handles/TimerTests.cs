// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.NetUV.Tests.Handles
{
    using System;
    using System.Collections.Generic;
    using DotNetty.NetUV.Handles;
    using Xunit;

    public sealed class TimerTests : IDisposable
    {
        Loop loop;
        long timeInHighResolution;
        int runOnceTimerCalled;

        bool repeatCheck;
        int hugeRepeatCount;
        Timer tinyTimer;
        Timer hugeTimer1;
        Timer hugeTimer2;

        int repeatCalled;

        void OnTimerRepeat(Timer handle)
        {
            this.repeatCalled++;

            if (this.repeatCalled == 5)
            {
                handle.CloseHandle(OnClose);
            }
        }

        int onceCalled;

        void OnTimerOnce(Timer handle)
        {
            this.onceCalled++;

            handle.CloseHandle(OnClose);

            /* Just call this randomly for the code coverage. */
            this.loop?.UpdateTime();
        }

        [Fact]
        public void Timer()
        {
            this.loop = new Loop();

            var oncetimers = new Timer[10];

            long startTime = this.loop.Now;
            Assert.True(startTime > 0);

            for (int i = 0; i < oncetimers.Length; i++)
            {
                oncetimers[i] = this.loop.CreateTimer();
                oncetimers[i].Start(this.OnTimerOnce, i * 50, 0);
            }

            /* The 11th timer is a repeating timer that runs 4 times */
            Timer repeat = this.loop.CreateTimer();
            repeat.Start(this.OnTimerRepeat, 100, 100);

            this.neverCallback = false;

            /* The 12th timer should not do anything. */
            Timer never = this.loop.CreateTimer();
            never.Start(this.NeverCallback, 100, 100);
            never.Stop();
            never.RemoveReference();

            this.loop.RunDefault();

            Assert.False(this.neverCallback);
            Assert.Equal(10, this.onceCalled);
            Assert.Equal(5, this.repeatCalled);

            foreach (Timer timer in oncetimers)
            {
                Assert.False(timer.IsValid);
            }

            long duration = this.loop.Now - startTime;
            Assert.True(duration >= 500);
        }

        bool neverCallback;

        void NeverCallback(Timer handle) => this.neverCallback = true;

        [Fact]
        public void StartTwice()
        {
            this.loop = new Loop();
            this.neverCallback = false;

            Timer timer = this.loop.CreateTimer();
            timer.Start(this.NeverCallback, 86400 * 1000, 0);
            timer.Start(this.OrderCallback, 10, 0);

            int result = this.loop.RunDefault();
            Assert.Equal(0, result);
            Assert.Equal(1, this.orderCalled);
            Assert.False(this.neverCallback);
        }

        [Fact]
        public void Init()
        {
            this.loop = new Loop();

            Timer timer = this.loop.CreateTimer();
            Assert.False(timer.IsActive, "Timer should not be active when created.");

            long repeat = timer.GetRepeat();
            Assert.Equal(0, repeat);
        }

        int orderCalled;
        List<Timer> orderTimers;

        void OrderCallback(Timer handle)
        {
            this.orderTimers?.Add(handle);
            this.orderCalled++;
        }

        [Fact]
        public void Order()
        {
            this.loop = new Loop();

            this.orderTimers = new List<Timer>();

            Timer timer1 = this.loop.CreateTimer();
            Timer timer2 = this.loop.CreateTimer();

            /* Test for starting handle_a then handle_b */
            timer1.Start(this.OrderCallback, 0, 0);
            timer2.Start(this.OrderCallback, 0, 0);

            int result = this.loop.RunDefault();
            Assert.Equal(0, result);

            timer1.Stop();
            timer2.Stop();

            Assert.Equal(2, this.orderCalled);
            Assert.Equal(2, this.orderTimers.Count);
            Assert.True(object.ReferenceEquals(this.orderTimers[0], timer1));
            Assert.True(object.ReferenceEquals(this.orderTimers[1], timer2));

            this.orderTimers.Clear();
            this.orderCalled = 0;

            /* Test for starting handle_b then handle_a */
            timer2.Start(this.OrderCallback, 0, 0);
            timer1.Start(this.OrderCallback, 0, 0);

            result = this.loop.RunDefault();
            Assert.Equal(0, result);

            Assert.Equal(2, this.orderCalled);
            Assert.Equal(2, this.orderTimers.Count);
            Assert.True(object.ReferenceEquals(this.orderTimers[0], timer2));
            Assert.True(object.ReferenceEquals(this.orderTimers[1], timer1));

            this.orderTimers.Clear();
            this.orderTimers = null;
        }

        void TinyTimerCallback(Timer handle)
        {
            this.repeatCheck = handle != null && handle == this.tinyTimer;

            this.tinyTimer.CloseHandle(OnClose);
            this.hugeTimer1.CloseHandle(OnClose);
            this.hugeTimer2.CloseHandle(OnClose);
        }

        [Fact]
        public void HugeTimout()
        {
            this.loop = new Loop();

            this.tinyTimer = this.loop.CreateTimer();
            this.hugeTimer1 = this.loop.CreateTimer();
            this.hugeTimer2 = this.loop.CreateTimer();

            this.tinyTimer.Start(this.TinyTimerCallback, 1, 0);
            this.hugeTimer1.Start(this.TinyTimerCallback, 0xffffffffffffL, 0);
            this.hugeTimer2.Start(this.TinyTimerCallback, 1, long.MaxValue - 1);

            int result = this.loop.RunDefault();
            Assert.Equal(0, result);
            Assert.True(this.repeatCheck, "Timer callback instance is not correct.");
        }

        void HugeTimerCallback(Timer handle)
        {
            if (this.hugeRepeatCount == 0)
            {
                this.repeatCheck = handle == this.hugeTimer1;
            }
            else
            {
                this.repeatCheck &= handle == this.tinyTimer;
            }

            this.hugeRepeatCount++;
            if (this.hugeRepeatCount == 10)
            {
                this.tinyTimer.CloseHandle(OnClose);
                this.hugeTimer1.CloseHandle(OnClose);
            }
        }

        [Fact]
        public void HugeRepeat()
        {
            this.loop = new Loop();

            this.repeatCheck = false;

            this.tinyTimer = this.loop.CreateTimer();
            this.hugeTimer1 = this.loop.CreateTimer();

            this.tinyTimer.Start(this.HugeTimerCallback, 2, 2);
            this.hugeTimer1.Start(this.HugeTimerCallback, 1, long.MaxValue - 1);

            int result = this.loop.RunDefault();
            Assert.Equal(0, result);

            Assert.True(this.repeatCheck, "Timer callback order not correct.");

            Assert.NotNull(this.tinyTimer);
            Assert.False(this.tinyTimer.IsValid);

            Assert.NotNull(this.hugeTimer1);
            Assert.False(this.hugeTimer1.IsValid);
        }

        void RunOnceTimerCallback(Timer handle) => this.runOnceTimerCalled++;

        [Fact]
        public void RunOnce()
        {
            this.loop = new Loop();

            Timer timer = this.loop.CreateTimer();
            timer.Start(this.RunOnceTimerCallback, 0, 0);

            int result = this.loop.RunDefault();
            Assert.Equal(0, result);
            Assert.Equal(1, this.runOnceTimerCalled);

            timer.Start(this.RunOnceTimerCallback, 1, 0);
            result = this.loop.RunDefault();
            Assert.Equal(0, result);
            Assert.Equal(2, this.runOnceTimerCalled);

            timer.Dispose();

            result = this.loop.RunOnce();
            Assert.Equal(0, result);
        }

        void EarlyCheckTimerCallback(Timer handle) => this.timeInHighResolution = this.loop.NowInHighResolution / 1000000;

        [Fact]
        public void EarlyCheck()
        {
            this.loop = new Loop();

            const int Timeout = 10; // ms
            long earlyCheckExpectedTime = this.loop.Now + Timeout;

            Timer timer = this.loop.CreateTimer();
            timer.Start(this.EarlyCheckTimerCallback, Timeout, 0);

            int result = this.loop.RunDefault();
            Assert.Equal(0, result);

            Assert.True(this.timeInHighResolution >= earlyCheckExpectedTime, "Timer callback time should be greater than the Now + timout." );
            timer.Dispose();
            result = this.loop.RunDefault();
            Assert.Equal(0, result);
        }

        static void OnClose(ScheduleHandle handle) => handle.Dispose();

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
