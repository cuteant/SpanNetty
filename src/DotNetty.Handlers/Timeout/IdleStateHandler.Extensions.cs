// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Timeout
{
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    partial class IdleStateHandler
    {
        private static void WrappingHandleReadTimeout(object handler, object ctx)
        {
            var self = (IdleStateHandler)handler; // instead of this
            var context = (IChannelHandlerContext)ctx;

            if (!context.Channel.IsOpen)
            {
                return;
            }

            HandleReadTimeout(self, context);
        }

        private static void WrappingHandleWriteTimeout(object handler, object ctx)
        {
            var self = (IdleStateHandler)handler; // instead of this
            var context = (IChannelHandlerContext)ctx;

            if (!context.Channel.IsOpen)
            {
                return;
            }

            HandleWriteTimeout(self, context);
        }

        private static void WrappingHandleAllTimeout(object handler, object ctx)
        {
            var self = (IdleStateHandler)handler; // instead of this
            var context = (IChannelHandlerContext)ctx;

            if (!context.Channel.IsOpen)
            {
                return;
            }

            HandleAllTimeout(self, context);
        }

        private static void WrappingWriteListener(Task antecedent, object state)
        {
            var self = (IdleStateHandler)state;
            self._lastWriteTime = self.Ticks();
            self._firstWriterIdleEvent = self._firstAllIdleEvent = true;
        }
    }
}

