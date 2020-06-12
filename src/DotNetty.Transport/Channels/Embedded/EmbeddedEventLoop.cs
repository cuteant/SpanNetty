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
    using Thread = DotNetty.Common.Concurrency.XThread;

    sealed class EmbeddedEventLoop : AbstractScheduledEventExecutor, IEventLoop
    {
        readonly QueueX<IRunnable> _tasks = new QueueX<IRunnable>(2);

        public new IEventLoop GetNext() => this;

        public Task RegisterAsync(IChannel channel) => channel.Unsafe.RegisterAsync(this);

        public override bool IsShuttingDown => false;

        public override Task TerminationCompletion => throw new NotSupportedException();

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
            _tasks.Enqueue(command);
        }

        public override Task ShutdownGracefullyAsync(TimeSpan quietPeriod, TimeSpan timeout)
        {
            throw new NotSupportedException();
        }

        internal PreciseTimeSpan NextScheduledTask() => NextScheduledTaskNanos();

        internal void RunTasks()
        {
            while (_tasks.TryDequeue(out var task))
            {
                task.Run();
            }
        }

        internal PreciseTimeSpan RunScheduledTasks()
        {
            PreciseTimeSpan time = GetNanos();
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
    }
}