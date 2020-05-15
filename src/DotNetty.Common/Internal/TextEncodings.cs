namespace DotNetty.Common.Internal
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;

    public static partial class TextEncodings
    {
        // Reject any invalid UTF-8 data rather than silently replacing.
        public static readonly Encoding UTF8NoBOM = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        // Encoding Helpers
        private const char HighSurrogateStart = '\ud800';
        private const char HighSurrogateEnd = '\udbff';
        private const char LowSurrogateStart = '\udc00';
        private const char LowSurrogateEnd = '\udfff';

#if !NETCOREAPP_3_0_GREATER

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static int PtrDiff(char* a, char* b)
        {
            return (int)(((uint)((byte*)a - (byte*)b)) >> 1);
        }

        // byte* flavor just for parity
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static int PtrDiff(byte* a, byte* b)
        {
            return (int)(a - b);
        }

#endif
    }
}
