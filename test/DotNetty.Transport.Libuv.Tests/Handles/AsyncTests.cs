// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests.Handles
{
    using System;
    using System.Threading;
    using DotNetty.Transport.Libuv.Handles;
    using Xunit;

    public sealed class AsyncTests : IDisposable
    {
        Loop loop;
        Prepare prepare;
        Async async;

        Thread thread;
        int prepareCalled;
        long asyncCalled;
        int closeCount;
        ManualResetEventSlim resetEvent;

        void PrepareCallback(Prepare handle)
        {
            if (this.prepareCalled == 0)
            {
                this.thread = new Thread(this.ThreadStart);
                this.thread.Start();
            }

            this.prepareCalled++;
        }

        void ThreadStart()
        {
            while (Interlocked.Read(ref this.asyncCalled) < 3)
            {
                this.async.Send();
            }

            this.resetEvent.Wait();
        }

        void OnAsync(Async handle)
        {
            if (Interlocked.Increment(ref this.asyncCalled) < 3)
            {
                return;
            }

            this.prepare.CloseHandle(this.OnClose);
            this.async.CloseHandle(this.OnClose);
        }

        void OnClose(IScheduleHandle handle)
        {
            handle.Dispose();
            this.closeCount++;

            if (!this.resetEvent.IsSet)
            {
                this.resetEvent.Set();
            }
        }

        [Fact]
        public void Run()
        {
            this.resetEvent = new ManualResetEventSlim(false);

            this.loop = new Loop();
            this.prepareCalled = 0;
            this.asyncCalled = 0;

            this.prepare = this.loop
                .CreatePrepare()
                .Start(this.PrepareCallback);
            this.async = this.loop.CreateAsync(this.OnAsync);

            this.loop.RunDefault();
            this.thread?.Join();

            Assert.Equal(2, this.closeCount);
            Assert.True(this.prepareCalled > 0);
            Assert.Equal(3, this.asyncCalled);
        }

        public void Dispose()
        {
            this.loop?.Dispose();
            this.loop = null;
        }
    }
}
