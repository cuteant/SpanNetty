// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Embedded
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using Thread = DotNetty.Common.Concurrency.XThread;

    sealed class EmbeddedEventLoop : AbstractScheduledEventExecutor, IEventLoop
    {
        readonly Deque<IRunnable> _tasks = new Deque<IRunnable>(2);

        public new IEventLoop GetNext() => this;

        public Task RegisterAsync(IChannel channel) => channel.Unsafe.RegisterAsync(this);

        public override bool IsShuttingDown => false;

        public override Task TerminationCompletion => throw ThrowHelper.GetNotSupportedException();

        public override bool IsShutdown => false;

        public override bool IsTerminated => false;

        public new IEventLoopGroup Parent => (IEventLoopGroup)base.Parent;

        protected override IEnumerable<IEventExecutor> GetItems() => new[] { this };

        public new IEnumerable<IEventLoop> Items => new[] { this };

        public override bool IsInEventLoop(Thread thread) => true;

        public override void Execute(IRunnable command)
        {
            if (command is null)
            {
                ThrowHelper.ThrowNullReferenceException_Command();
            }
            _tasks.AddLast​(command);
        }

        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            throw ThrowHelper.GetNotSupportedException();
        }

        internal long NextScheduledTask() => NextScheduledTaskNanos();

        internal void RunTasks()
        {
            while (_tasks.TryRemoveFirst(out var task))
            {
                task.Run();
            }
        }

        internal long RunScheduledTasks()
        {
            var time = PreciseTime.NanoTime();
            while (true)
            {
                IRunnable task = PollScheduledTask(time);
                if (task is null)
                {
                    return NextScheduledTaskNanos();
                }
                task.Run();
            }
        }

        internal new void CancelScheduledTasks() => base.CancelScheduledTasks();

        public override bool WaitTermination(TimeSpan timeout)
        {
            return false;
        }
    }
}