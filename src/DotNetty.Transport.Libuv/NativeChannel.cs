// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    public abstract partial class NativeChannel<TChannel, TUnsafe> : AbstractChannel<TChannel, TUnsafe>, INativeChannel
    {
        //[Flags]
        protected static class StateFlags
        {
            public const int Open = 1;
            public const int ReadScheduled = 1 << 1;
            public const int WriteScheduled = 1 << 2;
            public const int Active = 1 << 3;
        }

        internal bool ReadPending;
        int _state;

        IPromise connectPromise;
        IScheduledTask connectCancellationTask;

        protected NativeChannel(IChannel parent) : base(parent)
        {
            this.State = StateFlags.Open;
        }

        public override bool Open => this.IsInState(StateFlags.Open);

        public override bool Active => this.IsInState(StateFlags.Active);

        protected override bool IsCompatible(IEventLoop eventLoop) => eventLoop is LoopExecutor;

        protected bool IsInState(int stateToCheck) => (this.State & stateToCheck) == stateToCheck;

        protected void SetState(int stateToSet) => this.State |= stateToSet;

        protected int ResetState(int stateToReset)
        {
            var oldState = this.State;
            if ((oldState & stateToReset) != 0)
            {
                this.State = oldState & ~stateToReset;
            }
            return oldState;
        }

        protected bool TryResetState(int stateToReset)
        {
            var oldState = this.State;
            if ((oldState & stateToReset) != 0)
            {
                this.State = oldState & ~stateToReset;
                return true;
            }
            return false;
        }

        void DoConnect(EndPoint remoteAddress, EndPoint localAddress)
        {
            ConnectRequest request = null;
            try
            {
                if (localAddress is object)
                {
                    this.DoBind(localAddress);
                }
                request = new TcpConnect(this.Unsafe, (IPEndPoint)remoteAddress);
            }
            catch
            {
                request?.Dispose();
                throw;
            }
        }

        void DoFinishConnect() => this.OnConnected();

        protected override void DoClose()
        {
            var promise = this.connectPromise;
            if (promise is object)
            {
                promise.TrySetException(ThrowHelper.GetClosedChannelException());
                this.connectPromise = null;
            }
        }

        protected virtual void OnConnected()
        {
            this.SetState(StateFlags.Active);
            this.CacheLocalAddress();
            this.CacheRemoteAddress();
        }

        protected abstract void DoStopRead();

        NativeHandle INativeChannel.GetHandle() => this.GetHandle();
        internal abstract NativeHandle GetHandle();
        bool INativeChannel.IsBound => this.IsBound;
        internal abstract bool IsBound { get; }

        public abstract partial class NativeChannelUnsafe : AbstractUnsafe, INativeUnsafe
        {
            protected NativeChannelUnsafe() : base() //(NativeChannel channel) : base(channel)
            {
            }

            public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
            {
                var ch = this.channel;
                if (!ch.Open)
                {
                    return this.CreateClosedChannelExceptionTask();
                }

                try
                {
                    if (ch.connectPromise is object)
                    {
                        ThrowHelper.ThrowInvalidOperationException_ConnAttempt();
                    }

                    ch.connectPromise = ch.NewPromise(remoteAddress);

                    // Schedule connect timeout.
                    TimeSpan connectTimeout = ch.Configuration.ConnectTimeout;
                    if (connectTimeout > TimeSpan.Zero)
                    {
                        ch.connectCancellationTask = ch.EventLoop
                            .Schedule(CancelConnectAction, ch, remoteAddress, connectTimeout);
                    }

                    ch.DoConnect(remoteAddress, localAddress);
                    return ch.connectPromise.Task;
                }
                catch (Exception ex)
                {
                    this.CloseIfClosed();
                    return TaskUtil.FromException(this.AnnotateConnectException(ex, remoteAddress));
                }
            }

            static void CancelConnect(object context, object state)
            {
                var ch = (TChannel)context;
                var address = (IPEndPoint)state;
                var promise = ch.connectPromise;
                var cause = new ConnectTimeoutException($"connection timed out: {address}");
                if (promise is object && promise.TrySetException(cause))
                {
                    ch.Unsafe.CloseSafe();
                }
            }

            // Connect request callback from libuv thread
            void INativeUnsafe.FinishConnect(ConnectRequest request)
            {
                var ch = this.channel;
                ch.connectCancellationTask?.Cancel();

                var promise = ch.connectPromise;
                bool success = false;
                try
                {
                    if (promise is object) // Not cancelled from timed out
                    {
                        OperationException error = request.Error;
                        if (error is object)
                        {
                            if (error.ErrorCode == ErrorCode.ETIMEDOUT)
                            {
                                // Connection timed out should use the standard ConnectTimeoutException
                                promise.TrySetException(ThrowHelper.GetConnectTimeoutException(error));
                            }
                            else
                            {
                                promise.TrySetException(ThrowHelper.GetChannelException(error));
                            }
                        }
                        else
                        {
                            bool wasActive = ch.Active;
                            ch.DoFinishConnect();
                            success = promise.TryComplete();

                            // Regardless if the connection attempt was cancelled, channelActive() 
                            // event should be triggered, because what happened is what happened.
                            if (!wasActive && ch.Active)
                            {
                                ch.Pipeline.FireChannelActive();
                            }
                        }
                    }
                }
                finally
                {
                    request.Dispose();
                    ch.connectPromise = null;
                    if (!success)
                    {
                        this.CloseSafe();
                    }
                }
            }

            public abstract IntPtr UnsafeHandle { get; }

            // Allocate callback from libuv thread
            uv_buf_t INativeUnsafe.PrepareRead(ReadOperation readOperation)
            {
                Debug.Assert(readOperation is object);

                var ch = this.channel;
                IChannelConfiguration config = ch.Configuration;
                IByteBufferAllocator allocator = config.Allocator;

                IRecvByteBufAllocatorHandle allocHandle = this.RecvBufAllocHandle;
                IByteBuffer buffer = allocHandle.Allocate(allocator);
                allocHandle.AttemptedBytesRead = buffer.WritableBytes;

                return readOperation.GetBuffer(buffer);
            }

            // Read callback from libuv thread
            void INativeUnsafe.FinishRead(ReadOperation operation)
            {
                var ch = this.channel;
                IChannelConfiguration config = ch.Configuration;
                IChannelPipeline pipeline = ch.Pipeline;
                OperationException error = operation.Error;

                bool close = error is object || operation.EndOfStream;
                IRecvByteBufAllocatorHandle allocHandle = this.RecvBufAllocHandle;
                allocHandle.Reset(config);

                IByteBuffer buffer = operation.Buffer;
                Debug.Assert(buffer is object);

                allocHandle.LastBytesRead = operation.Status;
                if (allocHandle.LastBytesRead <= 0)
                {
                    // nothing was read -> release the buffer.
                    buffer.Release();
                    buffer = null;
                    close = allocHandle.LastBytesRead < 0;
                    if (close)
                    {
                        // There is nothing left to read as we received an EOF.
                        ch.ReadPending = false;
                    }
                }
                else
                {
                    buffer.SetWriterIndex(buffer.WriterIndex + operation.Status);
                    allocHandle.IncMessagesRead(1);

                    ch.ReadPending = false;
                    pipeline.FireChannelRead(buffer);
                }

                allocHandle.ReadComplete();
                pipeline.FireChannelReadComplete();

                if (close)
                {
                    if (error is object)
                    {
                        pipeline.FireExceptionCaught(ThrowHelper.GetChannelException(error));
                    }
                    if (ch.Open) { this.Close(this.VoidPromise()); } // ## 苦竹 修改 this.CloseSafe(); ##
                }
                else
                {
                    // If read is called from channel read or read complete
                    // do not stop reading
                    if (!ch.ReadPending && !config.AutoRead)
                    {
                        ch.DoStopRead();
                    }
                }
            }

            internal void CloseSafe() => CloseSafe(this.channel, this.channel.CloseAsync());

            internal static async void CloseSafe(object channelObject, Task closeTask)
            {
                try
                {
                    await closeTask;
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    if (Logger.DebugEnabled)
                    {
                        Logger.FailedToCloseChannelCleanly(channelObject, ex);
                    }
                }
            }

            protected sealed override void Flush0()
            {
                var ch = this.channel;
                if (!ch.IsInState(StateFlags.WriteScheduled))
                {
                    base.Flush0();
                }
            }

            // Write request callback from libuv thread
            void INativeUnsafe.FinishWrite(int bytesWritten, OperationException error)
            {
                var ch = this.channel;
                bool resetWritePending = ch.TryResetState(StateFlags.WriteScheduled);
                Debug.Assert(resetWritePending);

                try
                {
                    ChannelOutboundBuffer input = this.OutboundBuffer;
                    if (error is object)
                    {
                        input.FailFlushed(error, true);
                        ch.Pipeline.FireExceptionCaught(error);
                        this.Close(this.VoidPromise(), ThrowHelper.GetChannelException_FailedToWrite(error), WriteClosedChannelException , false);
                    }
                    else
                    {
                        if (bytesWritten > 0)
                        {
                            input.RemoveBytes(bytesWritten);
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.Close(this.VoidPromise(), ThrowHelper.GetClosedChannelException_FailedToWrite(ex), WriteClosedChannelException, false);
                }
                this.Flush0();
            }
        }
    }
}
