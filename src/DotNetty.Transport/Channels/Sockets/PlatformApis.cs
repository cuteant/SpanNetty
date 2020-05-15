using System.Runtime.InteropServices;

namespace DotNetty.Transport.Channels.Sockets
{
    internal static class PlatformApis
    {
        public static readonly bool IsWindows;

        public static readonly bool IsLinux;

        public static readonly bool IsDarwin;

        static PlatformApis()
        {
            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            IsDarwin = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        }
    }
}
