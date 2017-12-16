using System;
using DotNetty.Transport.Channels;

namespace DotNetty.Transport.Libuv
{
    interface INativeChannel : IChannel
    {
        IntPtr GetLoopHandle();
    }
}
