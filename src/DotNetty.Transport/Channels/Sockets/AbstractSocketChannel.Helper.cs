// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    partial class AbstractSocketChannel<TChannel, TUnsafe>
    {
        internal static readonly EventHandler<SocketAsyncEventArgs> IoCompletedCallback = OnIoCompleted;
        private static readonly Action<object, object> ConnectCallbackAction = OnConnectCompletedSync;
        private static readonly Action<object, object> ReadCallbackAction = OnReadCompletedSync;
        private static readonly Action<object, object> WriteCallbackAction = OnWriteCompletedSync;
        private static readonly Action<object> ClearReadPendingAction = OnClearReadPending;
        private static readonly Action<object, object> ConnectTimeoutAction = OnConnectTimeout;
        private static readonly Action<Task, object> CloseSafeOnCompleteAction = OnCloseSafeOnComplete;

        private static void OnConnectCompletedSync(object u, object e) => ((ISocketChannelUnsafe)u).FinishConnect((SocketChannelAsyncOperation<TChannel, TUnsafe>)e);

        private static void OnReadCompletedSync(object u, object e) => ((ISocketChannelUnsafe)u).FinishRead((SocketChannelAsyncOperation<TChannel, TUnsafe>)e);

        private static void OnWriteCompletedSync(object u, object e) => ((ISocketChannelUnsafe)u).FinishWrite((SocketChannelAsyncOperation<TChannel, TUnsafe>)e);

        private static void OnClearReadPending(object channel) => ((TChannel)channel).ClearReadPending0();

        private static void OnConnectTimeout(object c, object a)
        {
            var self = (TChannel)c;
            // todo: call Socket.CancelConnectAsync(...)
            var promise = self._connectPromise;
            var cause = new ConnectTimeoutException("connection timed out: " + a.ToString());
            if (promise is object && promise.TrySetException(cause))
            {
                self.CloseSafe();
            }
        }

        private static void OnCloseSafeOnComplete(Task t, object s)
        {
            var c = (TChannel)s;
            c._connectCancellationTask?.Cancel();
            c._connectPromise = null;
            c.CloseSafe();
        }
    }
}