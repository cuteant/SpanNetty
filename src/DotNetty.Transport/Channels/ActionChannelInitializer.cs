// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using DotNetty.Common.Utilities;

    public sealed class ActionChannelInitializer<T> : ChannelInitializer<T>
        where T : IChannel
    {
        readonly Action<T> initializationAction;

        public ActionChannelInitializer(Action<T> initializationAction)
        {
            if (null == initializationAction) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.initializationAction); }

            this.initializationAction = initializationAction;
        }

        protected override void InitChannel(T channel) => this.initializationAction(channel);

        public override string ToString() => nameof(ActionChannelInitializer<T>) + "[" + StringUtil.SimpleClassName(typeof(T)) + "]";
    }
}