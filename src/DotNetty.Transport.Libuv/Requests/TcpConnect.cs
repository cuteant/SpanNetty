/*
 * Copyright (c) Johnny Z. All rights reserved.
 *
 *   https://github.com/StormHub/NetUV
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com)
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Transport.Libuv.Requests
{
    using System;
    using System.Net;
    using System.Runtime.CompilerServices;
    using DotNetty.Transport.Libuv.Handles;
    using DotNetty.Transport.Libuv.Native;

    internal sealed class TcpConnect : IDisposable
    {
        private WatcherRequest _watcherRequest;
        private Action<Tcp, OperationException> _connectedAction;

        public TcpConnect(Tcp tcp, IPEndPoint remoteEndPoint, Action<Tcp, OperationException> connectedAction)
        {
            if (connectedAction is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connectedAction); }

            _connectedAction = connectedAction;
            Initialize(tcp, remoteEndPoint);
        }

        public TcpConnect(INativeChannelUnsafe channelUnsafe, IPEndPoint remoteEndPoint)
        {
            if (channelUnsafe is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.channelUnsafe); }

            var tcp = channelUnsafe.UnsafeHandle as Tcp;
            _connectedAction = (t, e) => channelUnsafe.FinishConnect(t, e);
            Initialize(tcp, remoteEndPoint);
        }

        private void Initialize(Tcp tcp, IPEndPoint remoteEndPoint)
        {
            if (tcp is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.tcp); }
            if (remoteEndPoint is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.remoteEndPoint); }

            tcp.Validate();

            Tcp = tcp;
            _watcherRequest = new WatcherRequest(
                uv_req_type.UV_CONNECT,
                (r, e) => OnConnected(e),
                h => NativeMethods.TcpConnect(h, tcp.InternalHandle, remoteEndPoint));
        }

        internal Tcp Tcp { get; private set; }

        private void OnConnected(/*WatcherRequest request, */OperationException error)
        {
            if (Tcp is null || _connectedAction is null)
            {
                ThrowObjectDisposedException();
            }

            try
            {
                if (error is null)
                {
                    Tcp.ReadStart();
                }

                _connectedAction(Tcp, error);
            }
            finally
            {
                Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowObjectDisposedException()
        {
            throw GetException();
            static ObjectDisposedException GetException()
            {
                return new ObjectDisposedException($"{nameof(TcpConnect)} has already been disposed.");
            }
        }

        public void Dispose()
        {
            Tcp = null;
            _connectedAction = null;
            _watcherRequest.Dispose();
        }
    }
}
