namespace DotNetty.Transport.Tests.Channel.Pool
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using DotNetty.Transport.Channels.Pool;
    using Xunit;

    [Collection("EventLoopTests")]
    public class FixedChannelPoolMapDeadlockTest
    {
        [Fact]
        public async Task TestDeadlockOnAcquire()
        {
            IEventLoop threadA1 = new DefaultEventLoop();
            Bootstrap bootstrapA1 = new Bootstrap()
                   .Channel<LocalChannel>().Group(threadA1).LocalAddress(new LocalAddress("A1"));
            IEventLoop threadA2 = new DefaultEventLoop();
            Bootstrap bootstrapA2 = new Bootstrap()
                   .Channel<LocalChannel>().Group(threadA2).LocalAddress(new LocalAddress("A2"));
            IEventLoop threadB1 = new DefaultEventLoop();
            Bootstrap bootstrapB1 = new Bootstrap()
                   .Channel<LocalChannel>().Group(threadB1).LocalAddress(new LocalAddress("B1"));
            IEventLoop threadB2 = new DefaultEventLoop();
            Bootstrap bootstrapB2 = new Bootstrap()
                   .Channel<LocalChannel>().Group(threadB2).LocalAddress(new LocalAddress("B2"));

            FixedChannelPool poolA1 = new FixedChannelPool(bootstrapA1, NoopHandler.Instance, 1);
            FixedChannelPool poolA2 = new FixedChannelPool(bootstrapB2, NoopHandler.Instance, 1);
            FixedChannelPool poolB1 = new FixedChannelPool(bootstrapB1, NoopHandler.Instance, 1);
            FixedChannelPool poolB2 = new FixedChannelPool(bootstrapA2, NoopHandler.Instance, 1);

            // Synchronize threads on these barriers to ensure order of execution, first wait until each thread is inside
            // the newPool callbak, then hold the two threads that should lose the match until the first two returns, then
            // release them to test if they deadlock when trying to release their pools on each other's threads.
            Barrier arrivalBarrier = new Barrier(4);
            Barrier releaseBarrier = new Barrier(3);

            var channelPoolMap = new TestChannelPoolMap0(
                threadA1, threadA2, threadB1, threadB2,
                poolA1, poolA2, poolB1, poolB2, arrivalBarrier, releaseBarrier);

            // Thread A1 calls ChannelPoolMap.get(A)
            // Thread A2 calls ChannelPoolMap.get(A)
            // Thread B1 calls ChannelPoolMap.get(B)
            // Thread B2 calls ChannelPoolMap.get(B)

            var futureA1 = threadA1.SubmitAsync(() =>
            {
                return channelPoolMap.Get("A");
            });

            var futureA2 = threadA2.SubmitAsync(() =>
            {
                return channelPoolMap.Get("A");
            });

            var futureB1 = threadB1.SubmitAsync(() =>
            {
                return channelPoolMap.Get("B");
            });

            var futureB2 = threadB2.SubmitAsync(() =>
            {
                return channelPoolMap.Get("B");
            });

            // Thread A1 succeeds on updating the map and moves on
            // Thread B1 succeeds on updating the map and moves on
            // These should always succeed and return with new pools
            try
            {
                var result = await TaskUtil.WaitAsync(futureA1, TimeSpan.FromSeconds(1));
                if (!result || futureA1.IsFailure()) { throw new TimeoutException(); }
                Assert.Same(poolA1, futureA1.Result);

                result = await TaskUtil.WaitAsync(futureB1, TimeSpan.FromSeconds(1));
                if (!result || futureB1.IsFailure()) { throw new TimeoutException(); }
                Assert.Same(poolB1, futureB1.Result);
            }
            catch (Exception)
            {
                Shutdown(threadA1, threadA2, threadB1, threadB2);
                throw;
            }

            // Now release the other two threads which at this point lost the race and will try to clean up the acquired
            // pools. The expected scenario is that both pools close, in case of a deadlock they will hang.
            if (!releaseBarrier.SignalAndWait(TimeSpan.FromSeconds(1))) { Assert.False(true); }

            // Thread A2 fails to update the map and submits close to thread B2
            // Thread B2 fails to update the map and submits close to thread A2
            // If the close is blocking, then these calls will time out as the threads are waiting for each other
            // If the close is not blocking, then the previously created pools will be returned
            try
            {
                var result = await TaskUtil.WaitAsync(futureA2, TimeSpan.FromSeconds(1));
                if (!result || futureA2.IsFailure()) { throw new TimeoutException(); }
                Assert.Same(poolA1, futureA2.Result);

                result = await TaskUtil.WaitAsync(futureB2, TimeSpan.FromSeconds(1));
                if (!result || futureB2.IsFailure()) { throw new TimeoutException(); }
                Assert.Same(poolB1, futureB2.Result);
            }
            catch (TimeoutException)
            {
                Assert.False(true); // Fail the test on timeout to distinguish from other errors
                throw;
            }
            finally
            {
                poolA1.Close();
                poolA2.Close();
                poolB1.Close();
                poolB2.Close();
                channelPoolMap.Close();
                Shutdown(threadA1, threadA2, threadB1, threadB2);
            }
        }

        sealed class TestChannelPoolMap0 : AbstractChannelPoolMap<string, FixedChannelPool>
        {
            private readonly IEventLoop _threadA1;
            private readonly IEventLoop _threadA2;
            private readonly IEventLoop _threadB1;
            private readonly IEventLoop _threadB2;
            private readonly FixedChannelPool _poolA1;
            private readonly FixedChannelPool _poolA2;
            private readonly FixedChannelPool _poolB1;
            private readonly FixedChannelPool _poolB2;
            private readonly Barrier _arrivalBarrier;
            private readonly Barrier _releaseBarrier;

            public TestChannelPoolMap0(
                IEventLoop threadA1,
                IEventLoop threadA2,
                IEventLoop threadB1,
                IEventLoop threadB2,
                FixedChannelPool poolA1,
                FixedChannelPool poolA2,
                FixedChannelPool poolB1,
                FixedChannelPool poolB2,
                Barrier arrivalBarrier,
                Barrier releaseBarrier)
            {
                _threadA1 = threadA1;
                _threadA2 = threadA2;
                _threadB1 = threadB1;
                _threadB2 = threadB2;
                _poolA1 = poolA1;
                _poolA2 = poolA2;
                _poolB1 = poolB1;
                _poolB2 = poolB2;
                _arrivalBarrier = arrivalBarrier;
                _releaseBarrier = releaseBarrier;
            }

            protected override FixedChannelPool NewPool(string key)
            {
                // Thread A1 gets a new pool on eventexecutor thread A1 (anywhere but A2 or B2)
                // Thread B1 gets a new pool on eventexecutor thread B1 (anywhere but A2 or B2)
                // Thread A2 gets a new pool on eventexecutor thread B2
                // Thread B2 gets a new pool on eventexecutor thread A2

                if ("A".Equals(key))
                {
                    if (_threadA1.InEventLoop)
                    {
                        // Thread A1 gets pool A with thread A1
                        if (!_arrivalBarrier.SignalAndWait(TimeSpan.FromSeconds(1))) { throw new TimeoutException(); }
                        return _poolA1;
                    }
                    else if (_threadA2.InEventLoop)
                    {
                        // Thread A2 gets pool A with thread B2, but only after A1 won
                        if (!_arrivalBarrier.SignalAndWait(TimeSpan.FromSeconds(1))) { throw new TimeoutException(); }
                        if (!_releaseBarrier.SignalAndWait(TimeSpan.FromSeconds(1))) { throw new TimeoutException(); }
                        return _poolA2;
                    }
                }
                else if ("B".Equals(key))
                {
                    if (_threadB1.InEventLoop)
                    {
                        // Thread B1 gets pool with thread B1
                        if (!_arrivalBarrier.SignalAndWait(TimeSpan.FromSeconds(1))) { throw new TimeoutException(); }
                        return _poolB1;
                    }
                    else if (_threadB2.InEventLoop)
                    {
                        // Thread B2 gets pool with thread A2
                        if (!_arrivalBarrier.SignalAndWait(TimeSpan.FromSeconds(1))) { throw new TimeoutException(); }
                        if (!_releaseBarrier.SignalAndWait(TimeSpan.FromSeconds(1))) { throw new TimeoutException(); }
                        return _poolB2;
                    }
                }
                throw new Exception("Unexpected key=" + key + " or thread="
                                         + XThread.CurrentThread.Id);
            }
        }

        [Fact]
        public async Task TestDeadlockOnRemove()
        {
            IEventLoop thread1 = new DefaultEventLoop();
            Bootstrap bootstrap1 = new Bootstrap()
                   .Channel<LocalChannel>().Group(thread1).LocalAddress(new LocalAddress("#1"));
            IEventLoop thread2 = new DefaultEventLoop();
            Bootstrap bootstrap2 = new Bootstrap()
                   .Channel<LocalChannel>().Group(thread2).LocalAddress(new LocalAddress("#2"));

            // pool1 runs on thread2, pool2 runs on thread1
            FixedChannelPool pool1 = new FixedChannelPool(bootstrap2, NoopHandler.Instance, 1);
            FixedChannelPool pool2 = new FixedChannelPool(bootstrap1, NoopHandler.Instance, 1);
            var channelPoolMap = new TestChannelPoolMap1(pool1, pool2);

            Assert.Same(pool1, channelPoolMap.Get("#1"));
            Assert.Same(pool2, channelPoolMap.Get("#2"));

            // thread1 tries to remove pool1 which is running on thread2
            // thread2 tries to remove pool2 which is running on thread1

            var barrier = new Barrier(2);

            var future1 = thread1.SubmitAsync(() =>
            {
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(1))) { throw new TimeoutException(); }
                channelPoolMap.Remove("#1");
                return 1;
            });

            var future2 = thread2.SubmitAsync(() =>
            {
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(1))) { throw new TimeoutException(); }
                channelPoolMap.Remove("#2");
                return 2;
            });

            // A blocking close on remove will cause a deadlock here and the test will time out
            try
            {
                var result = await TaskUtil.WaitAsync(future1, TimeSpan.FromSeconds(1));
                if (!result || future1.IsFailure()) { throw new TimeoutException(); }
                result = await TaskUtil.WaitAsync(future2, TimeSpan.FromSeconds(1));
                if (!result || future2.IsFailure()) { throw new TimeoutException(); }
            }
            catch (TimeoutException)
            {
                Assert.False(true); // Fail the test on timeout to distinguish from other errors
            }
            finally
            {
                pool1.Close();
                pool2.Close();
                channelPoolMap.Close();
                Shutdown(thread1, thread2);
            }
        }

        sealed class TestChannelPoolMap1 : AbstractChannelPoolMap<string, FixedChannelPool>
        {
            private readonly FixedChannelPool _pool1;
            private readonly FixedChannelPool _pool2;

            public TestChannelPoolMap1(FixedChannelPool pool1, FixedChannelPool pool2)
            {
                _pool1 = pool1;
                _pool2 = pool2;
            }

            protected override FixedChannelPool NewPool(string key)
            {
                if ("#1".Equals(key))
                {
                    return _pool1;
                }
                else if ("#2".Equals(key))
                {
                    return _pool2;
                }
                else
                {
                    Assert.False(true);
                    throw new Exception("Unexpected key=" + key);
                }
            }
        }

        private static void Shutdown(params IEventLoop[] eventLoops)
        {
            var tasks = new List<Task>();
            foreach (var eventLoop in eventLoops)
            {
                tasks.Add(eventLoop.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100)));
            }
            Task.WaitAll(tasks.ToArray());
        }

        sealed class NoopHandler : AbstractChannelPoolHandler
        {
            public static readonly NoopHandler Instance = new NoopHandler();

            public override void ChannelCreated(IChannel channel)
            {
                // NOOP
            }
        }
    }
}
