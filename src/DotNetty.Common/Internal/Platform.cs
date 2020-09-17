// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using DotNetty.Common.Internal;

    public static class Platform
    {
        public static readonly bool OSSupportsIPv6;

        public static readonly bool OSSupportsIPv4;

        public static readonly bool IsWindows;

        public static readonly bool IsLinux;

        public static readonly bool IsDarwin;

        public static readonly bool IsUnix;

        static Platform()
        {
            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            IsDarwin = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            IsUnix = IsLinux || IsDarwin;

            OSSupportsIPv6 = Socket.OSSupportsIPv6;
            OSSupportsIPv4 = Socket.OSSupportsIPv4;
        }

        public static int GetCurrentProcessId() => PlatformProvider.Platform.GetCurrentProcessId();

        public static byte[] GetDefaultDeviceId() => PlatformProvider.Platform.GetDefaultDeviceId();
    }
}