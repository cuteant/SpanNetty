
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
            var future = executor.Schedule(() => { }, TimeSpan.FromMilliseconds(0));
            //Assert.Equal(0, future.getDelay(TimeUnit.NANOSECONDS));
            Assert.NotNull(executor.PollScheduledTask());
            Assert.Null(executor.PollScheduledTask());
        }

        [Fact]
        public void ScheduleRunnableNegative()
        {
            TestScheduledEventExecutor executor = new TestScheduledEventExecutor();
            var future = executor.Schedule(() => { }, TimeSpan.FromMilliseconds(-1));
            //assertEquals(0, future.getDelay(TimeUnit.NANOSECONDS));
            Assert.NotNull(executor.PollScheduledTask());
            Assert.Null(executor.PollScheduledTask());
        }

        sealed class TestScheduledEventExecutor : AbstractScheduledEventExecutor
        {
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
        }
    }
}
