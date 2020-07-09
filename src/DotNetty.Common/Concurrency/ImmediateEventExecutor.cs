namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using DotNetty.Common.Utilities;

    public class ImmediateEventExecutor : AbstractEventExecutor
    {
        private static readonly Task s_terminationFuture = TaskUtil.FromException(new InvalidOperationException());

        public override bool IsShuttingDown => false;

        public override Task TerminationCompletion => s_terminationFuture;

        public override bool IsShutdown => false;

        public override bool IsTerminated => false;

        public override bool WaitTermination(TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public override void Execute(IRunnable task)
        {
            throw new NotImplementedException();
        }

        public override bool IsInEventLoop(XThread thread) => true;

        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout) => s_terminationFuture;

        protected override IEnumerable<IEventExecutor> GetItems() => new[] { this };
    }
}