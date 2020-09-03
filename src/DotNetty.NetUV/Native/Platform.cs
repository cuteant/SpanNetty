/*
 * Copyright (c) Johnny Z. All rights reserved.
 *
 *   https://github.com/StormHub/NetUV
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com)
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.NetUV.Native
{
    using System.Net.Sockets;
    using System.Runtime.InteropServices;

    internal static class Platform
    {
        static Platform()
        {
            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            IsDarwin = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            IsUnix = IsLinux || IsDarwin;

            OSSupportsIPv6 = Socket.OSSupportsIPv6;
            OSSupportsIPv4 = Socket.OSSupportsIPv4;
        }

        public static readonly bool OSSupportsIPv6;

        public static readonly bool OSSupportsIPv4;

        public static readonly bool IsWindows;

        public static readonly bool IsUnix;

        public static readonly bool IsDarwin;

        public static readonly bool IsLinux;
    }
}
