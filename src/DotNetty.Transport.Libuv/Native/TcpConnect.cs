// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System.Diagnostics;
    using System.Net;

    sealed class TcpConnect : ConnectRequest
    {
        readonly INativeUnsafe nativeUnsafe;

        public TcpConnect(INativeUnsafe nativeUnsafe, IPEndPoint remoteEndPoint)
        {
            Debug.Assert(nativeUnsafe is object);
            Debug.Assert(remoteEndPoint is object);

            NativeMethods.GetSocketAddress(remoteEndPoint, out sockaddr addr);
            int result = NativeMethods.uv_tcp_connect(
                this.Handle,
                nativeUnsafe.UnsafeHandle,
                ref addr,
                WatcherCallback);
            NativeMethods.ThrowIfError(result);

            this.nativeUnsafe = nativeUnsafe;
        }

        protected override void OnWatcherCallback() => this.nativeUnsafe.FinishConnect(this);
    }
}
