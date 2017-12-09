// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#if NET40
namespace DotNetty.Common.Internal
{
    using System.Runtime.CompilerServices;

    static class PlatformDependent0
    {
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
    }
}
#endif
