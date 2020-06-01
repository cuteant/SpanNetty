// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Net.Sockets;
    using DotNetty.Common.Utilities;

    public class SocketChannelAsyncOperation<TChannel, TUnsafe> : SocketAsyncEventArgs
        where TChannel : AbstractSocketChannel<TChannel, TUnsafe>
        where TUnsafe : AbstractSocketChannel<TChannel, TUnsafe>.AbstractSocketUnsafe, new()
    {
        public SocketChannelAsyncOperation(TChannel channel)
            : this(channel, true)
        {
        }

        public SocketChannelAsyncOperation(TChannel channel, bool setEmptyBuffer)
        {
            if (channel is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.channel); }

            this.Channel = channel;
            this.Completed += AbstractSocketChannel<TChannel, TUnsafe>.IoCompletedCallback;
            if (setEmptyBuffer)
            {
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
                this.SetBuffer(Memory<byte>.Empty);
#else
                this.SetBuffer(ArrayExtensions.ZeroBytes, 0, 0);
#endif
            }
        }

        public void Validate()
        {
            SocketError socketError = this.SocketError;
            if (socketError != SocketError.Success)
            {
                ThrowHelper.ThrowSocketException(socketError);
            }
        }

        public TChannel Channel { get; private set; }
    }
}