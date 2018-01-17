namespace DotNetty.Transport.Libuv.Native
{
    using System.Runtime.InteropServices;

    partial class PlatformApi
    {
        public static readonly bool IsWindows;

        public static readonly bool IsLinux;

        public static readonly bool IsDarwin;

        static PlatformApi()
        {
            IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            IsDarwin = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        }
    }
}
