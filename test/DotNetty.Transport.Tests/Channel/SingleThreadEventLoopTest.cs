namespace DotNetty.Transport.Tests.Channel
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using Xunit;

    [Collection("EventLoopTests")]
    public class SingleThreadEventLoopTest : IDisposable
    {
        private SingleThreadEventLoopA _loopA;
        private SingleThreadEventLoopB _loopB;
        private SingleThreadEventLoopC _loopC;

        public SingleThreadEventLoopTest()
        {
            _loopA = new SingleThreadEventLoopA();
            _loopB = new SingleThreadEventLoopB();
            _loopC = new SingleThreadEventLoopC();
        }

        public void Dispose()
        {
            if (!_loopA.IsShuttingDown)
            {
                try
                {
                    _loopA.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Ignore();
                }
                catch { }
            }
            if (!_loopB.IsShuttingDown)
            {
                try
                {
                    _loopB.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Ignore();
                }
                catch { }
            }
            if (!_loopC.IsShuttingDown)
            {
                try
                {
                    _loopC.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Ignore();
                }
                catch { }
            }

            while (!_loopA.IsTerminated)
            {
                _loopA.WaitTermination(TimeSpan.FromDays(1));
            }
            Assert.Equal(1, _loopA._cleanedUp.Value);

            while (!_loopB.IsTerminated)
            {
                _loopB.WaitTermination(TimeSpan.FromDays(1));
            }
            while (!_loopC.IsTerminated)
            {
                _loopC.WaitTermination(TimeSpan.FromDays(1));
            }
        }

        [Fact]
        public void ShutdownBeforeStart()
        {
            _loopA.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
            AssertRejection(_loopA);
        }

        [Fact]
        public void ShutdownAfterStart()
        {
            CountdownEvent latch = new CountdownEvent(1);
            _loopA.Execute(() =>
            {
                latch.Signal();
            });

            // Wait for the event loop thread to start.
            Assert.True(latch.Wait(TimeSpan.FromMinutes(1)));

            // Request the event loop thread to stop.
            _loopA.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
            AssertRejection(_loopA);

            Assert.True(_loopA.IsShutdown);

            // Wait until the event loop is terminated.
            while (!_loopA.IsTerminated)
            {
                _loopA.WaitTermination(TimeSpan.FromDays(1));
            }
        }

        private static void AssertRejection(IEventExecutor loop)
        {
            try
            {
                loop.Execute(NOOP.Instance);
                Assert.False(true, "A task must be rejected after shutdown() is called.");
            }
            catch (Exception exc)
            {
                Assert.IsType<RejectedExecutionException>(exc);
            }
        }

        [Fact]
        public void ScheduleTaskA()
        {
            TestScheduleTask(_loopA);
        }

        [Fact]
        public void ScheduleTaskB()
        {
            TestScheduleTask(_loopB);
        }

        [Fact]
        public void ScheduleTaskC()
        {
            TestScheduleTask(_loopC);
        }

        private void TestScheduleTask(IEventLoop loopA)
        {
            long startTime = Stopwatch.GetTimestamp();
            AtomicLong endTime = new AtomicLong();
            var f = loopA.Schedule(() =>
            {
                endTime.Value = Stopwatch.GetTimestamp();
            }, TimeSpan.FromMilliseconds(500));

            f.Completion.GetAwaiter().GetResult();

            Assert.True(endTime.Value - startTime >= PreciseTime.ToDelayNanos(TimeSpan.FromMilliseconds(500)));
        }

        [Fact]
        public void ScheduleTaskAtFixedRateA()
        {
            TestScheduleTaskAtFixedRate(_loopA);
        }

        [Fact]
        public void ScheduleTaskAtFixedRateB()
        {
            TestScheduleTaskAtFixedRate(_loopB);
        }

        private void TestScheduleTaskAtFixedRate(IEventLoop loopA)
        {
            var timestamps = new BlockingCollection<long>();
            int expectedTimeStamps = 5;
            var allTimeStampsLatch = new CountdownEvent(expectedTimeStamps);
            var f = loopA.ScheduleAtFixedRate(() =>
            {
                timestamps.Add(Stopwatch.GetTimestamp());
                try
                {
                    Thread.Sleep(50);
                }
                catch { }
                allTimeStampsLatch.Signal();
            }, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
            Assert.True(allTimeStampsLatch.Wait(TimeSpan.FromMinutes(1)));
            Assert.True(f.Cancel());
            Thread.Sleep(300);
            Assert.Equal(expectedTimeStamps, timestamps.Count);

            // Check if the task was run without a lag.
            long? firstTimestamp = null;
            int cnt = 0;
            foreach (long t in timestamps)
            {
                if (firstTimestamp == null)
                {
                    firstTimestamp = t;
                    continue;
                }

                long timepoint = t - firstTimestamp.Value;
                Assert.True(timepoint >= PreciseTime.ToDelayNanos(TimeSpan.FromMilliseconds(100 * cnt + 80)));
                Assert.True(timepoint <= PreciseTime.ToDelayNanos(TimeSpan.FromMilliseconds(100 * (cnt + 1) + 20)));

                cnt++;
            }
        }

        [Fact]
        public void ScheduleLaggyTaskAtFixedRateA()
        {
            TestScheduleLaggyTaskAtFixedRate(_loopA);
        }

        [Fact]
        public void ScheduleLaggyTaskAtFixedRateB()
        {
            TestScheduleLaggyTaskAtFixedRate(_loopB);
        }

        private void TestScheduleLaggyTaskAtFixedRate(IEventLoop loopA)
        {
            var timestamps = new BlockingCollection<long>();
            int expectedTimeStamps = 5;
            var allTimeStampsLatch = new CountdownEvent(expectedTimeStamps);
            var f = loopA.ScheduleAtFixedRate(() =>
            {
                var empty = timestamps.Count == 0;
                timestamps.Add(Stopwatch.GetTimestamp());
                if (empty)
                {
                    try
                    {
                        Thread.Sleep(401);
                    }
                    catch { }
                }
                allTimeStampsLatch.Signal();
            }, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
            Assert.True(allTimeStampsLatch.Wait(TimeSpan.FromMinutes(1)));
            Assert.True(f.Cancel());
            Thread.Sleep(300);
            Assert.Equal(expectedTimeStamps, timestamps.Count);

            // Check if the task was run with lag.
            int i = 0;
            long? previousTimestamp = null;
            foreach (long t in timestamps)
            {
                if (previousTimestamp == null)
                {
                    previousTimestamp = t;
                    continue;
                }

                long diff = t - previousTimestamp.Value;
                if (i == 0)
                {
                    Assert.True(diff >= PreciseTime.ToDelayNanos(TimeSpan.FromMilliseconds(400)));
                }
                else
                {
                    Assert.True(diff <= PreciseTime.ToDelayNanos(TimeSpan.FromMilliseconds(10)));
                }
                previousTimestamp = t;
                i++;
            }
        }

        [Fact]
        public void ScheduleTaskWithFixedDelayA()
        {
            TestScheduleTaskWithFixedDelay(_loopA);
        }

        [Fact]
        public void ScheduleTaskWithFixedDelayB()
        {
            TestScheduleTaskWithFixedDelay(_loopB);
        }

        private void TestScheduleTaskWithFixedDelay(IEventLoop loopA)
        {
            var timestamps = new BlockingCollection<long>();
            int expectedTimeStamps = 3;
            var allTimeStampsLatch = new CountdownEvent(expectedTimeStamps);
            var f = loopA.ScheduleWithFixedDelay(() =>
            {
                timestamps.Add(Stopwatch.GetTimestamp());
                try
                {
                    Thread.Sleep(51);
                }
                catch { }
                allTimeStampsLatch.Signal();
            }, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
            Assert.True(allTimeStampsLatch.Wait(TimeSpan.FromMinutes(1)));
            Assert.True(f.Cancel());
            Thread.Sleep(300);
            Assert.Equal(expectedTimeStamps, timestamps.Count);

            // Check if the task was run without a lag.
            long? previousTimestamp = null;
            foreach (long t in timestamps)
            {
                if (previousTimestamp is null)
                {
                    previousTimestamp = t;
                    continue;
                }

                Assert.True(t - previousTimestamp.Value >= PreciseTime.ToDelayNanos(TimeSpan.FromMilliseconds(150)));
                previousTimestamp = t;
            }
        }

        [Fact]
        public void ShutdownWithPendingTasks()
        {
            int NUM_TASKS = 3;
            AtomicInteger ranTasks = new AtomicInteger();
            CountdownEvent latch = new CountdownEvent(1);
            Action task = () =>
            {
                ranTasks.Increment();
                while (latch.CurrentCount > 0)
                {
                    try
                    {
                        Assert.True(latch.Wait(TimeSpan.FromMinutes(1)));
                    }
                    catch (Exception) { }
                }
            };

            for (int i = 0; i < NUM_TASKS; i++)
            {
                _loopA.Execute(task);
            }

            // At this point, the first task should be running and stuck at latch.await().
            while (ranTasks.Value == 0)
            {
                Thread.Yield();
            }
            Assert.Equal(1, ranTasks.Value);

            // Shut down the event loop to test if the other tasks are run before termination.
            _loopA.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero);

            // Let the other tasks run.
            latch.Signal();

            // Wait until the event loop is terminated.
            while (!_loopA.IsTerminated)
            {
                _loopA.WaitTermination(TimeSpan.FromDays(1));
            }

            // Make sure loop.shutdown() above triggered wakeup().
            Assert.Equal(NUM_TASKS, ranTasks.Value);
        }

        [Fact]
        public async Task TestRegistrationAfterShutdown()
        {
            await _loopA.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero);
            IChannel ch = new LocalChannel();
            try
            {
                await _loopA.RegisterAsync(ch);
                Assert.False(true);
            }
            catch (Exception exc)
            {
                Assert.IsType<RejectedExecutionException>(exc);
            }
            Assert.False(ch.IsOpen);
        }

        [Fact]
        public async Task TestRegistrationAfterShutdown2()
        {
            await _loopA.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero);
            var latch = new CountdownEvent(1);
            IChannel ch = new LocalChannel();
            try
            {
                var f = _loopA.RegisterAsync(ch);
                f.ContinueWith(t =>
                {
                    if (t.IsSuccess())
                    {
                        latch.Signal();
                    }
                }).Ignore();
                await f;
                Assert.False(true);
            }
            catch (Exception exc)
            {
                Assert.IsType<RejectedExecutionException>(exc);
            }
            // Ensure the listener was notified.
            Assert.False(latch.Wait(TimeSpan.FromSeconds(1)));
            Assert.False(ch.IsOpen);
        }

        [Fact]
        public void TestGracefulShutdownQuietPeriod()
        {
            _loopA.ShutdownGracefullyAsync(TimeSpan.FromSeconds(1), TimeSpan.FromDays(1));
            // Keep Scheduling tasks for another 2 seconds.
            for (int i = 0; i < 20; i++)
            {
                Thread.Sleep(100);
                _loopA.Execute(NOOP.Instance);
            }

            long startTime = PreciseTime.NanoTime();

            Assert.True(_loopA.IsShuttingDown);
            Assert.False(_loopA.IsShutdown);

            while (!_loopA.IsTerminated)
            {
                _loopA.WaitTermination(TimeSpan.FromDays(1));
            }

            Assert.True(PreciseTime.NanoTime() - startTime >= PreciseTime.ToDelayNanos(TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void TestGracefulShutdownTimeout()
        {
            _loopA.ShutdownGracefullyAsync(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
            // Keep Scheduling tasks for another 3 seconds.
            // Submitted tasks must be rejected after 2 second timeout.
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(100);
                _loopA.Execute(NOOP.Instance);
            }

            try
            {
                for (int i = 0; i < 20; i++)
                {
                    Thread.Sleep(100);
                    _loopA.Execute(NOOP.Instance);
                }
                Assert.False(true, "shutdownGracefully() must reject a task after timeout.");
            }
            catch (RejectedExecutionException)
            {
                // Expected
            }

            Assert.True(_loopA.IsShuttingDown);
            Assert.True(_loopA.IsShutdown);
        }

        [Fact]
        public void TestOnEventLoopIteration()
        {
            CountingRunnable onIteration = new CountingRunnable();
#if DEBUG
            _loopC.ExecuteAfterEventLoopIteration(onIteration);
#endif
            CountingRunnable noopTask = new CountingRunnable();
            _loopC.SubmitAsync(() =>
            {
                noopTask.Run();
                return 0;
            }).GetAwaiter().GetResult();
            _loopC._iterationEndSignal.Take();
            Assert.Equal(1, noopTask.GetInvocationCount()); // Unexpected invocation count for regular task.
            Assert.Equal(1, onIteration.GetInvocationCount()); // Unexpected invocation count for on every eventloop iteration task.
        }

        [Fact]
        public void TestRemoveOnEventLoopIteration()
        {
            CountingRunnable onIteration2 = new CountingRunnable();
#if DEBUG
            CountingRunnable onIteration1 = new CountingRunnable();
            _loopC.ExecuteAfterEventLoopIteration(onIteration1);
            _loopC.ExecuteAfterEventLoopIteration(onIteration2);
#endif
            //_loopC.RemoveAfterEventLoopIterationTask(onIteration1);
            CountingRunnable noopTask = new CountingRunnable();
            _loopC.SubmitAsync(() =>
            {
                noopTask.Run();
                return 0;
            }).GetAwaiter().GetResult();

            _loopC._iterationEndSignal.Take();
            Assert.Equal(1, noopTask.GetInvocationCount()); // Unexpected invocation count for regular task.
            Assert.Equal(1, onIteration2.GetInvocationCount()); // Unexpected invocation count for on every eventloop iteration task.
            //Assert.Equal(0, onIteration1.GetInvocationCount()); // Unexpected invocation count for on every eventloop iteration task.
        }

        sealed class SingleThreadEventLoopA : SingleThreadEventLoopBase
        {
            internal AtomicInteger _cleanedUp = new AtomicInteger();

            public SingleThreadEventLoopA()
                : base(null, DefaultThreadFactory<SingleThreadEventLoopA>.Instance, true)
            {
                Start();
            }

            protected override void Run()
            {
                for (; ; )
                {
                    IRunnable task = TakeTask();
                    if (task != null)
                    {
                        task.Run();
                        UpdateLastExecutionTime();
                    }

                    if (ConfirmShutdown()) { break; }
                }
            }

            protected override void Cleanup()
            {
                _cleanedUp.Increment();
            }
        }

        class SingleThreadEventLoopB : SingleThreadEventLoop
        {
            public SingleThreadEventLoopB()
                : base(null)
            {
            }

            protected override void Run()
            {
                for (; ; )
                {
                    RunTasks0();

                    if (ConfirmShutdown())
                    {
                        break;
                    }
                }
            }

            protected virtual void RunTasks0()
            {
                RunAllTasks();
            }
        }

        class SingleThreadEventLoopC : SingleThreadEventLoopB
        {
            internal BlockingCollection<bool> _iterationEndSignal = new BlockingCollection<bool>();

            protected override void AfterRunningAllTasks()
            {
                base.AfterRunningAllTasks();
                _iterationEndSignal.Add(true);
            }

            protected override void RunTasks0()
            {
                RunAllTasks(PreciseTime.ToDelayNanos(TimeSpan.FromMinutes(1)));
            }
        }

        class NOOP : IRunnable
        {
            public static readonly NOOP Instance = new NOOP();

            public void Run()
            {
            }
        }

        class CountingRunnable : IRunnable
        {
            private readonly AtomicInteger _invocationCount = new AtomicInteger();

            public void Run()
            {
                _invocationCount.Increment();
            }

            public int GetInvocationCount()
            {
                return _invocationCount.Value;
            }

            public void ResetInvocationCount()
            {
                _invocationCount.Value = 0;
            }
        }
    }
}