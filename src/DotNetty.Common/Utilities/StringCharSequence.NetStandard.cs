// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Runtime.CompilerServices;

    partial class StringCharSequence : IHasUtf16Span
    {
        public ReadOnlySpan<char> Utf16Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var len = this.count;
                if (0u >= (uint)len) { return ReadOnlySpan<char>.Empty; }
                return this.value.AsSpan(this.offset, len);
            }
        }
    }
}

#endif
