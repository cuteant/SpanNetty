// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Microbench.Concurrency
{
    using System;
    using System.Threading;
    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Jobs;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Transport.Channels;

    [SimpleJob(RuntimeMoniker.Net48)]
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    [RPlotExporter]
    [BenchmarkCategory("Concurrency")]
    public class SingleThreadEventExecutorBenchmark
    {
        const int Iterations = 10 * 1000 * 1000;
        ITestExecutor _singleThreadEventLoop;
        ITestExecutor _concurrentQueueExecutor;
        ITestExecutor _fixedMpscQueueExecutor;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _singleThreadEventLoop = new NewTestExecutor("SingleThreadEventLoop", TimeSpan.FromSeconds(1));
            _concurrentQueueExecutor = new TestExecutor("CompatibleConcurrentQueue", TimeSpan.FromSeconds(1), new CompatibleConcurrentQueue<IRunnable>());
            _fixedMpscQueueExecutor = new TestExecutor("FixedMpscQueue", TimeSpan.FromSeconds(1), PlatformDependent.NewFixedMpscQueue<IRunnable>(1 * 1000 * 1000));
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _singleThreadEventLoop?.ShutdownGracefullyAsync();
            _concurrentQueueExecutor?.ShutdownGracefullyAsync();
            _fixedMpscQueueExecutor?.ShutdownGracefullyAsync();
        }

        [Benchmark(Baseline = true)]
        public void LoopConcurrentQueue() => Run(_singleThreadEventLoop);

        [Benchmark]
        public void ConcurrentQueue() => Run(_concurrentQueueExecutor);

        [Benchmark]
        public void FixedMpscQueue() => Run(_fixedMpscQueueExecutor);

        static void Run(ITestExecutor executor)
        {
            var mre = new ManualResetEvent(false);
            var actionIn = new BenchActionIn(executor, mre);
            executor.Execute(actionIn);

            if (!mre.WaitOne(TimeSpan.FromMinutes(1)))
            {
                throw new TimeoutException($"{executor.Name} benchmark timed out.");
            }
            mre.Reset();

            var actionOut = new BenchActionOut(mre);
            for (int i = 0; i < Iterations; i++)
            {
                executor.Execute(actionOut);
            }

            if (!mre.WaitOne(TimeSpan.FromMinutes(1)))
            {
                throw new TimeoutException($"{executor.Name} benchmark timed out.");
            }
        }

        sealed class BenchActionIn : IRunnable
        {
            int value;
            readonly IEventExecutor _executor;
            readonly ManualResetEvent _evt;

            public BenchActionIn(IEventExecutor executor, ManualResetEvent evt)
            {
                _executor = executor;
                _evt = evt;
            }

            public void Run()
            {
                if (++value < Iterations)
                {
                    _executor.Execute(this);
                }
                else
                {
                    _evt.Set();
                }
            }
        }

        sealed class BenchActionOut : IRunnable
        {
            int _value;
            readonly ManualResetEvent _evt;

            public BenchActionOut(ManualResetEvent evt)
            {
                _evt = evt;
            }

            public void Run()
            {
                if (++_value >= Iterations)
                {
                    _evt.Set();
                }
            }
        }

        interface ITestExecutor : IEventExecutor
        {
            string Name { get; }
        }

        sealed class NewTestExecutor : SingleThreadEventLoop, ITestExecutor
        {
            public NewTestExecutor(string threadName, TimeSpan breakoutInterval)
                : base(null, breakoutInterval)
            {
                Name = threadName;
            }

            public string Name { get; }
        }

        sealed class TestExecutor : SingleThreadEventExecutorOld, ITestExecutor
        {
            public string Name { get; }

            public TestExecutor(string threadName, TimeSpan breakoutInterval, IQueue<IRunnable> queue)
                : base(threadName, breakoutInterval, queue)
            {
                Name = threadName;
            }
        }
    }
}
