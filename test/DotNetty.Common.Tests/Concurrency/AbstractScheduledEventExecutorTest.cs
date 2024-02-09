
namespace DotNetty.Common.Tests.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using Xunit;

    public class AbstractScheduledEventExecutorTest
    {
        [Fact]
        public void ScheduleRunnableZero()
        {
            TestScheduledEventExecutor executor = new TestScheduledEventExecutor();
            var future = executor.Schedule(() => { }, TimeSpan.Zero);
            Assert.Equal(0, future.DelayNanos);
            Assert.NotNull(executor.PollScheduledTask());
            Assert.Null(executor.PollScheduledTask());
        }

        [Fact]
        public void ScheduleRunnableNegative()
        {
            TestScheduledEventExecutor executor = new TestScheduledEventExecutor();
            var future = executor.Schedule(() => { }, TimeSpan.FromMilliseconds(-1));
            Assert.Equal(0, future.DelayNanos);
            Assert.NotNull(executor.PollScheduledTask());
            Assert.Null(executor.PollScheduledTask());
        }

        [Fact]
        public void TestScheduleAtFixedRateRunnableZero()
        {
            TestScheduledEventExecutor executor = new TestScheduledEventExecutor();
            Assert.Throws<ArgumentException>(() => executor.ScheduleAtFixedRate(TestRunnable.Instance, TimeSpan.Zero, TimeSpan.Zero));
        }

        [Fact]
        public void TestScheduleAtFixedRateRunnableNegative()
        {
            TestScheduledEventExecutor executor = new TestScheduledEventExecutor();
            Assert.Throws<ArgumentException>(() => executor.ScheduleAtFixedRate(TestRunnable.Instance, TimeSpan.Zero, TimeSpan.FromDays(-1)));
        }

        [Fact]
        public void TestScheduleWithFixedDelayZero()
        {
            TestScheduledEventExecutor executor = new TestScheduledEventExecutor();
            Assert.Throws<ArgumentException>(() => executor.ScheduleWithFixedDelay(TestRunnable.Instance, TimeSpan.Zero, TimeSpan.Zero));
        }

        [Fact]
        public void TestScheduleWithFixedDelayNegative()
        {
            TestScheduledEventExecutor executor = new TestScheduledEventExecutor();
            Assert.Throws<ArgumentException>(() => executor.ScheduleWithFixedDelay(TestRunnable.Instance, TimeSpan.Zero, TimeSpan.FromDays(-1)));
        }

        sealed class TestRunnable : IRunnable
        {
            public static readonly TestRunnable Instance = new TestRunnable();

            public void Run()
            {
                // NOOP
            }
        }

        sealed class TestScheduledEventExecutor : AbstractScheduledEventExecutor
        {
            protected override bool HasTasks => false;

            public override bool IsShuttingDown => false;

            public override Task TerminationCompletion => throw new NotImplementedException();

            public override bool IsShutdown => false;

            public override bool IsTerminated => false;

            protected override IEnumerable<IEventExecutor> GetItems() => new[] { this };

            public override void Execute(IRunnable task)
            {
                throw new NotImplementedException();
            }

            public override bool IsInEventLoop(XThread thread)
            {
                return true;
            }

            public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
            {
                throw new NotImplementedException();
            }

            public override bool WaitTermination(TimeSpan timeout)
            {
                return false;
            }
        }
    }
}
