// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    partial class AbstractSocketChannel<TChannel, TUnsafe> : AbstractChannel<TChannel, TUnsafe>
        where TChannel : AbstractSocketChannel<TChannel, TUnsafe>
        where TUnsafe : AbstractSocketChannel<TChannel, TUnsafe>.AbstractSocketUnsafe, new()
    {
        private static readonly Action<object> ClearReadPendingAction = OnClearReadPending;
        private static readonly Action<object, object> ConnectTimeoutAction = OnConnectTimeout;
        private static readonly Action<Task<int>, object> CloseSafeOnCompleteAction = OnCloseSafeOnComplete;

        private static void OnConnectCompletedSync(object u, object e) => ((ISocketChannelUnsafe)u).FinishConnect((SocketChannelAsyncOperation<TChannel, TUnsafe>)e);

        private static void OnReadCompletedSync(object u, object e) => ((ISocketChannelUnsafe)u).FinishRead((SocketChannelAsyncOperation<TChannel, TUnsafe>)e);

        private static void OnWriteCompletedSync(object u, object e) => ((ISocketChannelUnsafe)u).FinishWrite((SocketChannelAsyncOperation<TChannel, TUnsafe>)e);

        private static void OnClearReadPending(object channel) => ((TChannel)channel).ClearReadPending0();

        private static void OnConnectTimeout(object c, object a)
        {
            var self = (TChannel)c;
            // todo: call Socket.CancelConnectAsync(...)
            TaskCompletionSource promise = self.connectPromise;
            var cause = new ConnectTimeoutException("connection timed out: " + a.ToString());
            if (promise != null && promise.TrySetException(cause))
            {
                self.CloseSafe();
            }
        }

        private static void OnCloseSafeOnComplete(Task<int> t, object s)
        {
            var c = (TChannel)s;
            c.connectCancellationTask?.Cancel();
            c.connectPromise = null;
            c.CloseSafe();
        }
    }
}