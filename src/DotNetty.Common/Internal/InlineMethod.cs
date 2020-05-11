using System.Runtime.CompilerServices;

namespace DotNetty
{
    /// <summary>Helper class for constants for inlining methods</summary>
    public static class InlineMethod
    {
        /// <summary>Value for lining method</summary>
        public const MethodImplOptions AggressiveInlining =
#if NET40
            (MethodImplOptions)256;
#else
            MethodImplOptions.AggressiveInlining;
#endif

        /// <summary>Value for lining method</summary>
        public const MethodImplOptions AggressiveOptimization =
#if NETCOREAPP_3_0_GREATER
            MethodImplOptions.AggressiveOptimization;
#elif NET40
            (MethodImplOptions)256;
#else
            MethodImplOptions.AggressiveInlining;
#endif
    }
}