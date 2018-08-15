// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    partial class AbstractChannelHandlerContext
    {
        private static void OnInvokeChannelReadComplete(object ctx)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeChannelReadComplete();
        }

        private static void OnInvokeRead(object ctx)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeRead();
        }

        private static void OnInvokeChannelWritabilityChanged(object ctx)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeChannelWritabilityChanged();
        }

        private static void OnInvokeFlush(object ctx)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeFlush();
        }

        private static void OnInvokeUserEventTriggered(object ctx, object evt)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeUserEventTriggered(evt);
        }

        private static void OnInvokeChannelRead(object ctx, object msg)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeChannelRead(msg);
        }
    }
}


#if !DESKTOPCLR && (NET40 || NET45 || NET451 || NET46 || NET461 || NET462 || NET47 || NET471)
  确保编译不出问题
#endif
#if !NETSTANDARD && (NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETSTANDARD2_0)
  确保编译不出问题
#endif
