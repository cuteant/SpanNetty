// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    partial class NativeChannel<TChannel, TUnsafe> : AbstractChannel<TChannel, TUnsafe>, INativeChannel
        where TChannel : NativeChannel<TChannel, TUnsafe>
        where TUnsafe : NativeChannel<TChannel, TUnsafe>.NativeChannelUnsafe, new()
    {
    }

    internal interface INativeUnsafe
    {
        IntPtr UnsafeHandle { get; }

        void FinishConnect(ConnectRequest request);

        uv_buf_t PrepareRead(ReadOperation readOperation);

        void FinishRead(ReadOperation readOperation);

        void FinishWrite(int bytesWritten, OperationException error);
    }
}
