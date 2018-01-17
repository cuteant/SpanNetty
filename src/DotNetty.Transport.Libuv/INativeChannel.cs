using System;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Libuv.Native;

namespace DotNetty.Transport.Libuv
{
    interface INativeChannel : IChannel
    {
        bool IsBound { get; }
        NativeHandle GetHandle();
    }
}
