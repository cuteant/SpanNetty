// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace DotNetty.Transport.Channels
{
    partial class CombinedChannelDuplexHandler<TIn, TOut>
    {
        private void OnExceptionCaught(Exception cause)
        {
            try
            {
                this.OutboundHandler.ExceptionCaught(this.outboundCtx, cause);
            }
            catch (Exception error)
            {
                if (Logger.DebugEnabled)
                {
                    Logger.FreedThreadLocalBufferFromThreadFull(error, cause);
                }
                else if (Logger.WarnEnabled)
                {
                    Logger.FreedThreadLocalBufferFromThread(error, cause);
                }
            }
        }

        partial class DelegatingChannelHandlerContext
        {
            private static readonly Action<object> RemoveAction = OnRemove;

            private static void OnRemove(object c)
            {
                ((DelegatingChannelHandlerContext)c).Remove0();
            }
        }
    }
}
