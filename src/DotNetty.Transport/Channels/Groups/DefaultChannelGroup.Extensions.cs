// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Transport.Channels.Groups
{
    using System;
    using System.Threading.Tasks;

    partial class DefaultChannelGroup
    {
        static readonly Action<Task, object> RemoveChannelAfterCloseAction = RemoveChannelAfterClose;

        static void RemoveChannelAfterClose(Task t, object s)
        {
            var wrapped = (Tuple<DefaultChannelGroup, IChannel>)s;
            wrapped.Item1.Remove(wrapped.Item2);
        }
    }
}
#endif