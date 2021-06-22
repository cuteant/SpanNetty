#if NETCOREAPP_3_0_GREATER
namespace DotNetty.Common.Internal
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public static partial class TextEncodings
    {
        public static partial class Utf8
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe bool TryGetCharCountFast(in ReadOnlySpan<byte> utf8Bytes, out int totalCharCount)
            {
                fixed (byte* bytesPtr = &MemoryMarshal.GetReference(utf8Bytes))
                {
                    var byteCount = utf8Bytes.Length;
                    totalCharCount = GetCharCountFastInternal(bytesPtr, byteCount, out int bytesConsumed);
                    if (bytesConsumed == byteCount) { return true; }
                }
                totalCharCount = 0; return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)] // called directly by TryGetCharCountFast
            private static unsafe int GetCharCountFastInternal(byte* pBytes, int bytesLength, out int bytesConsumed)
            {
                // The number of UTF-16 code units will never exceed the number of UTF-8 code units,
                // so the addition at the end of this method will not overflow.

                byte* ptrToFirstInvalidByte = Utf8Utility.GetPointerToFirstInvalidByte(pBytes, bytesLength, out int utf16CodeUnitCountAdjustment, out _);

                int tempBytesConsumed = (int)(ptrToFirstInvalidByte - pBytes);
                bytesConsumed = tempBytesConsumed;

                return tempBytesConsumed + utf16CodeUnitCountAdjustment;
            }

            //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public static unsafe int GetChars(in ReadOnlySpan<byte> utf8Bytes, Span<char> chars)
            {
                var byteCount = utf8Bytes.Length;
                if (0u >= (uint)byteCount) { return 0; }

                fixed (byte* bytesPtr = &MemoryMarshal.GetReference(utf8Bytes))
                fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
                {
                    // First call into the fast path.
                    var charsWritten = GetCharsFastInternal(bytesPtr, byteCount, charsPtr, chars.Length, out int bytesConsumed);

                    if (bytesConsumed == byteCount)
                    {
                        // All elements converted - return immediately.

                        return charsWritten;
                    }
                }
                return UTF8NoBOM.GetChars(utf8Bytes, chars);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)] // called directly by GetChars
            private static unsafe int GetCharsFastInternal(byte* pBytes, int bytesLength, char* pChars, int charsLength, out int bytesConsumed)
            {
                // We don't care about the exact OperationStatus value returned by the workhorse routine; we only
                // care if the workhorse was able to consume the entire input payload. If we're unable to do so,
                // we'll handle the remainder in the fallback routine.

                byte* pInputBufferRemaining;
                char* pOutputBufferRemaining;

                _ = Utf8Utility.TranscodeToUtf16(pBytes, bytesLength, pChars, charsLength, out pInputBufferRemaining, out pOutputBufferRemaining);

                bytesConsumed = (int)(pInputBufferRemaining - pBytes);
                return (int)(pOutputBufferRemaining - pChars);
            }

            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public static string GetString(in ReadOnlySpan<byte> utf8Bytes)
            {
                return UTF8NoBOM.GetString(utf8Bytes);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe bool TryGetByteCountFast(in ReadOnlySpan<char> chars, out int bytesNeeded)
            {
                // It's ok for us to pass null pointers down to the workhorse below.
                fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
                {
                    var charCount = chars.Length;
                    // First call into the fast path.
                    bytesNeeded = GetByteCountFastInternal(charsPtr, charCount, out int charsConsumed);
                    if (charsConsumed == charCount) { return true; }
                }
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)] // called directly by TryGetByteCountFast
            private static unsafe int GetByteCountFastInternal(char* pChars, int charsLength, out int charsConsumed)
            {
                // The number of UTF-8 code units may exceed the number of UTF-16 code units,
                // so we'll need to check for overflow before casting to Int32.

                char* ptrToFirstInvalidChar = Utf16Utility.GetPointerToFirstInvalidChar(pChars, charsLength, out long utf8CodeUnitCountAdjustment, out _);

                int tempCharsConsumed = (int)(ptrToFirstInvalidChar - pChars);
                charsConsumed = tempCharsConsumed;

                long totalUtf8Bytes = tempCharsConsumed + utf8CodeUnitCountAdjustment;
                if ((ulong)totalUtf8Bytes > int.MaxValue)
                {
                    ThrowConversionOverflow();
                }

                return (int)totalUtf8Bytes;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static void ThrowConversionOverflow()
            {
                throw GetArgumentException();
                static ArgumentException GetArgumentException()
                {
                    return new ArgumentException("Argument_ConversionOverflow");
                }
            }
        }
    }
}
#endif
