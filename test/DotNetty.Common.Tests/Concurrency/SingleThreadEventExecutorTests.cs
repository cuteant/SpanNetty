// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Tests.Concurrency
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using DotNetty.Tests.Common;
    using Xunit;
    using Xunit.Abstractions;

    public class SingleThreadEventExecutorTests : TestBase
    {
        public SingleThreadEventExecutorTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void TaskSchedulerIsPreserved()
        {
            var executor = new DefaultEventExecutor(); // SingleThreadEventExecutor("test", TimeSpan.FromSeconds(5));
            IEnumerable<Task<int>> tasks = Enumerable.Range(1, 1).Select(async i =>
            {
                //Clear SynchronizationContext set by xunit
                SynchronizationContext.SetSynchronizationContext(null);

                var completion = new DefaultPromise();
                executor.Execute(async () =>
                {
                    try
                    {
                        Assert.True(executor.InEventLoop);
                        await Task.Delay(1);
                        Assert.True(executor.InEventLoop);
                        completion.TryComplete(); // all is well
                    }
                    catch (Exception ex)
                    {
                        completion.TrySetException(ex);
                    }
                });
                await completion.Task;
                Assert.False(executor.InEventLoop);
                return i;
            });

            Task.WhenAll(tasks).Wait(TimeSpan.FromSeconds(500));
            executor.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }

        [Theory]
        [InlineData(1, true, 20)]
        [InlineData(10, true, 20)]
        [InlineData(1, false, 20)]
        public void FuzzyScheduling(int producerCount, bool perCpu, int taskPerProducer)
        {
            int producerCountFinal = perCpu ? producerCount * Environment.ProcessorCount : producerCount;
            var valueContainer = new Container<int>
            {
                Value = taskPerProducer * producerCountFinal
            };
            var mre = new ManualResetEvent(false);
            Action noop = () =>
            {
                if (--valueContainer.Value <= 0)
                {
                    Assert.Equal(0, valueContainer.Value);
                    mre.Set();
                }
            };
            var scheduler = new DefaultEventExecutor(); // SingleThreadEventExecutor("test", TimeSpan.FromSeconds(1));
            IEnumerable<Task<Task>> producers = Enumerable.Range(1, producerCountFinal).Select(x => Task.Factory.StartNew(
                async () =>
                {
                    var r = new Random((int)Stopwatch.GetTimestamp() ^ x);
                    for (int i = 0; i < taskPerProducer; i++)
                    {
                        scheduler.Execute(noop);
                        await Task.Delay(r.Next(10, 100));
                    }
                },
                TaskCreationOptions.LongRunning));
            Task.WhenAll(producers).Wait();
            Assert.True(mre.WaitOne(TimeSpan.FromSeconds(5)));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ScheduledTaskFiresOnTime(bool scheduleFromExecutor)
        {
            var scheduler = new DefaultEventExecutor(); // SingleThreadEventExecutor(null, TimeSpan.FromMinutes(1))
            var promise = new DefaultPromise();
            Func<Task> scheduleFunc = () => scheduler.ScheduleAsync(() => promise.Complete(), TimeSpan.FromMilliseconds(100));
            Task task = scheduleFromExecutor ? await scheduler.SubmitAsync(scheduleFunc) : scheduleFunc();
            await Task.WhenAny(task, Task.Delay(TimeSpan.FromMilliseconds(300)));
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public async Task ScheduledTaskFiresOnTimeWhileBusy()
        {
            var scheduler = new DefaultEventExecutor();
            var promise = new DefaultPromise();
            Action selfQueueAction = null;
            selfQueueAction = () =>
            {
                if (!promise.Task.IsCompleted)
                {
                    scheduler.Execute(selfQueueAction);
                }
            };

            scheduler.Execute(selfQueueAction);
            Task task = scheduler.ScheduleAsync(() => promise.Complete(), TimeSpan.FromMilliseconds(100));
            await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(1)));
            Assert.True(task.IsCompleted);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(200)]
        public async Task ShutdownWhileIdle(int delayInMs)
        {
            var scheduler = new DefaultEventExecutor();
            if (delayInMs > 0)
            {
                Thread.Sleep(delayInMs);
            }
            Task shutdownTask = scheduler.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(50), TimeSpan.FromSeconds(1));
            await Task.WhenAny(shutdownTask, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.True(shutdownTask.IsCompleted);
        }

        private static void ExecuteShouldFail(IEventExecutor executor)
        {
            try
            {
                executor.Execute(NoopRunnable.Instance);
                Assert.False(true);
            }
            catch (RejectedExecutionException)
            {
                // expected
            }
        }

        [Fact]
        public void TestLazyExecution()
        {
            var executor = new LazyTestExecutor();

            // Ensure event loop is started
            LatchTask latch0 = new LatchTask();
            executor.Execute(latch0);
            Assert.True(latch0.Wait(100));
            // Pause to ensure it enters waiting state
            Thread.Sleep(100);

            // Submit task via lazyExecute
            LatchTask latch1 = new LatchTask();
            executor.LazyExecute(latch1);
            // Sumbit lazy task via regular execute
            LatchTask latch2 = new LazyLatchTask();
            executor.Execute(latch2);

            // Neither should run yet
            Assert.False(latch1.Wait(100));
            Assert.False(latch2.Wait(100));

            // Submit regular task via regular execute
            LatchTask latch3 = new LatchTask();
            executor.Execute(latch3);

            // Should flush latch1 and latch2 and then run latch3 immediately
            Assert.True(latch3.Wait(100));
            Assert.Equal(0, latch1.CurrentCount);
            Assert.Equal(0, latch2.CurrentCount);
        }

        class LatchTask : CountdownEvent, IRunnable
        {
            public LatchTask() : base(1) { }

            public void Run()
            {
                Signal();
            }
        }

        class LazyLatchTask : LatchTask, ILazyRunnable
        {
        }

        sealed class LazyTestExecutor : SingleThreadEventExecutor
        {
            private readonly ManualResetEventSlim _rstEvent;

            public LazyTestExecutor()
                : base(DefaultThreadFactory<LazyTestExecutor>.Instance, false)
            {
                _rstEvent = new ManualResetEventSlim(false, 1);
                Start();
            }

            protected override IQueue<IRunnable> NewTaskQueue(int maxPendingTasks)
            {
                return new CompatibleConcurrentQueue<IRunnable>();
            }

            protected override void Run()
            {
                while (!ConfirmShutdown())
                {
                    try
                    {
                        if (!HasTasks)
                        {
                            _rstEvent.Wait();
                            _rstEvent.Reset();
                        }
                        RunAllTasks();
                    }
                    catch (Exception)
                    {
                        Assert.False(true);
                    }
                }
            }

            protected override void InternalLazyExecute(IRunnable task)
            {
                LazyExecute(task);
            }

            protected internal override void WakeUp(bool inEventLoop)
            {
                if (!InEventLoop)
                {
                    _rstEvent.Set();
                }
            }
        }

        [Fact]
        public void TestTaskAddedAfterShutdownNotAbandoned()
        {
            BlockingCollection<Task<int>> submittedTasks = new BlockingCollection<Task<int>>();
            AtomicInteger attempts = new AtomicInteger();
            AtomicInteger rejects = new AtomicInteger();

            var executor = new NotAbandonedTestExecutor(submittedTasks, attempts, rejects);

            // Start the loop
            executor.SubmitAsync(() => 0).GetAwaiter().GetResult();

            // Shutdown without any quiet period
            executor.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.FromMilliseconds(100)).GetAwaiter().GetResult();

            // Ensure there are no user-tasks left.
            Assert.Equal(0, executor.DrainTasks());

            // Verify that queue is empty and all attempts either succeeded or were rejected
            Assert.Equal(0, executor.PendingTasks);
            Assert.True(attempts.Value > 0);
            Assert.Equal(attempts.Value, submittedTasks.Count + rejects.Value);
            foreach (var f in submittedTasks)
            {
                Assert.True(f.IsSuccess());
            }
        }

        sealed class NotAbandonedTestExecutor : SingleThreadEventExecutor
        {
            private readonly BlockingCollection<Task<int>> _submittedTasks;
            private readonly AtomicInteger _attempts;
            private readonly AtomicInteger _rejects;

            public NotAbandonedTestExecutor(BlockingCollection<Task<int>> submittedTasks, AtomicInteger attempts, AtomicInteger rejects)
                : base(DefaultThreadFactory<LazyTestExecutor>.Instance, false)
            {
                _submittedTasks = submittedTasks;
                _attempts = attempts;
                _rejects = rejects;
                Start();
            }

            protected override void Run()
            {
                while (!ConfirmShutdown())
                {
                    IRunnable task = TakeTask();
                    if (task is object)
                    {
                        task.Run();
                    }
                }
            }

            protected override bool ConfirmShutdown()
            {
                var result = base.ConfirmShutdown();
                // After shutdown is confirmed, scheduled one more task and record it
                if (result)
                {
                    _attempts.Increment();
                    try
                    {
                        _submittedTasks.Add(SubmitAsync(() => 0));
                    }
                    catch (RejectedExecutionException)
                    {
                        // ignore, tasks are either accepted or rejected
                        _rejects.Increment();
                    }
                }
                return result;
            }
        }

        [Fact]
        public void TestTakeTask()
        {
            var executor = new DefaultTestExecutor(true);

            //add task
            TestRunnable beforeTask = new TestRunnable();
            executor.Execute(beforeTask);

            //add scheduled task
            TestRunnable scheduledTask = new TestRunnable();
            var f = executor.Schedule(scheduledTask, TimeSpan.FromMilliseconds(1500));

            //add task
            TestRunnable afterTask = new TestRunnable();
            executor.Execute(afterTask);

            f.Completion.GetAwaiter().GetResult();

            Assert.True(beforeTask.Ran.Value);
            Assert.True(scheduledTask.Ran.Value);
            Assert.True(afterTask.Ran.Value);

            executor.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero);
        }

        [Fact]
        public void TestTakeTaskAlwaysHasTask()
        {
            // for https://github.com/netty/netty/issues/1614
            var executor = new DefaultTestExecutor(true);

            //add scheduled task
            TestRunnable t = new TestRunnable();
            var f = executor.Schedule(t, TimeSpan.FromMilliseconds(1500));

            AtomicBoolean stop = new AtomicBoolean(false);

            //ensure always has at least one task in taskQueue
            //check if scheduled tasks are triggered
            try
            {
                Task.Run(() =>
                {
                    while (!stop.Value)
                    {
                        executor.Execute(NoopRunnable.Instance);
                    }
                    executor.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero);
                });
                f.Completion.GetAwaiter().GetResult();

                Assert.True(t.Ran.Value);
            }
            finally
            {
                stop.Value = true;
            }
        }

        sealed class DefaultTestExecutor : SingleThreadEventExecutor
        {
            public DefaultTestExecutor(bool addTaskWakesUp)
                : base(DefaultThreadFactory<DefaultTestExecutor>.Instance, addTaskWakesUp)
            {
                Start();
            }

            protected override void Run()
            {
                while (!ConfirmShutdown())
                {
                    IRunnable task = TakeTask();
                    if (task is object)
                    {
                        task.Run();
                    }
                }
            }
        }

        sealed class NoopRunnable : IRunnable
        {
            public static readonly NoopRunnable Instance = new NoopRunnable();

            public void Run()
            {
                // NOOP
            }
        }

        sealed class TestRunnable : IRunnable
        {
            internal readonly AtomicBoolean Ran;

            public TestRunnable()
            {
                Ran = new AtomicBoolean();
            }

            public void Run()
            {
                Ran.Value = true;
            }
        }

        class Container<T>
        {
            public T Value;
        }
    }
}