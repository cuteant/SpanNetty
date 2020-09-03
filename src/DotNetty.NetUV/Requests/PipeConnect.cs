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

namespace DotNetty.NetUV.Requests
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;

    internal sealed class PipeConnect : IDisposable
    {
        private readonly WatcherRequest _watcherRequest;
        private Action<Pipe, Exception> _connectedAction;

        public PipeConnect(Pipe pipe, string remoteName, Action<Pipe, Exception> connectedAction)
        {
            if (pipe is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.pipe); }
            if (string.IsNullOrEmpty(remoteName)) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.remoteName); }
            if (connectedAction is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connectedAction); }

            pipe.Validate();

            Pipe = pipe;
            _connectedAction = connectedAction;
            _watcherRequest = new WatcherRequest(
                uv_req_type.UV_CONNECT,
                (r, e) => OnConnected(e),
                h => NativeMethods.PipeConnect(h, pipe.InternalHandle, remoteName));
        }

        internal Pipe Pipe { get; private set; }

        private void OnConnected(/*WatcherRequest request, */Exception error)
        {
            if (Pipe is null || _connectedAction is null)
            {
                ThrowObjectDisposedException();
            }

            try
            {
                if (error is null)
                {
                    Pipe.ReadStart();
                }

                _connectedAction(Pipe, error);
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
                return new ObjectDisposedException($"{nameof(PipeConnect)} has already been disposed.");
            }
        }

        public void Dispose()
        {
            Pipe = null;
            _connectedAction = null;
            _watcherRequest.Dispose();
        }
    }
}
