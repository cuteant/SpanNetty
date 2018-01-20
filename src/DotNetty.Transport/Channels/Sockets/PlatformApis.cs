#if !NET40
using System.Runtime.InteropServices;
#endif

namespace DotNetty.Transport.Channels.Sockets
{
    internal static class PlatformApis
    {
        public static readonly bool IsWindows;

        public static readonly bool IsLinux;

        public static readonly bool IsDarwin;

        static PlatformApis()
        {
#if NET40
            IsWindows = true;
            IsLinux = false;
            IsDarwin = false;
#else
            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            IsDarwin = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
#endif
        }
    }
}
