#if NETCOREAPP_3_0_GREATER
namespace DotNetty.Common.Internal
{
    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using SysUtf8 = System.Text.Unicode.Utf8;

    public static partial class TextEncodings
    {
        public static partial class Utf16
        {
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe OperationStatus ToUtf8(in ReadOnlySpan<char> chars, Span<byte> utf8Destination, out int charsConsumed, out int bytesWritten)
            {
                // It's ok for us to operate on null / empty spans.

                fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
                fixed (byte* bytesPtr = &MemoryMarshal.GetReference(utf8Destination))
                {
                    if (TryGetBytesFast(charsPtr, chars.Length, bytesPtr, utf8Destination.Length, out charsConsumed, out bytesWritten))
                    {
                        return OperationStatus.Done;
                    }
                }

                return SysUtf8.FromUtf16(chars, utf8Destination, out charsConsumed, out bytesWritten);
            }

            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static unsafe OperationStatus ToUtf8(in ReadOnlySpan<char> chars, ref byte utf8Destination, int utf8Length, out int charsConsumed, out int bytesWritten)
            {
                // It's ok for us to operate on null / empty spans.

                fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
                fixed (byte* bytesPtr = &utf8Destination)
                {
                    if (TryGetBytesFast(charsPtr, chars.Length, bytesPtr, utf8Length, out charsConsumed, out bytesWritten))
                    {
                        return OperationStatus.Done;
                    }
                }

                return SysUtf8.FromUtf16(chars, MemoryMarshal.CreateSpan(ref utf8Destination, utf8Length), out charsConsumed, out bytesWritten);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe bool TryGetBytesFast(char* pChars, int charCount, byte* pBytes, int byteCount, out int charsConsumed, out int bytesWritten)
            {
                // First call into the fast path.

                bytesWritten = GetBytesFastInternal(pChars, charCount, pBytes, byteCount, out charsConsumed);

                return (charsConsumed == charCount); // All elements converted - return immediately.
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)] // called directly by TryGetBytesFast
            private static unsafe int GetBytesFastInternal(char* pChars, int charsLength, byte* pBytes, int bytesLength, out int charsConsumed)
            {
                // We don't care about the exact OperationStatus value returned by the workhorse routine; we only
                // care if the workhorse was able to consume the entire input payload. If we're unable to do so,
                // we'll handle the remainder in the fallback routine.

                char* pInputBufferRemaining;
                byte* pOutputBufferRemaining;

                _ = Utf8Utility.TranscodeToUtf8(pChars, charsLength, pBytes, bytesLength, out pInputBufferRemaining, out pOutputBufferRemaining);

                charsConsumed = (int)(pInputBufferRemaining - pChars);
                return (int)(pOutputBufferRemaining - pBytes);
            }
        }
    }
}
#endif