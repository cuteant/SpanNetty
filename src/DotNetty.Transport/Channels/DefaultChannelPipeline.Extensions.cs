// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using Thread = DotNetty.Common.Concurrency.XThread;

    partial class DefaultChannelPipeline
    {
        static readonly Action<object, object> CallHandlerRemovedAction = OnCallHandlerRemoved;
        static readonly Action<object, object> DestroyUpAction = OnDestroyUp;
        static readonly Action<object, object> DestroyDownAction = OnDestroyDown;

        private static void OnCallHandlerAdded(object self, object ctx)
        {
            ((DefaultChannelPipeline)self).CallHandlerAdded0((AbstractChannelHandlerContext)ctx);
        }

        private static void OnCallHandlerRemoved(object self, object ctx)
        {
            ((DefaultChannelPipeline)self).CallHandlerRemoved0((AbstractChannelHandlerContext)ctx);
        }

        private static void OnDestroyUp(object self, object ctx)
        {
            ((DefaultChannelPipeline)self).DestroyUp((AbstractChannelHandlerContext)ctx, true);
        }

        private static void OnDestroyDown(object self, object ctx)
        {
            ((DefaultChannelPipeline)self).DestroyDown(Thread.CurrentThread, (AbstractChannelHandlerContext)ctx, true);
        }
    }
}