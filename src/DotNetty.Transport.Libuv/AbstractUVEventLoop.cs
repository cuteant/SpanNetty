/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) The DotNetty Project (Microsoft). All rights reserved.
 *
 *   https://github.com/azure/dotnetty
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Handles;
    using Timer = DotNetty.Transport.Libuv.Handles.Timer;

    public abstract class AbstractUVEventLoop : SingleThreadEventLoopBase
    {
        #region @@ Fields @@

        private const int DefaultBreakoutTime = 100; //ms

        private static long s_initialTime;
        private static long s_startTimeInitialized;

        internal static readonly TimeSpan DefaultBreakoutInterval = TimeSpan.FromMilliseconds(DefaultBreakoutTime);

        private readonly long _preciseBreakoutInterval;
        private readonly ManualResetEventSlim _loopRunStart;
        private readonly Loop _loop;
        private readonly Async _asyncHandle;
        private readonly Timer _timerHandle;

        // Flag to indicate whether async handle should be used to wake up 
        // the loop, only accessed when InEventLoop is true
        private bool _wakeUp = true;

        #endregion

        #region @@ Constructor @@

        protected AbstractUVEventLoop(IEventLoopGroup parent, IThreadFactory threadFactory, IRejectedExecutionHandler rejectedHandler, TimeSpan breakoutInterval)
            : base(parent, threadFactory, false, int.MaxValue, rejectedHandler)
        {
            _preciseBreakoutInterval = ToPreciseTime(breakoutInterval);

            _loop = new Loop();
            _asyncHandle = _loop.CreateAsync(OnCallbackAsyncAction, this);
            _timerHandle = _loop.CreateTimer(OnCallbackTimerAction, this);
            _loopRunStart = new ManualResetEventSlim(false, 1);

            if (SharedConstants.False >= (uint)Interlocked.Exchange(ref s_startTimeInitialized, SharedConstants.True))
            {
                _loop.UpdateTime();
                s_initialTime = _loop.Now;
            }
        }

        #endregion

        #region -- Properties --

        internal Loop UnsafeLoop => _loop;

        internal int LoopThreadId => InnerThread.Id;

        #endregion

        #region -- Thread --

        private static readonly XParameterizedThreadStart RunAction = s => Run(s);
        private static readonly Action<Async, object> OnCallbackAsyncAction = (h, s) => OnCallback(s);
        private static readonly Action<Timer, object> OnCallbackTimerAction = (h, s) => OnCallback(s);

        protected sealed override XThread NewThread(IThreadFactory threadFactory)
        {
            return threadFactory.NewThread(RunAction);
        }

        protected override void Start()
        {
            var currState = ExecutionState;
            if (currState > NotStartedState)
            {
                ThrowHelper.ThrowInvalidOperationException_ExecutionState(currState);
            }
            InnerThread.Start(this);
        }

        private static void Run(object state)
        {
            var loopExecutor = (AbstractUVEventLoop)state;
            loopExecutor.SetCurrentExecutor(loopExecutor);

            _ = Task.Factory.StartNew(
                executor => ((AbstractUVEventLoop)executor).StartLoop(), state,
                CancellationToken.None,
                TaskCreationOptions.AttachedToParent,
                loopExecutor.Scheduler);
        }

        private void StartLoop()
        {
            try
            {
                UpdateLastExecutionTime();
                Initialize();
                if (!CompareAndSetExecutionState(NotStartedState, StartedState))
                {
                    ThrowHelper.ThrowInvalidOperationException_ExecutionState0(NotStartedState);
                }
                _loopRunStart.Set();
                _ = _loop.RunDefault();
            }
            catch (Exception ex)
            {
                _loopRunStart.Set();
                SetExecutionState(TerminatedState);
                Logger.LoopRunDefaultError(InnerThread, ex);
            }
            finally
            {
                if (Logger.InfoEnabled) Logger.LoopThreadFinished(InnerThread);
                try
                {
                    CleanupAndTerminate(false);
                }
                catch { }
            }
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        private static void OnCallback(object state) => ((AbstractUVEventLoop)state).Run();

        #endregion

        #region -- Utilities --

        protected sealed override long GetTimeFromStart()
        {
            _loop.UpdateTime();
            return _loop.Now - s_initialTime;
        }

        protected sealed override long ToPreciseTime(TimeSpan time)
        {
            return (long)time.TotalMilliseconds;
        }

        protected override void TaskDelay(int millisecondsTimeout)
        {
            _ = _timerHandle.Start(millisecondsTimeout, 0);
        }

        #endregion

        /// <summary>
        /// Called before run the loop in the loop thread.
        /// </summary>
        protected virtual void Initialize()
        {
            // NOOP
        }

        /// <summary>
        /// Called before stop the loop in the loop thread.
        /// </summary>
        protected virtual void Release()
        {
            // NOOP
        }

        protected sealed override IQueue<IRunnable> NewTaskQueue(int maxPendingTasks)
        {
            // This event loop never calls takeTask()
            return new CompatibleConcurrentQueue<IRunnable>();
        }

        public override void Execute(IRunnable task)
        {
            InternalExecute(task);
        }

        public override void LazyExecute(IRunnable task)
        {
            InternalExecute(task);
        }

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        private void InternalExecute(IRunnable task)
        {
            AddTask(task);

            WakeUp(InEventLoop);
        }

        protected override void WakeUp(bool inEventLoop)
        {
            // If the executor is not in the event loop, wake up the loop by async handle immediately.
            //
            // If the executor is in the event loop and in the middle of RunAllTasks, no need to 
            // wake up the loop again because this is normally called by the current running task.
            if (!inEventLoop || _wakeUp)
            {
                _asyncHandle.Send();
            }
        }

        protected override void OnBeginRunningAllTasks()
        {
            _wakeUp = false;
        }

        protected override void OnEndRunningAllTasks()
        {
            _wakeUp = true;
        }

        protected override void AfterRunningAllTasks()
        {
            const long MaxDelayMilliseconds = int.MaxValue - 1;

#if DEBUG
            base.AfterRunningAllTasks();
#endif

            if (IsShuttingDown)
            {
                // Immediate shutdown
                WakeUp(true);
                return;
            }

            long nextTimeout = DefaultBreakoutTime;
            if (HasTasks)
            {
                _ = _timerHandle.Start(nextTimeout, 0);
            }
            else
            {
                if (ScheduledTaskQueue.TryPeek(out IScheduledRunnable nextScheduledTask))
                {
                    long delayNanos = nextScheduledTask.DelayNanos;
                    if ((ulong)delayNanos > 0UL) // delayNanos >= 0
                    {
                        var timeout = PreciseTime.ToMilliseconds(delayNanos);
                        nextTimeout = Math.Min(timeout, MaxDelayMilliseconds);
                    }
                    _ = _timerHandle.Start(nextTimeout, 0);
                }
            }
        }

        protected override void Run()
        {
            if (!IsShuttingDown)
            {
                RunAllTasks(_preciseBreakoutInterval);
            }
            else
            {
                DoShutdown();
            }
        }

        internal void WaitForLoopRun(TimeSpan timeout) => _loopRunStart.Wait(timeout);

        protected override void OnBeginShutdownGracefully()
        {
            // In case of Shutdown called before the loop run
            _loopRunStart.Wait();
        }

        private void DoShutdown()
        {
            if (ConfirmShutdown())
            {
                StopLoop();
                return;
            }

            SetExecutionState(ShuttingDownState);

            // Run all remaining tasks and shutdown hooks. At this point the event loop
            // is in ST_SHUTTING_DOWN state still accepting tasks which is needed for
            // graceful shutdown with quietPeriod.
            while (true)
            {
                if (ConfirmShutdown())
                {
                    break;
                }
            }

            // Now we want to make sure no more tasks can be added from this point. This is
            // achieved by switching the state. Any new tasks beyond this point will be rejected.
            SetExecutionState(ShutdownState);

            // We have the final set of tasks in the queue now, no more can be added, run all remaining.
            // No need to loop here, this is the final pass.
            if (ConfirmShutdown())
            {
                StopLoop();
            }
        }

        protected override void Cleanup()
        {
            try
            {
                Release();
            }
            catch (Exception ex)
            {
                if (Logger.WarnEnabled) Logger.LoopReleaseError(InnerThread, ex);
            }

            SafeDispose(_timerHandle);
            SafeDispose(_asyncHandle);
            SafeDispose(_loop);
            if (Logger.InfoEnabled) Logger.LoopDisposed(InnerThread);
        }

        private void StopLoop()
        {
            try
            {
                // Drop out from the loop so that it can be safely disposed,
                // other active handles will be closed by loop.Close()
                _timerHandle.Stop();
                _loop.Stop();
            }
            catch (Exception ex)
            {
                Logger.ShuttingDownLoopError(ex);
            }
        }

        private static void SafeDispose(IDisposable handle)
        {
            try
            {
#if DEBUG
                if (Logger.DebugEnabled) Logger.LoopDisposing(handle);
#endif
                handle.Dispose();
            }
            catch (Exception ex)
            {
                if (Logger.WarnEnabled) Logger.LoopDisposeError(handle, ex);
            }
        }
    }
}
