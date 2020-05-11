// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System.Runtime.CompilerServices;

    public static class AsciiStringExtensions
    {
        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static byte[] ToByteArray(this AsciiString ascii) => ascii.ToByteArray(0, ascii.Count);

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static char[] ToCharArray(this AsciiString ascii) => ascii.ToCharArray(0, ascii.Count);

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static bool Contains(this AsciiString ascii, ICharSequence sequence) => (SharedConstants.TooBigOrNegative >= (uint)ascii.IndexOf(sequence)) ? true : false;

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static int IndexOf(this AsciiString ascii, ICharSequence sequence) => ascii.IndexOf(sequence, 0);

        // Use count instead of count - 1 so lastIndexOf("") answers count
        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static int LastIndexOf(this AsciiString ascii, ICharSequence charSequence) => ascii.LastIndexOf(charSequence, ascii.Count);

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static bool StartsWith(this AsciiString ascii, ICharSequence prefix)
            => ascii.StartsWith(prefix, 0) ? true : false;

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static bool StartsWith(this AsciiString ascii, ICharSequence prefix, int start)
            => ascii.RegionMatches(start, prefix, 0, prefix.Count) ? true : false;

        public static bool EndsWith(this AsciiString ascii, ICharSequence suffix)
        {
            int suffixLen = suffix.Count;
            return ascii.RegionMatches(ascii.Count - suffixLen, suffix, 0, suffixLen) ? true : false;
        }
    }
}
