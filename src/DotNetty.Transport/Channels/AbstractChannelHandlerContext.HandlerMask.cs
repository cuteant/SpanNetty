// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Net;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Concurrency;

    partial class AbstractChannelHandlerContext
    {
        [Flags]
        protected internal enum SkipFlags
        {
            HandlerAdded = 1,
            HandlerRemoved = 1 << 1,
            ExceptionCaught = 1 << 2,
            ChannelRegistered = 1 << 3,
            ChannelUnregistered = 1 << 4,
            ChannelActive = 1 << 5,
            ChannelInactive = 1 << 6,
            ChannelRead = 1 << 7,
            ChannelReadComplete = 1 << 8,
            ChannelWritabilityChanged = 1 << 9,
            UserEventTriggered = 1 << 10,
            Bind = 1 << 11,
            Connect = 1 << 12,
            Disconnect = 1 << 13,
            Close = 1 << 14,
            Deregister = 1 << 15,
            Read = 1 << 16,
            Write = 1 << 17,
            Flush = 1 << 18,

            Inbound = ExceptionCaught |
                ChannelRegistered |
                ChannelUnregistered |
                ChannelActive |
                ChannelInactive |
                ChannelRead |
                ChannelReadComplete |
                ChannelWritabilityChanged |
                UserEventTriggered,

            Outbound = Bind |
                Connect |
                Disconnect |
                Close |
                Deregister |
                Read |
                Write |
                Flush,
        }

        private static readonly ConditionalWeakTable<Type, Tuple<SkipFlags>> SkipTable = new ConditionalWeakTable<Type, Tuple<SkipFlags>>();

        protected static SkipFlags GetSkipPropagationFlags(IChannelHandler handler)
        {
            Tuple<SkipFlags> skipDirection = SkipTable.GetValue(
                handler.GetType(),
                handlerType => Tuple.Create(CalculateSkipPropagationFlags(handlerType)));

            return skipDirection?.Item1 ?? 0;
        }

        protected static SkipFlags CalculateSkipPropagationFlags(Type handlerType)
        {
            SkipFlags flags = 0;

            // this method should never throw
            if (IsSkippable(handlerType, nameof(IChannelHandler.HandlerAdded)))
            {
                flags |= SkipFlags.HandlerAdded;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.HandlerRemoved)))
            {
                flags |= SkipFlags.HandlerRemoved;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ExceptionCaught), typeof(Exception)))
            {
                flags |= SkipFlags.ExceptionCaught;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelRegistered)))
            {
                flags |= SkipFlags.ChannelRegistered;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelUnregistered)))
            {
                flags |= SkipFlags.ChannelUnregistered;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelActive)))
            {
                flags |= SkipFlags.ChannelActive;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelInactive)))
            {
                flags |= SkipFlags.ChannelInactive;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelRead), typeof(object)))
            {
                flags |= SkipFlags.ChannelRead;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelReadComplete)))
            {
                flags |= SkipFlags.ChannelReadComplete;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ChannelWritabilityChanged)))
            {
                flags |= SkipFlags.ChannelWritabilityChanged;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.UserEventTriggered), typeof(object)))
            {
                flags |= SkipFlags.UserEventTriggered;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.BindAsync), typeof(EndPoint)))
            {
                flags |= SkipFlags.Bind;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.ConnectAsync), typeof(EndPoint), typeof(EndPoint)))
            {
                flags |= SkipFlags.Connect;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Disconnect), typeof(IPromise)))
            {
                flags |= SkipFlags.Disconnect;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Close), typeof(IPromise)))
            {
                flags |= SkipFlags.Close;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Deregister), typeof(IPromise)))
            {
                flags |= SkipFlags.Deregister;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Read)))
            {
                flags |= SkipFlags.Read;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Write), typeof(object), typeof(IPromise)))
            {
                flags |= SkipFlags.Write;
            }
            if (IsSkippable(handlerType, nameof(IChannelHandler.Flush)))
            {
                flags |= SkipFlags.Flush;
            }
            return flags;
        }

        protected static bool IsSkippable(Type handlerType, string methodName) => IsSkippable(handlerType, methodName, Type.EmptyTypes);

        protected static bool IsSkippable(Type handlerType, string methodName, params Type[] paramTypes)
        {
            if (paramTypes is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.paramTypes); }

            var paramTypeLength = paramTypes.Length;
            var newParamTypes = new Type[paramTypeLength + 1];
            newParamTypes[0] = typeof(IChannelHandlerContext);
            if ((uint)paramTypeLength > 0U)
            {
                Array.Copy(paramTypes, 0, newParamTypes, 1, paramTypeLength);
            }
            return handlerType.GetMethod(methodName, newParamTypes).GetCustomAttribute<SkipAttribute>(false) is object;
        }
    }
}
