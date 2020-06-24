// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;

    partial class AbstractChannelHandlerContext
    {
        private static readonly Action<object> InvokeChannelRegisteredAction = OnInvokeChannelRegistered;
        private static readonly Action<object> InvokeChannelUnregisteredAction = OnInvokeChannelUnregistered;
        private static readonly Action<object> InvokeChannelActiveAction = OnInvokeChannelActive;
        private static readonly Action<object> InvokeChannelInactiveAction = OnInvokeChannelInactive;

        private static readonly Action<object, object> InvokeUserEventTriggeredAction = OnInvokeUserEventTriggered;
        private static readonly Action<object, object> InvokeChannelReadAction = OnInvokeChannelRead;
        private static readonly Action<object, object> InvokeExceptionCaughtAction = OnInvokeExceptionCaught;

        private static void OnInvokeUserEventTriggered(object ctx, object evt)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeUserEventTriggered(evt);
        }

        private static void OnInvokeChannelRead(object ctx, object msg)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeChannelRead(msg);
        }

        private static void OnInvokeChannelRegistered(object ctx)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeChannelRegistered();
        }

        private static void OnInvokeChannelUnregistered(object ctx)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeChannelUnregistered();
        }

        private static void OnInvokeChannelActive(object ctx)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeChannelActive();
        }

        private static void OnInvokeChannelInactive(object ctx)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeChannelInactive();
        }

        private static void OnInvokeExceptionCaught(object c, object e)
        {
            ((AbstractChannelHandlerContext)c).InvokeExceptionCaught((Exception)e);
        }
    }
}


#if !DESKTOPCLR && (NET45 || NET451 || NET46 || NET461 || NET462 || NET47 || NET471 || NET472)
  确保编译不出问题
#endif
#if !NETSTANDARD && (NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETSTANDARD2_0)
  确保编译不出问题
#endif
