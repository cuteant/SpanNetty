// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    partial class HttpObjectAggregator
    {
        static readonly Action<Task, object> CloseOnCompleteAction = CloseOnComplete;
        static readonly Action<Task, object> CloseOnFaultAction = CloseOnFault;

        static void CloseOnComplete(Task t, object s)
        {
            if (t.IsFaulted)
            {
                if (Logger.DebugEnabled) Logger.FailedToSendA413RequestEntityTooLarge(t);
            }
            ((IChannelHandlerContext)s).CloseAsync();
        }

        static void CloseOnFault(Task t, object s)
        {
            if (t.IsFaulted)
            {
                if (Logger.DebugEnabled) Logger.FailedToSendA413RequestEntityTooLarge(t);
                ((IChannelHandlerContext)s).CloseAsync();
            }
        }

        public override bool TryAcceptInboundMessage(object msg, out IHttpObject message)
        {
            message = msg as IHttpObject;
            if (null == message) { return false; }

            if (msg is IFullHttpMessage) { return false; }

            switch (msg)
            {
                case IHttpContent _:
                case IHttpMessage _:
                    return true;
                default:
                    return false;
            }
        }
    }
}
