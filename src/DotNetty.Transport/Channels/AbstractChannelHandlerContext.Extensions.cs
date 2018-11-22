// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;

    partial class AbstractChannelHandlerContext
    {
        static readonly Action<object> InvokeChannelRegisteredAction = OnInvokeChannelRegistered;
        static readonly Action<object> InvokeChannelUnregisteredAction = OnInvokeChannelUnregistered;
        static readonly Action<object> InvokeChannelActiveAction = OnInvokeChannelActive;
        static readonly Action<object> InvokeChannelInactiveAction = OnInvokeChannelInactive;
        static readonly Action<object, object> InvokeExceptionCaughtAction = OnInvokeExceptionCaught;
        static readonly Action<object, object> SafeExecuteOutboundAsyncAction = OnSafeExecuteOutbound;
        static readonly Action<object, object> InvokeCloseAction = OnInvokeClose;
        static readonly Action<object, object> InvokeDisconnectAction = OnInvokeDisconnect;
        static readonly Action<object, object> InvokeDeregisterAction = OnInvokeDeregister;

        internal AbstractChannelHandlerContext Next
        {
            [MethodImpl(InlineMethod.Value)]
            get => Volatile.Read(ref _next);
            set => Interlocked.Exchange(ref _next, value);
        }
        internal AbstractChannelHandlerContext Prev
        {
            [MethodImpl(InlineMethod.Value)]
            get => Volatile.Read(ref _prev);
            set => Interlocked.Exchange(ref _prev, value);
        }

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

        private static void OnInvokeDeregister(object ctx, object promise)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeDeregister((IPromise)promise);
        }

        private static void OnInvokeDisconnect(object ctx, object promise)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeDisconnect((IPromise)promise);
        }

        private static void OnInvokeClose(object ctx, object promise)
        {
            ((AbstractChannelHandlerContext)ctx).InvokeClose((IPromise)promise);
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

        private static void OnSafeExecuteOutbound(object p, object func)
        {
            ((Func<Task>)func)().LinkOutcome((IPromise)p);
        }
    }
}


#if !DESKTOPCLR && (NET40 || NET45 || NET451 || NET46 || NET461 || NET462 || NET47 || NET471 || NET472)
  确保编译不出问题
#endif
#if !NETSTANDARD && (NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETSTANDARD2_0)
  确保编译不出问题
#endif
