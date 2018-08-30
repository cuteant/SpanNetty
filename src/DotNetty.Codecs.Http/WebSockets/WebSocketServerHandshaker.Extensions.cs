// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    partial class WebSocketServerHandshaker
    {
        static readonly Action<Task, object> RemoveHandlerAfterWriteAction = RemoveHandlerAfterWrite;
        static readonly Action<Task, object> CloseOnCompleteAction = CloseOnComplete;

        static void RemoveHandlerAfterWrite(Task t, object state)
        {
            var wrapped = (Tuple<TaskCompletionSource, IChannelPipeline, string>)state;
            if (t.Status == TaskStatus.RanToCompletion)
            {
                wrapped.Item2.Remove(wrapped.Item3);
                wrapped.Item1.TryComplete();
            }
            else
            {
                wrapped.Item1.TrySetException(t.Exception);
            }
        }

        static void CloseOnComplete(Task t, object s) => ((IChannel)s).CloseAsync();
    }
}
