// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable UseStringInterpolation
namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;

    partial class WebSocket08FrameDecoder
    {
        static void CloseOnComplete(Task t, object c) => ((IChannel)c).CloseAsync();
    }
}
