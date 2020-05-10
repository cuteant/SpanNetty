// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using DotNetty.Common.Concurrency;

    sealed class DefaultChannelHandlerContext : AbstractChannelHandlerContext
    {
        public DefaultChannelHandlerContext(
            DefaultChannelPipeline pipeline, IEventExecutor executor, string name, IChannelHandler handler)
            : base(pipeline, executor, name, GetSkipPropagationFlags(handler))
        {
            if (handler is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.handler); }

            this.Handler = handler;
        }

        public override IChannelHandler Handler { get; }
    }
}