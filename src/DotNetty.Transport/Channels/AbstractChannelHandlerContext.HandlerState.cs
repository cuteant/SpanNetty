// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    partial class AbstractChannelHandlerContext
    {
        static class HandlerState
        {
            /// <summary>Neither <see cref="IChannelHandler.HandlerAdded"/> nor <see cref="IChannelHandler.HandlerRemoved"/> was called.</summary>
            public const int Init = 0;

            /// <summary><see cref="IChannelHandler.HandlerAdded"/> is about to be called.</summary>
            public const int AddPending = 1;

            /// <summary><see cref="IChannelHandler.HandlerAdded"/> was called.</summary>
            public const int AddComplete = 2;

            /// <summary><see cref="IChannelHandler.HandlerRemoved"/> was called.</summary>
            public const int RemoveComplete = 3;
        }
    }
}
