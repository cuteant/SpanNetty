// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System.Runtime.CompilerServices;

    public static class BitOps
    {
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static int RightUShift(this int value, int bits) => unchecked((int)((uint)value >> bits));

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static uint RightShift2U(this int value, int bits) => (uint)value >> bits;

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static long RightUShift(this long value, int bits) => unchecked((long)((ulong)value >> bits));

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static ulong RightShift2U(this long value, int bits) => (ulong)value >> bits;
    }
}