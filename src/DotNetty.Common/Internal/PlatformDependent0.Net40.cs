// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET40
namespace DotNetty.Common.Internal
{
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Utilities;

    static class PlatformDependent0
    {
        internal static readonly int HashCodeAsciiSeed = unchecked((int)0xc2b2ae35);
        internal static readonly int HashCodeC1 = unchecked((int)0xcc9e2d51);
        internal static readonly int HashCodeC2 = 0x1b873593;

        // https://stackoverflow.com/questions/43289/comparing-two-byte-arrays-in-net
        [MethodImpl(InlineMethod.Value)]
        internal static unsafe bool ByteArrayEquals(byte* bytes1, int startPos1, byte* bytes2, int startPos2, int length)
        {
            if (length <= 0) { return true; }

            byte* baseOffset1 = bytes1 + startPos1;
            byte* baseOffset2 = bytes2 + startPos2;

            for (int i = 0; i < length / 8; i++, baseOffset1 += 8, baseOffset2 += 8)
                if (*((long*)baseOffset1) != *((long*)baseOffset2)) return false;
            if ((length & 4) != 0) { if (*((int*)baseOffset1) != *((int*)baseOffset2)) return false; baseOffset1 += 4; baseOffset2 += 4; }
            if ((length & 2) != 0) { if (*((short*)baseOffset1) != *((short*)baseOffset2)) return false; baseOffset1 += 2; baseOffset2 += 2; }
            if ((length & 1) != 0) if (*((byte*)baseOffset1) != *((byte*)baseOffset2)) return false;
            return true;
        }

        [MethodImpl(InlineMethod.Value)]
        internal static unsafe int HashCodeAscii(byte* bytes, int length)
        {
            int hash = HashCodeAsciiSeed;
            int remainingBytes = length & 7;
            byte* end = bytes + remainingBytes;
            for (byte* i = bytes - 8 + length; i >= end; i -= 8)
            {
                hash = HashCodeAsciiCompute(*((long*)i), hash);
            }

            switch (remainingBytes)
            {
                case 7:
                    return ((hash * HashCodeC1 + HashCodeAsciiSanitize(*bytes))
                        * HashCodeC2 + HashCodeAsciiSanitize(*((short*)(bytes + 1))))
                        * HashCodeC1 + HashCodeAsciiSanitize(*((int*)(bytes + 3)));
                case 6:
                    return (hash * HashCodeC1 + HashCodeAsciiSanitize(*((short*)bytes)))
                        * HashCodeC2 + HashCodeAsciiSanitize(*((int*)(bytes + 2)));
                case 5:
                    return (hash * HashCodeC1 + HashCodeAsciiSanitize(*bytes))
                        * HashCodeC2 + HashCodeAsciiSanitize(*((int*)(bytes + 1)));
                case 4:
                    return hash * HashCodeC1 + HashCodeAsciiSanitize(*((int*)bytes));
                case 3:
                    return (hash * HashCodeC1 + HashCodeAsciiSanitize(*bytes))
                        * HashCodeC2 + HashCodeAsciiSanitize(*((short*)(bytes + 1)));
                case 2:
                    return hash * HashCodeC1 + HashCodeAsciiSanitize(*((short*)bytes));
                case 1:
                    return hash * HashCodeC1 + HashCodeAsciiSanitize(*bytes);
                default:
                    return hash;
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static int HashCodeAsciiCompute(long value, int hash)
        {
            // masking with 0x1f reduces the number of overall bits that impact the hash code but makes the hash
            // code the same regardless of character case (upper case or lower case hash is the same).
            unchecked
            {
                return hash * HashCodeC1 +
                    // Low order int
                    HashCodeAsciiSanitize((int)value) * HashCodeC2 +
                    // High order int
                    (int)(value & 0x1f1f1f1f00000000L).RightUShift(32);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        static int HashCodeAsciiSanitize(int value) => value & 0x1f1f1f1f;

        [MethodImpl(InlineMethod.Value)]
        static int HashCodeAsciiSanitize(short value) => value & 0x1f1f;

        [MethodImpl(InlineMethod.Value)]
        static int HashCodeAsciiSanitize(byte value) => value & 0x1f;
    }
}
#endif
