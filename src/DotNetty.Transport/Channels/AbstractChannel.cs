// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using CuteAnt.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public abstract partial class AbstractChannel<TChannel, TUnsafe> : DefaultAttributeMap, IChannel
    {
        protected static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance(typeof(TChannel));

        static readonly ClosedChannelException Flush0ClosedChannelException = new ClosedChannelException();
        static readonly ClosedChannelException EnsureOpenClosedChannelException = new ClosedChannelException();
        static readonly ClosedChannelException CloseClosedChannelException = new ClosedChannelException();
        protected static readonly ClosedChannelException WriteClosedChannelException = new ClosedChannelException();
        static readonly NotYetConnectedException Flush0NotYetConnectedException = new NotYetConnectedException();

        readonly TUnsafe channelUnsafe;

        readonly DefaultChannelPipeline pipeline;
        readonly VoidChannelPromise unsafeVoidPromise;
        readonly IPromise closeFuture;

        EndPoint localAddress;
        EndPoint remoteAddress;
        IEventLoop eventLoop;
        int registered;
        int closeInitiated;

        /// <summary>Cache for the string representation of this channel</summary>
        bool strValActive;

        string strVal;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="parent">The parent of this channel. Pass <c>null</c> if there's no parent.</param>
        protected AbstractChannel(IChannel parent)
        {
            this.Parent = parent;
            this.Id = this.NewId();
            this.channelUnsafe = this.NewUnsafe();
            this.pipeline = this.NewChannelPipeline();
            this.unsafeVoidPromise = new VoidChannelPromise(this, false);
            this.closeFuture = this.NewPromise();
        }

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="parent">The parent of this channel. Pass <c>null</c> if there's no parent.</param>
        /// <param name="id">An <see cref="IChannelId"/> for the new channel.</param>
        protected AbstractChannel(IChannel parent, IChannelId id)
        {
            this.Parent = parent;
            this.Id = id;
            this.channelUnsafe = this.NewUnsafe();
            this.pipeline = this.NewChannelPipeline();
            this.unsafeVoidPromise = new VoidChannelPromise(this, false);
            this.closeFuture = this.NewPromise();
        }

        public IChannelId Id { get; }

        public bool IsWritable
        {
            get
            {
                ChannelOutboundBuffer buf = this.channelUnsafe.OutboundBuffer;
                return buf != null && buf.IsWritable;
            }
        }

        public long BytesBeforeUnwritable
        {
            get
            {
                ChannelOutboundBuffer buf = this.channelUnsafe.OutboundBuffer;
                // isWritable() is currently assuming if there is no outboundBuffer then the channel is not writable.
                // We should be consistent with that here.
                return buf != null ? buf.BytesBeforeUnwritable : 0;
            }
        }

        public long BytesBeforeWritable
        {
            get
            {
                ChannelOutboundBuffer buf = this.channelUnsafe.OutboundBuffer;
                // isWritable() is currently assuming if there is no outboundBuffer then the channel is not writable.
                // We should be consistent with that here.
                return buf != null ? buf.BytesBeforeWritable : long.MaxValue;
            }
        }

        public IChannel Parent { get; }

        public IChannelPipeline Pipeline => this.pipeline;

        public abstract IChannelConfiguration Configuration { get; }

        public IByteBufferAllocator Allocator => this.Configuration.Allocator;

        public IEventLoop EventLoop
        {
            get
            {
                IEventLoop eventLoop = Volatile.Read(ref this.eventLoop);
                if (eventLoop == null)
                {
                    ThrowHelper.ThrowInvalidOperationException_ChannelNotReg();
                }
                return eventLoop;
            }
        }

        public abstract bool Open { get; }

        public abstract bool Active { get; }

        public abstract ChannelMetadata Metadata { get; }

        public EndPoint LocalAddress
        {
            get
            {
                EndPoint address = Volatile.Read(ref this.localAddress);
                return address ?? this.CacheLocalAddress();
            }
        }

        public EndPoint RemoteAddress
        {
            get
            {
                EndPoint address = Volatile.Read(ref this.remoteAddress);
                return address ?? this.CacheRemoteAddress();
            }
        }

        protected abstract EndPoint LocalAddressInternal { get; }

        protected void InvalidateLocalAddress() => Interlocked.Exchange(ref this.localAddress, null);

        protected EndPoint CacheLocalAddress()
        {
            try
            {
                var localAddr = this.LocalAddressInternal;
                Interlocked.Exchange(ref this.localAddress, localAddr);
                return localAddr;
            }
            catch (Exception)
            {
                // Sometimes fails on a closed socket in Windows.
                return null;
            }
        }

        protected abstract EndPoint RemoteAddressInternal { get; }

        /// <summary>
        /// Resets the stored <see cref="RemoteAddress"/>.
        /// </summary>
        protected void InvalidateRemoteAddress() => Interlocked.Exchange(ref this.remoteAddress, null);

        protected EndPoint CacheRemoteAddress()
        {
            try
            {
                var remoteAddr = this.RemoteAddressInternal;
                Interlocked.Exchange(ref this.remoteAddress, remoteAddr);
                return remoteAddr;
            }
            catch (Exception)
            {
                // Sometimes fails on a closed socket in Windows.
                return null;
            }
        }

        public bool Registered => Constants.True == Volatile.Read(ref this.registered);

        /// <summary>
        /// Returns a new <see cref="DefaultChannelId"/> instance. Subclasses may override this method to assign custom
        /// <see cref="IChannelId"/>s to <see cref="IChannel"/>s that use the <see cref="AbstractChannel{TChannel, TUnsafe}"/> constructor.
        /// </summary>
        /// <returns>A new <see cref="DefaultChannelId"/> instance.</returns>
        protected virtual IChannelId NewId() => DefaultChannelId.NewInstance();

        /// <summary>Returns a new pipeline instance.</summary>
        protected virtual DefaultChannelPipeline NewChannelPipeline() => new DefaultChannelPipeline(this);

        public virtual Task BindAsync(EndPoint localAddress) => this.pipeline.BindAsync(localAddress);

        public virtual Task ConnectAsync(EndPoint remoteAddress) => this.pipeline.ConnectAsync(remoteAddress);

        public virtual Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress) => this.pipeline.ConnectAsync(remoteAddress, localAddress);

        public virtual Task DisconnectAsync() => this.pipeline.DisconnectAsync();

        public virtual Task DisconnectAsync(IPromise promise) => this.pipeline.DisconnectAsync(promise);

        public virtual Task CloseAsync() => this.pipeline.CloseAsync();

        public virtual Task CloseAsync(IPromise promise) => this.pipeline.CloseAsync(promise);

        public Task DeregisterAsync() => this.pipeline.DeregisterAsync();

        public Task DeregisterAsync(IPromise promise) => this.pipeline.DeregisterAsync(promise);

        public IChannel Flush()
        {
            this.pipeline.Flush();
            return this;
        }

        public IChannel Read()
        {
            this.pipeline.Read();
            return this;
        }

        public Task WriteAsync(object msg) => this.pipeline.WriteAsync(msg);

        public Task WriteAsync(object message, IPromise promise) => this.pipeline.WriteAsync(message, promise);

        public Task WriteAndFlushAsync(object message) => this.pipeline.WriteAndFlushAsync(message);

        public Task WriteAndFlushAsync(object message, IPromise promise) => this.pipeline.WriteAndFlushAsync(message, promise);

        public IPromise NewPromise() => new TaskCompletionSource();

        public IPromise NewPromise(object state) => new TaskCompletionSource(state);

        public IPromise VoidPromise() => this.pipeline.VoidPromise();

        public Task CloseCompletion => this.closeFuture.Task;

        IChannelUnsafe IChannel.Unsafe => this.channelUnsafe;
        public TUnsafe Unsafe => this.channelUnsafe;

        /// <summary>
        /// Create a new <see cref="AbstractUnsafe" /> instance which will be used for the life-time of the
        /// <see cref="IChannel" />
        /// </summary>
        protected virtual TUnsafe NewUnsafe()
        {
            var @unsafe = new TUnsafe();
            @unsafe.Initialize((TChannel)this);
            return @unsafe;
        }

        /// <summary>
        /// Returns the ID of this channel.
        /// </summary>
        public override int GetHashCode() => this.Id.GetHashCode();

        /// <summary>
        /// Returns <c>true</c> if and only if the specified object is identical
        /// with this channel (i.e. <c>this == o</c>).
        /// </summary>
        public override bool Equals(object o) => this == o;

        public int CompareTo(IChannel o) => ReferenceEquals(this, o) ? 0 : this.Id.CompareTo(o.Id);

        /// <summary>
        /// Returns the string representation of this channel. The returned string contains a hex dump of the
        /// <see cref="IChannelId"/>, the <see cref="LocalAddress"/>, and the <see cref="RemoteAddress"/> of this
        /// channel for easier identification.
        /// </summary>
        public override string ToString()
        {
            bool active = this.Active;
            if (this.strValActive == active && this.strVal != null)
            {
                return this.strVal;
            }

            EndPoint remoteAddr = this.RemoteAddress;
            EndPoint localAddr = this.LocalAddress;
            if (remoteAddr != null)
            {
                var buf = StringBuilderCache.Acquire(96)
                    .Append("[id: 0x")
                    .Append(this.Id.AsShortText())
                    .Append(", L:")
                    .Append(localAddr)
                    .Append(active ? " - " : " ! ")
                    .Append("R:")
                    .Append(remoteAddr)
                    .Append(']');
                this.strVal = StringBuilderCache.GetStringAndRelease(buf);
            }
            else if (localAddr != null)
            {
                var buf = StringBuilderCache.Acquire(64)
                    .Append("[id: 0x")
                    .Append(this.Id.AsShortText())
                    .Append(", L:")
                    .Append(localAddr)
                    .Append(']');
                this.strVal = StringBuilderCache.GetStringAndRelease(buf);
            }
            else
            {
                var buf = StringBuilderCache.Acquire(16)
                    .Append("[id: 0x")
                    .Append(this.Id.AsShortText())
                    .Append(']');
                this.strVal = StringBuilderCache.GetStringAndRelease(buf);
            }

            this.strValActive = active;
            return this.strVal;
        }

        /// <summary>
        /// <see cref="IChannelUnsafe" /> implementation which sub-classes must extend and use.
        /// </summary>
        public abstract partial class AbstractUnsafe : IChannelUnsafe
        {
            protected TChannel channel; // ## 苦竹 修改 readonly ##
            ChannelOutboundBuffer outboundBuffer;
            IRecvByteBufAllocatorHandle recvHandle;
            bool inFlush0;

            /// <summary> true if the channel has never been registered, false otherwise /// </summary>
            bool neverRegistered = true;

            public IRecvByteBufAllocatorHandle RecvBufAllocHandle
                => this.recvHandle ?? (this.recvHandle = this.channel.Configuration.RecvByteBufAllocator.NewHandle());

            //public ChannelHandlerInvoker invoker() {
            //    // return the unwrapped invoker.
            //    return ((PausableChannelEventExecutor) eventLoop().asInvoker()).unwrapInvoker();
            //}
            public AbstractUnsafe() { }
            public virtual void Initialize(IChannel channel)
            {
                this.channel = (TChannel)channel;
                Interlocked.Exchange(ref this.outboundBuffer, new ChannelOutboundBuffer(channel));
            }

            public TChannel Channel => this.channel;

            public ChannelOutboundBuffer OutboundBuffer => Volatile.Read(ref this.outboundBuffer);

            [Conditional("DEBUG")]
            void AssertEventLoop() => Debug.Assert(Constants.False == Volatile.Read(ref this.channel.registered) || Volatile.Read(ref this.channel.eventLoop).InEventLoop);

            public Task RegisterAsync(IEventLoop eventLoop)
            {
                if (null == eventLoop) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.eventLoop); }

                var ch = this.channel;
                if (ch.Registered)
                {
                    return ThrowHelper.ThrowInvalidOperationException_RegisteredToEventLoopAlready();
                }

                if (!ch.IsCompatible(eventLoop))
                {
                    return ThrowHelper.ThrowInvalidOperationException_IncompatibleEventLoopType(eventLoop);
                }

                Interlocked.Exchange(ref ch.eventLoop, eventLoop);

                var promise = ch.NewPromise();

                if (eventLoop.InEventLoop)
                {
                    this.Register0(promise);
                }
                else
                {
                    try
                    {
                        eventLoop.Execute(RegisterAction, this, promise);
                    }
                    catch (Exception ex)
                    {
                        if (Logger.WarnEnabled) Logger.ForceClosingAChannel(ch, ex);
                        this.CloseForcibly();
                        ch.closeFuture.Complete();
                        Util.SafeSetFailure(promise, ex, Logger);
                    }
                }

                return promise.Task;
            }

            void Register0(IPromise promise)
            {
                var ch = this.channel;
                try
                {
                    // check if the channel is still open as it could be closed input the mean time when the register
                    // call was outside of the eventLoop
                    if (!promise.SetUncancellable() || !this.EnsureOpen(promise))
                    {
                        Util.SafeSetFailure(promise, ThrowHelper.GetClosedChannelException(), Logger);
                        return;
                    }
                    bool firstRegistration = this.neverRegistered;
                    ch.DoRegister();
                    this.neverRegistered = false;
                    Interlocked.Exchange(ref ch.registered, Constants.True);

                    // Ensure we call handlerAdded(...) before we actually notify the promise. This is needed as the
                    // user may already fire events through the pipeline in the ChannelFutureListener.
                    ch.pipeline.InvokeHandlerAddedIfNeeded();

                    Util.SafeSetSuccess(promise, Logger);
                    ch.pipeline.FireChannelRegistered();
                    // Only fire a channelActive if the channel has never been registered. This prevents firing
                    // multiple channel actives if the channel is deregistered and re-registered.
                    if (ch.Active)
                    {
                        if (firstRegistration)
                        {
                            ch.pipeline.FireChannelActive();
                        }
                        else if (ch.Configuration.AutoRead)
                        {
                            // This channel was registered before and autoRead() is set. This means we need to begin read
                            // again so that we process inbound data.
                            //
                            // See https://github.com/netty/netty/issues/4805
                            this.BeginRead();
                        }
                    }
                }
                catch (Exception t)
                {
                    // Close the channel directly to avoid FD leak.
                    this.CloseForcibly();
                    ch.closeFuture.Complete();
                    Util.SafeSetFailure(promise, t, Logger);
                }
            }

            public Task BindAsync(EndPoint localAddress)
            {
                this.AssertEventLoop();

                var ch = this.channel;
                // todo: cancellation support
                if ( /*!promise.setUncancellable() || */!ch.Open)
                {
                    return this.CreateClosedChannelExceptionTask();
                }

                //// See: https://github.com/netty/netty/issues/576
                //if (bool.TrueString.Equals(this.channel.Configuration.getOption(ChannelOption.SO_BROADCAST)) &&
                //    localAddress is IPEndPoint &&
                //    !((IPEndPoint)localAddress).Address.getAddress().isAnyLocalAddress() &&
                //    !Environment.OSVersion.Platform == PlatformID.Win32NT && !Environment.isRoot())
                //{
                //    // Warn a user about the fact that a non-root user can't receive a
                //    // broadcast packet on *nix if the socket is bound on non-wildcard address.
                //    logger.Warn(
                //        "A non-root user can't receive a broadcast packet if the socket " +
                //            "is not bound to a wildcard address; binding to a non-wildcard " +
                //            "address (" + localAddress + ") anyway as requested.");
                //}

                bool wasActive = ch.Active;
                try
                {
                    ch.DoBind(localAddress);
                }
                catch (Exception t)
                {
                    this.CloseIfClosed();
                    return TaskUtil.FromException(t);
                }

                if (!wasActive && ch.Active)
                {
                    this.InvokeLater(() => ch.pipeline.FireChannelActive());
                }

                return TaskUtil.Completed;
            }

            public abstract Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress);

            public void Disconnect(IPromise promise)
            {
                this.AssertEventLoop();

                if (!promise.SetUncancellable()) { return; }

                var ch = this.channel;
                bool wasActive = ch.Active;
                try
                {
                    ch.DoDisconnect();
                }
                catch (Exception t)
                {
                    Util.SafeSetFailure(promise, t, Logger);
                    this.CloseIfClosed();
                    return;
                }

                if (wasActive && !ch.Active)
                {
                    this.InvokeLater(() => ch.pipeline.FireChannelInactive());
                }

                Util.SafeSetSuccess(promise, Logger);

                this.CloseIfClosed(); // doDisconnect() might have closed the channel
            }

            public void Close(IPromise promise) /*CancellationToken cancellationToken) */
            {
                this.AssertEventLoop();

                this.Close(promise, CloseClosedChannelException, CloseClosedChannelException, false);
            }

            protected void Close(IPromise promise, Exception cause, ClosedChannelException closeCause, bool notify)
            {
                if (!promise.SetUncancellable()) { return; }

                var ch = this.channel;
                if (Constants.True == Interlocked.Exchange(ref ch.closeInitiated, Constants.True))
                {
                    var closeCompletion = ch.CloseCompletion;
                    if (closeCompletion.IsCompleted)
                    {
                        // Closed already.
                        Util.SafeSetSuccess(promise, Logger);
                    }
                    else if (!promise.IsVoid) // Only needed if no VoidChannelPromise.
                    {
                        closeCompletion.LinkOutcome(promise);
                    }
                    return;
                }

                bool wasActive = ch.Active;
                var outboundBuffer = Interlocked.Exchange(ref this.outboundBuffer, null); // Disallow adding any messages and flushes to outboundBuffer.
                IEventExecutor closeExecutor = this.PrepareToClose();
                if (closeExecutor != null)
                {
                    closeExecutor.Execute(() =>
                    {
                        try
                        {
                            // Execute the close.
                            this.DoClose0(promise);
                        }
                        finally
                        {
                            // Call invokeLater so closeAndDeregister is executed input the EventLoop again!
                            this.InvokeLater(() =>
                            {
                                if (outboundBuffer != null)
                                {
                                    // Fail all the queued messages
                                    outboundBuffer.FailFlushed(cause, notify);
                                    outboundBuffer.Close(closeCause);
                                }
                                this.FireChannelInactiveAndDeregister(wasActive);
                            });
                        }
                    });
                }
                else
                {
                    try
                    {
                        // Close the channel and fail the queued messages input all cases.
                        this.DoClose0(promise);
                    }
                    finally
                    {
                        if (outboundBuffer != null)
                        {
                            // Fail all the queued messages.
                            outboundBuffer.FailFlushed(cause, notify);
                            outboundBuffer.Close(closeCause);
                        }
                    }
                    if (this.inFlush0)
                    {
                        this.InvokeLater(() => this.FireChannelInactiveAndDeregister(wasActive));
                    }
                    else
                    {
                        this.FireChannelInactiveAndDeregister(wasActive);
                    }
                }
            }

            void DoClose0(IPromise promise)
            {
                var ch = this.channel;
                try
                {
                    ch.DoClose();
                    ch.closeFuture.Complete();
                    Util.SafeSetSuccess(promise, Logger);
                }
                catch (Exception t)
                {
                    ch.closeFuture.Complete();
                    Util.SafeSetFailure(promise, t, Logger);
                }
            }

            void FireChannelInactiveAndDeregister(bool wasActive) => this.Deregister(this.VoidPromise(), wasActive && !this.channel.Active);

            public void CloseForcibly()
            {
                this.AssertEventLoop();

                try
                {
                    this.channel.DoClose();
                }
                catch (Exception e)
                {
                    if (Logger.WarnEnabled) Logger.FailedToCloseAChannel(e);
                }
            }

            /// <summary>
            /// This method must NEVER be called directly, but be executed as an
            /// extra task with a clean call stack instead. The reason for this
            /// is that this method calls <see cref="IChannelPipeline.FireChannelUnregistered"/>
            /// directly, which might lead to an unfortunate nesting of independent inbound/outbound
            /// events. See the comments input <see cref="InvokeLater"/> for more details.
            /// </summary>
            public void Deregister(IPromise promise)
            {
                this.AssertEventLoop();

                this.Deregister(promise, false);
            }

            void Deregister(IPromise promise, bool fireChannelInactive)
            {
                if (!promise.SetUncancellable())
                {
                    return;
                }

                var ch = this.channel;
                if (Constants.False == Volatile.Read(ref ch.registered))
                {
                    Util.SafeSetSuccess(promise, Logger);
                }

                // As a user may call deregister() from within any method while doing processing in the ChannelPipeline,
                // we need to ensure we do the actual deregister operation later. This is needed as for example,
                // we may be in the ByteToMessageDecoder.callDecode(...) method and so still try to do processing in
                // the old EventLoop while the user already registered the Channel to a new EventLoop. Without delay,
                // the deregister operation this could lead to have a handler invoked by different EventLoop and so
                // threads.
                //
                // See:
                // https://github.com/netty/netty/issues/4435
                this.InvokeLater(() =>
                {
                    try
                    {
                        ch.DoDeregister();
                    }
                    catch (Exception t)
                    {
                        if (Logger.WarnEnabled) Logger.UnexpectedExceptionOccurredWhileDeregisteringChannel(t);
                    }
                    finally
                    {
                        if (fireChannelInactive)
                        {
                            ch.pipeline.FireChannelInactive();
                        }
                        // Some transports like local and AIO does not allow the deregistration of
                        // an open channel.  Their doDeregister() calls close(). Consequently,
                        // close() calls deregister() again - no need to fire channelUnregistered, so check
                        // if it was registered.
                        if (Constants.True == Volatile.Read(ref ch.registered))
                        {
                            Interlocked.Exchange(ref ch.registered, Constants.False);
                            ch.pipeline.FireChannelUnregistered();
                        }
                        Util.SafeSetSuccess(promise, Logger);
                    }
                });
            }

            public void BeginRead()
            {
                this.AssertEventLoop();

                var ch = this.channel;
                if (!ch.Active)
                {
                    return;
                }

                try
                {
                    ch.DoBeginRead();
                }
                catch (Exception e)
                {
                    this.InvokeLater(() => ch.pipeline.FireExceptionCaught(e));
                    this.Close(this.VoidPromise());
                }
            }

            public void Write(object msg, IPromise promise)
            {
                this.AssertEventLoop();

                ChannelOutboundBuffer outboundBuffer = Volatile.Read(ref this.outboundBuffer);
                if (outboundBuffer == null)
                {
                    // If the outboundBuffer is null we know the channel was closed and so
                    // need to fail the future right away. If it is not null the handling of the rest
                    // will be done input flush0()
                    // See https://github.com/netty/netty/issues/2362
                    Util.SafeSetFailure(promise, WriteClosedChannelException, Logger);
                    // release message now to prevent resource-leak
                    ReferenceCountUtil.Release(msg);
                    return;
                }

                int size;
                try
                {
                    var ch = this.channel;
                    msg = ch.FilterOutboundMessage(msg);
                    size = ch.pipeline.EstimatorHandle.Size(msg);
                    if (size < 0)
                    {
                        size = 0;
                    }
                }
                catch (Exception t)
                {
                    Util.SafeSetFailure(promise, t, Logger);
                    ReferenceCountUtil.Release(msg);
                    return;
                }

                outboundBuffer.AddMessage(msg, size, promise);
            }

            public void Flush()
            {
                this.AssertEventLoop();

                ChannelOutboundBuffer outboundBuffer = Volatile.Read(ref this.outboundBuffer);
                if (outboundBuffer == null)
                {
                    return;
                }

                outboundBuffer.AddFlush();
                this.Flush0();
            }

            protected virtual void Flush0()
            {
                if (this.inFlush0)
                {
                    // Avoid re-entrance
                    return;
                }

                ChannelOutboundBuffer outboundBuffer = Volatile.Read(ref this.outboundBuffer);
                if (outboundBuffer == null || outboundBuffer.IsEmpty)
                {
                    return;
                }

                this.inFlush0 = true;

                var ch = this.channel;
                // Mark all pending write requests as failure if the channel is inactive.
                if (!this.CanWrite)
                {
                    try
                    {
                        if (ch.Open)
                        {
                            outboundBuffer.FailFlushed(Flush0NotYetConnectedException, true);
                        }
                        else
                        {
                            // Do not trigger channelWritabilityChanged because the channel is closed already.
                            outboundBuffer.FailFlushed(Flush0ClosedChannelException, false);
                        }
                    }
                    finally
                    {
                        this.inFlush0 = false;
                    }
                    return;
                }

                try
                {
                    ch.DoWrite(outboundBuffer);
                }
                catch (Exception ex)
                {
                    //if (ch.Configuration.AutoClose)
                    //{
                    /*
                     * Just call {@link #close(ChannelPromise, Throwable, boolean)} here which will take care of
                     * failing all flushed messages and also ensure the actual close of the underlying transport
                     * will happen before the promises are notified.
                     *
                     * This is needed as otherwise {@link #isActive()} , {@link #isOpen()} and {@link #isWritable()}
                     * may still return {@code true} even if the channel should be closed as result of the exception.
                     */
                    this.Close(this.VoidPromise(), ex, Flush0ClosedChannelException, false);
                    //}
                    //else
                    //{
                    //    try
                    //    {
                    //        shutdownOutput(voidPromise(), t);
                    //    }
                    //    catch(Exception ex2)
                    //    {
                    //        close(voidPromise(), t2, FLUSH0_CLOSED_CHANNEL_EXCEPTION, false);
                    //    }
                    //}
                }
                finally
                {
                    this.inFlush0 = false;
                }
            }

            protected virtual bool CanWrite => this.channel.Active;

            public IPromise VoidPromise()
            {
                this.AssertEventLoop();
                return this.channel.unsafeVoidPromise;
            }

            protected bool EnsureOpen(IPromise promise)
            {
                if (this.channel.Open)
                {
                    return true;
                }

                Util.SafeSetFailure(promise, EnsureOpenClosedChannelException, Logger);
                return false;
            }

            protected Task CreateClosedChannelExceptionTask() => ThrowHelper.ThrowClosedChannelException();

            protected void CloseIfClosed()
            {
                if (this.channel.Open)
                {
                    return;
                }
                this.Close(this.VoidPromise());
            }

            void InvokeLater(Action task)
            {
                try
                {
                    // This method is used by outbound operation implementations to trigger an inbound event later.
                    // They do not trigger an inbound event immediately because an outbound operation might have been
                    // triggered by another inbound event handler method.  If fired immediately, the call stack
                    // will look like this for example:
                    //
                    //   handlerA.inboundBufferUpdated() - (1) an inbound handler method closes a connection.
                    //   -> handlerA.ctx.close()
                    //      -> channel.unsafe.close()
                    //         -> handlerA.channelInactive() - (2) another inbound handler method called while input (1) yet
                    //
                    // which means the execution of two inbound handler methods of the same handler overlap undesirably.
                    this.channel.EventLoop.Execute(task);
                }
                catch (RejectedExecutionException e)
                {
                    if (Logger.WarnEnabled) Logger.CannotInvokeTaskLaterAsEventLoopRejectedIt(e);
                }
            }

            protected Exception AnnotateConnectException(Exception exception, EndPoint remoteAddress)
            {
                if (exception is SocketException)
                {
                    return new ConnectException("LogError connecting to " + remoteAddress, exception);
                }

                return exception;
            }

            /// <summary>
            /// Prepares to close the <see cref="IChannel"/>. If this method returns an <see cref="IEventExecutor"/>, the
            /// caller must call the <see cref="IExecutor.Execute(DotNetty.Common.Concurrency.IRunnable)"/> method with a task that calls
            /// <see cref="AbstractChannel{TChannel, TUnsafe}.DoClose"/> on the returned <see cref="IEventExecutor"/>. If this method returns <c>null</c>,
            /// <see cref="AbstractChannel{TChannel, TUnsafe}.DoClose"/> must be called from the caller thread. (i.e. <see cref="IEventLoop"/>)
            /// </summary>
            protected virtual IEventExecutor PrepareToClose() => null;
        }

        /// <summary>
        /// Checks whether a given <see cref="IEventLoop"/> is compatible with the <see cref="AbstractChannel{TChannel, TUnsafe}"/>.
        /// </summary>
        /// <param name="eventLoop">The <see cref="IEventLoop"/> to check compatibility.</param>
        /// <returns>
        /// <c>true</c> if the given <see cref="IEventLoop"/> is compatible with this <see cref="AbstractChannel{TChannel, TUnsafe}"/>
        /// instance, otherwise <c>false</c>.
        /// </returns>
        protected abstract bool IsCompatible(IEventLoop eventLoop);

        /// <summary>
        /// Is called after the <see cref="IChannel"/> is registered with its <see cref="IEventLoop"/> as part of the
        /// register process. Sub-classes may override this method.
        /// </summary>
        protected virtual void DoRegister()
        {
            // NOOP
        }

        /// <summary>
        /// Binds the <see cref="IChannel"/> to the <see cref="EndPoint"/>.
        /// </summary>
        /// <param name="localAddress">The <see cref="EndPoint"/> to bind.</param>
        protected abstract void DoBind(EndPoint localAddress);

        /// <summary>
        /// Disconnects this <see cref="IChannel"/> from its remote peer.
        /// </summary>
        protected abstract void DoDisconnect();

        /// <summary>
        /// Closes the <see cref="IChannel"/>.
        /// </summary>
        protected abstract void DoClose();

        /// <summary>
        /// Deregisters the <see cref="IChannel"/> from its <see cref="IEventLoop"/>. Sub-classes may override this
        /// method.
        /// </summary>
        protected virtual void DoDeregister()
        {
            // NOOP
        }

        /// <summary>
        /// ScheduleAsync a read operation.
        /// </summary>
        protected abstract void DoBeginRead();

        /// <summary>
        /// Flush the content of the given buffer to the remote peer.
        /// </summary>
        protected abstract void DoWrite(ChannelOutboundBuffer input);

        /// <summary>
        /// Invoked when a new message is added to a <see cref="ChannelOutboundBuffer"/> of this
        /// <see cref="AbstractChannel{TChannel, TUnsafe}"/>, so that the <see cref="IChannel"/> implementation converts the message to
        /// another. (e.g. heap buffer -> direct buffer).
        /// </summary>
        /// <param name="msg">The message to be filtered.</param>
        /// <returns>The filtered message.</returns>
        protected virtual object FilterOutboundMessage(object msg) => msg;
    }
}