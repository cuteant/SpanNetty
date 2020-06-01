// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    public abstract partial class NativeChannel<TChannel, TUnsafe> : AbstractChannel<TChannel, TUnsafe>, INativeChannel
        where TChannel : NativeChannel<TChannel, TUnsafe>
        where TUnsafe : NativeChannel<TChannel, TUnsafe>.NativeChannelUnsafe, new()
    {

        internal bool ReadPending;

        private int v_state;
        private int InternalState
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => Volatile.Read(ref v_state);
            set => Interlocked.Exchange(ref v_state, value);
        }

        private IPromise _connectPromise;
        private IScheduledTask _connectCancellationTask;

        protected NativeChannel(IChannel parent) : base(parent)
        {
            InternalState = StateFlags.Open;
        }

        public override bool Open => IsInState(StateFlags.Open);

        public override bool Active => IsInState(StateFlags.Active);

        protected override bool IsCompatible(IEventLoop eventLoop) => eventLoop is LoopExecutor;

        protected bool IsInState(int stateToCheck) => (InternalState & stateToCheck) == stateToCheck;

        protected void SetState(int stateToSet) => InternalState |= stateToSet;

        protected int ResetState(int stateToReset)
        {
            var oldState = InternalState;
            if ((oldState & stateToReset) != 0)
            {
                InternalState = oldState & ~stateToReset;
            }
            return oldState;
        }

        protected bool TryResetState(int stateToReset)
        {
            var oldState = InternalState;
            if ((oldState & stateToReset) != 0)
            {
                InternalState = oldState & ~stateToReset;
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
                    DoBind(localAddress);
                }
                request = new TcpConnect(Unsafe, (IPEndPoint)remoteAddress);
            }
            catch
            {
                request?.Dispose();
                throw;
            }
        }

        void DoFinishConnect() => OnConnected();

        protected override void DoClose()
        {
            var promise = _connectPromise;
            if (promise is object)
            {
                promise.TrySetException(ThrowHelper.GetClosedChannelException());
                _connectPromise = null;
            }
        }

        protected virtual void OnConnected()
        {
            SetState(StateFlags.Active);
            CacheLocalAddress();
            CacheRemoteAddress();
        }

        protected abstract void DoStopRead();

        NativeHandle INativeChannel.GetHandle() => GetHandle();
        internal abstract NativeHandle GetHandle();
        bool INativeChannel.IsBound => IsBound;
        internal abstract bool IsBound { get; }

    }
}
