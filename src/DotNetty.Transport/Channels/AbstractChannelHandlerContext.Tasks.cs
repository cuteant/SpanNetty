// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;

    partial class AbstractChannelHandlerContext
    {
        abstract class AbstractWriteTask : IRunnable
        {
            private static readonly bool EstimateTaskSizeOnSubmit =
                SystemPropertyUtil.GetBoolean("io.netty.transport.estimateSizeOnSubmit", true);

            // Assuming a 64-bit .NET VM, 16 bytes object header, 4 reference fields and 2 int field
            private static readonly int WriteTaskOverhead =
                SystemPropertyUtil.GetInt("io.netty.transport.writeTaskSizeOverhead", 56);

            private ThreadLocalPool.Handle _handle;
            private AbstractChannelHandlerContext _ctx;
            private object _msg;
            private IPromise _promise;
            private int _size;

            protected static void Init(AbstractWriteTask task, AbstractChannelHandlerContext ctx, object msg, IPromise promise)
            {
                task._ctx = ctx;
                task._msg = msg;
                task._promise = promise;

                if (EstimateTaskSizeOnSubmit)
                {
                    task._size = ctx._pipeline.EstimatorHandle.Size(msg) + WriteTaskOverhead;
                    ctx._pipeline.IncrementPendingOutboundBytes(task._size);
                }
                else
                {
                    task._size = 0;
                }
            }

            protected AbstractWriteTask(ThreadLocalPool.Handle handle)
            {
                _handle = handle;
            }

            public void Run()
            {
                try
                {
                    DecrementPendingOutboundBytes();
                    Write(_ctx, _msg, _promise);
                }
                finally
                {
                    Recycle();
                }
            }

            internal void Cancel()
            {
                try
                {
                    DecrementPendingOutboundBytes();
                }
                finally
                {
                    Recycle();
                }
            }

            void DecrementPendingOutboundBytes()
            {
                if (EstimateTaskSizeOnSubmit)
                {
                    _ctx._pipeline.DecrementPendingOutboundBytes(_size);
                }
            }

            void Recycle()
            {
                // Set to null so the GC can collect them directly
                _ctx = null;
                _msg = null;
                _promise = null;
                _handle.Release(this);
            }

            protected virtual void Write(AbstractChannelHandlerContext ctx, object msg, IPromise promise) => ctx.InvokeWrite(msg, promise);
        }
        sealed class WriteTask : AbstractWriteTask
        {

            private static readonly ThreadLocalPool<WriteTask> Recycler = new ThreadLocalPool<WriteTask>(handle => new WriteTask(handle));

            public static WriteTask NewInstance(AbstractChannelHandlerContext ctx, object msg, IPromise promise)
            {
                WriteTask task = Recycler.Take();
                Init(task, ctx, msg, promise);
                return task;
            }

            WriteTask(ThreadLocalPool.Handle handle)
                : base(handle)
            {
            }
        }

        sealed class WriteAndFlushTask : AbstractWriteTask
        {

            private static readonly ThreadLocalPool<WriteAndFlushTask> Recycler = new ThreadLocalPool<WriteAndFlushTask>(handle => new WriteAndFlushTask(handle));

            public static WriteAndFlushTask NewInstance(
                    AbstractChannelHandlerContext ctx, object msg, IPromise promise)
            {
                WriteAndFlushTask task = Recycler.Take();
                Init(task, ctx, msg, promise);
                return task;
            }

            WriteAndFlushTask(ThreadLocalPool.Handle handle)
                : base(handle)
            {
            }

            protected override void Write(AbstractChannelHandlerContext ctx, object msg, IPromise promise)
            {
                base.Write(ctx, msg, promise);
                ctx.InvokeFlush();
            }
        }
    }
}
