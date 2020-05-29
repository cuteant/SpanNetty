// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Runtime.CompilerServices;

    partial class ByteBufferUtil
    {
        /// <summary>Returns the reader index of needle in haystack, or -1 if needle is not in haystack.</summary>
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static int IndexOf(IByteBuffer needle, IByteBuffer haystack)
        {
            return haystack.IndexOf(needle);
        }

        /// <summary>Returns the reader index of needle in haystack, or -1 if needle is not in haystack.</summary>
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static int IndexOf(in ReadOnlySpan<byte> needle, IByteBuffer haystack)
        {
            return haystack.IndexOf(needle);
        }
    }
}
