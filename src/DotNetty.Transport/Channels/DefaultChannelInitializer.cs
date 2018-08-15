// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using DotNetty.Common.Utilities;

    public sealed class DefaultChannelInitializer<TChannel> : ChannelInitializer<TChannel>
        where TChannel : IChannel
    {
        private readonly IChannelInitializer<TChannel> _initialization;

        public DefaultChannelInitializer(IChannelInitializer<TChannel> initialization)
        {
            if (null == initialization) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.initialization);

            _initialization = initialization;
        }

        protected override void InitChannel(TChannel channel) => _initialization.InitChannel(channel);

        public override string ToString() => nameof(DefaultChannelInitializer<TChannel>) + "[" + StringUtil.SimpleClassName(typeof(TChannel)) + "]";
    }
}