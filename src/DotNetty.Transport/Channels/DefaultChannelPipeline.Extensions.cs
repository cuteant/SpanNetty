// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    partial class DefaultChannelPipeline
    {
        private static void OnCallHandlerAdded(object self, object ctx)
        {
            ((DefaultChannelPipeline)self).CallHandlerAdded0((AbstractChannelHandlerContext)ctx);
        }
    }
}