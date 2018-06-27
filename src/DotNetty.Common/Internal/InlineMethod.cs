using System.Runtime.CompilerServices;

namespace DotNetty
{
    /// <summary>Helper class for constants for inlining methods</summary>
    public static class InlineMethod
    {
        /// <summary>Value for lining method</summary>
        public const MethodImplOptions Value =
#if NET40
            (MethodImplOptions)256;
#else
            MethodImplOptions.AggressiveInlining;
#endif
    }
}