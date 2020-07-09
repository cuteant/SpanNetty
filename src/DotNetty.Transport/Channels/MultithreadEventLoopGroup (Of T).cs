namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    /// <summary>
    /// Abstract base class for <see cref="IEventLoopGroup"/> implementations that handles their tasks with multiple threads at
    /// the same time.
    /// </summary>
    public abstract class MultithreadEventLoopGroup<TLoopGroup, TEventLoop> : MultithreadEventExecutorGroup<TLoopGroup, TEventLoop>, IEventLoopGroup
        where TLoopGroup : MultithreadEventLoopGroup<TLoopGroup, TEventLoop>
        where TEventLoop : class, IEventLoop
    {
        private static readonly int DefaultEventLoopThreadCount = Environment.ProcessorCount * 2;

        /// <summary>Creates a new instance of <see cref="MultithreadEventLoopGroup{TGroup, TExecutor}"/>.</summary>
        protected MultithreadEventLoopGroup(int nThreads, Func<TLoopGroup, TEventLoop> eventLoopFactory)
            : this(0u >= (uint)nThreads ? DefaultEventLoopThreadCount : nThreads, DefaultEventExecutorChooserFactory<TEventLoop>.Instance, eventLoopFactory)
        {
        }

        /// <summary>Creates a new instance of <see cref="MultithreadEventLoopGroup{TGroup, TExecutor}"/>.</summary>
        protected MultithreadEventLoopGroup(int nThreads,
            IEventExecutorChooserFactory<TEventLoop> chooserFactory,
            Func<TLoopGroup, TEventLoop> eventLoopFactory)
            : base(0u >= (uint)nThreads ? DefaultEventLoopThreadCount : nThreads, chooserFactory, eventLoopFactory)
        {
        }

        IEnumerable<IEventLoop> IEventLoopGroup.Items => GetItems();

        IEventLoop IEventLoopGroup.GetNext() => GetNext();

        public virtual Task RegisterAsync(IChannel channel) => GetNext().RegisterAsync(channel);
    }
}