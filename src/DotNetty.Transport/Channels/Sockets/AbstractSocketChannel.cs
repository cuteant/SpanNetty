// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
    using System.Runtime.InteropServices;
#endif

    public abstract partial class AbstractSocketChannel<TChannel, TUnsafe> : AbstractChannel<TChannel, TUnsafe>
    {
        //[Flags]
        protected static class StateFlags
        {
            public const int Open = 1;
            public const int ReadScheduled = 1 << 1;
            public const int WriteScheduled = 1 << 2;
            public const int Active = 1 << 3;
            // todo: add input shutdown and read pending here as well?
        }

        internal static readonly EventHandler<SocketAsyncEventArgs> IoCompletedCallback = OnIoCompleted;
        static readonly Action<object, object> ConnectCallbackAction = OnConnectCompletedSync; // (u, e) => ((ISocketChannelUnsafe)u).FinishConnect((SocketChannelAsyncOperation<TChannel, TUnsafe>)e);
        static readonly Action<object, object> ReadCallbackAction = OnReadCompletedSync; // (u, e) => ((ISocketChannelUnsafe)u).FinishRead((SocketChannelAsyncOperation<TChannel, TUnsafe>)e);
        static readonly Action<object, object> WriteCallbackAction = OnWriteCompletedSync; // (u, e) => ((ISocketChannelUnsafe)u).FinishWrite((SocketChannelAsyncOperation<TChannel, TUnsafe>)e);

        protected readonly Socket Socket;
        SocketChannelAsyncOperation<TChannel, TUnsafe> readOperation;
        SocketChannelAsyncOperation<TChannel, TUnsafe> writeOperation;
        int inputShutdown;
        internal bool ReadPending;
        int _state;

        IPromise connectPromise;
        IScheduledTask connectCancellationTask;

        protected AbstractSocketChannel(IChannel parent, Socket socket)
            : base(parent)
        {
            this.Socket = socket;
            this.State = StateFlags.Open;

            try
            {
                this.Socket.Blocking = false;
            }
            catch (SocketException ex)
            {
                try
                {
                    socket.Dispose();
                }
                catch (SocketException ex2)
                {
                    if (Logger.WarnEnabled)
                    {
                        Logger.FailedToCloseAPartiallyInitializedSocket(ex2);
                    }
                }

                ThrowHelper.ThrowChannelException_FailedToEnterNonBlockingMode(ex);
            }
        }

        public override bool Open
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => this.IsInState(StateFlags.Open);
        }

        public override bool Active
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => this.IsInState(StateFlags.Active);
        }

        /// <summary>
        ///     Set read pending to <c>false</c>.
        /// </summary>
        protected internal void ClearReadPending()
        {
            if (this.Registered)
            {
                IEventLoop eventLoop = this.EventLoop;
                if (eventLoop.InEventLoop)
                {
                    this.ClearReadPending0();
                }
                else
                {
                    eventLoop.Execute(ClearReadPendingAction, this);
                }
            }
            else
            {
                // Best effort if we are not registered yet clear ReadPending. This happens during channel initialization.
                // NB: We only set the boolean field instead of calling ClearReadPending0(), because the SelectionKey is
                // not set yet so it would produce an assertion failure.
                this.ReadPending = false;
            }
        }

        void ClearReadPending0() => this.ReadPending = false;

        protected bool InputShutdown => SharedConstants.False < (uint)Volatile.Read(ref this.inputShutdown);

        protected void ShutdownInput() => Interlocked.Exchange(ref this.inputShutdown, SharedConstants.True);

        protected void SetState(int stateToSet) => this.State |= stateToSet;

        /// <returns>state before modification</returns>
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

        protected bool IsInState(int stateToCheck) => (this.State & stateToCheck) == stateToCheck;

        protected SocketChannelAsyncOperation<TChannel, TUnsafe> ReadOperation => this.readOperation ??= new SocketChannelAsyncOperation<TChannel, TUnsafe>((TChannel)this, true);

        SocketChannelAsyncOperation<TChannel, TUnsafe> WriteOperation => this.writeOperation ??= new SocketChannelAsyncOperation<TChannel, TUnsafe>((TChannel)this, false);

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
        protected SocketChannelAsyncOperation<TChannel, TUnsafe> PrepareWriteOperation(in ReadOnlyMemory<byte> buffer)
        {
            var operation = this.WriteOperation;
            operation.SetBuffer(MemoryMarshal.AsMemory(buffer));
            return operation;
        }
#else
        protected SocketChannelAsyncOperation<TChannel, TUnsafe> PrepareWriteOperation(in ArraySegment<byte> buffer)
        {
            var operation = this.WriteOperation;
            operation.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
            return operation;
        }
#endif

        protected SocketChannelAsyncOperation<TChannel, TUnsafe> PrepareWriteOperation(IList<ArraySegment<byte>> buffers)
        {
            var operation = this.WriteOperation;
            operation.BufferList = buffers;
            return operation;
        }

        protected void ResetWriteOperation()
        {
            var operation = this.writeOperation;

            Debug.Assert(operation is object);

            if (operation.BufferList is null)
            {
                operation.SetBuffer(null, 0, 0);
            }
            else
            {
                operation.BufferList = null;
            }
        }

        /// <remarks>PORT NOTE: matches behavior of NioEventLoop.processSelectedKey</remarks>
        static void OnIoCompleted(object sender, SocketAsyncEventArgs args)
        {
            var operation = (SocketChannelAsyncOperation<TChannel, TUnsafe>)args;
            var channel = operation.Channel;
            var @unsafe = channel.Unsafe;
            IEventLoop eventLoop = channel.EventLoop;
            switch (args.LastOperation)
            {
                case SocketAsyncOperation.Accept:
                    if (eventLoop.InEventLoop)
                    {
                        @unsafe.FinishRead(operation);
                    }
                    else
                    {
                        eventLoop.Execute(ReadCallbackAction, @unsafe, operation);
                    }
                    break;
                case SocketAsyncOperation.Connect:
                    if (eventLoop.InEventLoop)
                    {
                        @unsafe.FinishConnect(operation);
                    }
                    else
                    {
                        eventLoop.Execute(ConnectCallbackAction, @unsafe, operation);
                    }
                    break;
                case SocketAsyncOperation.Receive:
                case SocketAsyncOperation.ReceiveFrom:
                    if (eventLoop.InEventLoop)
                    {
                        @unsafe.FinishRead(operation);
                    }
                    else
                    {
                        eventLoop.Execute(ReadCallbackAction, @unsafe, operation);
                    }
                    break;
                case SocketAsyncOperation.Send:
                case SocketAsyncOperation.SendTo:
                    if (eventLoop.InEventLoop)
                    {
                        @unsafe.FinishWrite(operation);
                    }
                    else
                    {
                        eventLoop.Execute(WriteCallbackAction, @unsafe, operation);
                    }
                    break;
                default:
                    // todo: think of a better way to comm exception
                    ThrowHelper.ThrowArgumentException_TheLastOpCompleted(); break;
            }
        }

        internal interface ISocketChannelUnsafe : IChannelUnsafe
        {
            /// <summary>
            ///     Finish connect
            /// </summary>
            void FinishConnect(SocketChannelAsyncOperation<TChannel, TUnsafe> operation);

            /// <summary>
            ///     Read from underlying {@link SelectableChannel}
            /// </summary>
            void FinishRead(SocketChannelAsyncOperation<TChannel, TUnsafe> operation);

            void FinishWrite(SocketChannelAsyncOperation<TChannel, TUnsafe> operation);
        }

        public abstract class AbstractSocketUnsafe : AbstractUnsafe, ISocketChannelUnsafe
        {
            protected AbstractSocketUnsafe() //(AbstractSocketChannel channel)
                : base() //(channel)
            {
            }

            //public AbstractSocketChannel Channel => (AbstractSocketChannel)this.channel;
            //public TChannel Channel => this.channel;

            public sealed override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
            {
                // todo: handle cancellation
                var ch = this.channel;
                if (!ch.Open)
                {
                    return this.CreateClosedChannelExceptionTask();
                }

                try
                {
                    if (ch.connectPromise is object)
                    {
                        ThrowHelper.ThrowInvalidOperationException_ConnAttemptAlreadyMade();
                    }

                    bool wasActive = this.channel.Active;
                    if (ch.DoConnect(remoteAddress, localAddress))
                    {
                        this.FulfillConnectPromise(ch.connectPromise, wasActive);
                        return TaskUtil.Completed;
                    }
                    else
                    {
                        ch.connectPromise = ch.NewPromise(remoteAddress);

                        // Schedule connect timeout.
                        TimeSpan connectTimeout = ch.Configuration.ConnectTimeout;
                        if (connectTimeout > TimeSpan.Zero)
                        {
                            ch.connectCancellationTask = ch.EventLoop.Schedule(
                                ConnectTimeoutAction, this.channel,
                                remoteAddress, connectTimeout);
                        }

                        ch.connectPromise.Task.ContinueWith(CloseSafeOnCompleteAction, ch,
                            TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously);

                        return ch.connectPromise.Task;
                    }
                }
                catch (Exception ex)
                {
                    this.CloseIfClosed();
                    return TaskUtil.FromException(this.AnnotateConnectException(ex, remoteAddress));
                }
            }

            void FulfillConnectPromise(IPromise promise, bool wasActive)
            {
                if (promise is null)
                {
                    // Closed via cancellation and the promise has been notified already.
                    return;
                }

                var ch = this.channel;

                // Get the state as trySuccess() may trigger an ChannelFutureListener that will close the Channel.
                // We still need to ensure we call fireChannelActive() in this case.
                bool active = ch.Active;

                // trySuccess() will return false if a user cancelled the connection attempt.
                bool promiseSet = promise.TryComplete();

                // Regardless if the connection attempt was cancelled, channelActive() event should be triggered,
                // because what happened is what happened.
                if (!wasActive && active)
                {
                    ch.Pipeline.FireChannelActive();
                }

                // If a user cancelled the connection attempt, close the channel, which is followed by channelInactive().
                if (!promiseSet)
                {
                    this.Close(this.VoidPromise());
                }
            }

            void FulfillConnectPromise(IPromise promise, Exception cause)
            {
                if (promise is null)
                {
                    // Closed via cancellation and the promise has been notified already.
                    return;
                }

                // Use tryFailure() instead of setFailure() to avoid the race against cancel().
                promise.TrySetException(cause);
                this.CloseIfClosed();
            }

            public void FinishConnect(SocketChannelAsyncOperation<TChannel, TUnsafe> operation)
            {
                var ch = this.channel;
                Debug.Assert(ch.EventLoop.InEventLoop);

                try
                {
                    bool wasActive = ch.Active;
                    ch.DoFinishConnect(operation);
                    this.FulfillConnectPromise(ch.connectPromise, wasActive);
                }
                catch (Exception ex)
                {
                    var promise = ch.connectPromise;
                    var remoteAddress = (EndPoint)promise?.Task.AsyncState;
                    this.FulfillConnectPromise(ch.connectPromise, this.AnnotateConnectException(ex, remoteAddress));
                }
                finally
                {
                    // Check for null as the connectTimeoutFuture is only created if a connectTimeoutMillis > 0 is used
                    // See https://github.com/netty/netty/issues/1770
                    ch.connectCancellationTask?.Cancel();
                    ch.connectPromise = null;
                }
            }

            public abstract void FinishRead(SocketChannelAsyncOperation<TChannel, TUnsafe> operation);

            protected sealed override void Flush0()
            {
                // Flush immediately only when there's no pending flush.
                // If there's a pending flush operation, event loop will call FinishWrite() later,
                // and thus there's no need to call it now.
                if (!this.IsFlushPending()) { base.Flush0(); }
            }

            public void FinishWrite(SocketChannelAsyncOperation<TChannel, TUnsafe> operation)
            {
                var ch = this.channel;
                bool resetWritePending = ch.TryResetState(StateFlags.WriteScheduled);

                Debug.Assert(resetWritePending);

                ChannelOutboundBuffer input = this.OutboundBuffer;
                try
                {
                    operation.Validate();
                    int sent = operation.BytesTransferred;
                    ch.ResetWriteOperation();
                    if (sent > 0)
                    {
                        input.RemoveBytes(sent);
                    }
                }
                catch (Exception ex)
                {
                    ch.Pipeline.FireExceptionCaught(ex);
                    this.Close(this.VoidPromise(), ThrowHelper.GetClosedChannelException_FailedToWrite(ex), WriteClosedChannelException, false);
                }

                // Double check if there's no pending flush
                // See https://github.com/Azure/DotNetty/issues/218
                this.Flush0(); // todo: does it make sense now that we've actually written out everything that was flushed previously? concurrent flush handling?
            }

            bool IsFlushPending() => this.channel.IsInState(StateFlags.WriteScheduled);
        }

        protected override bool IsCompatible(IEventLoop eventLoop) => true;

        protected override void DoBeginRead()
        {
            if (SharedConstants.False < (uint)Volatile.Read(ref this.inputShutdown))
            {
                return;
            }

            if (!this.Open)
            {
                return;
            }

            this.ReadPending = true;

            if (!this.IsInState(StateFlags.ReadScheduled))
            {
                this.State |= StateFlags.ReadScheduled;
                this.ScheduleSocketRead();
            }
        }

        protected abstract void ScheduleSocketRead();

        /// <summary>
        ///     Connect to the remote peer
        /// </summary>
        protected abstract bool DoConnect(EndPoint remoteAddress, EndPoint localAddress);

        /// <summary>
        ///     Finish the connect
        /// </summary>
        protected abstract void DoFinishConnect(SocketChannelAsyncOperation<TChannel, TUnsafe> operation);

        protected override void DoClose()
        {
            var promise = this.connectPromise;
            if (promise is object)
            {
                // Use TrySetException() instead of SetException() to avoid the race against cancellation due to timeout.
                promise.TrySetException(ThrowHelper.GetClosedChannelException());
                this.connectPromise = null;
            }

            IScheduledTask cancellationTask = this.connectCancellationTask;
            if (cancellationTask is object)
            {
                cancellationTask.Cancel();
                this.connectCancellationTask = null;
            }

            var readOp = this.readOperation;
            if (readOp is object)
            {
                readOp.Dispose();
                this.readOperation = null;
            }

            var writeOp = this.writeOperation;
            if (writeOp is object)
            {
                writeOp.Dispose();
                this.writeOperation = null;
            }
        }
    }
}