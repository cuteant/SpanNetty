// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET40

namespace DotNetty.Buffers
{
    using System.Runtime.CompilerServices;

    static unsafe partial class UnsafeByteBufferUtil
    {
        [MethodImpl(InlineMethod.Value)]
        internal static short GetShort(byte* bytes) =>
            unchecked((short)(((*bytes) << 8) | *(bytes + 1)));

        [MethodImpl(InlineMethod.Value)]
        internal static short GetShortLE(byte* bytes) =>
            unchecked((short)((*bytes) | (*(bytes + 1) << 8)));

        [MethodImpl(InlineMethod.Value)]
        internal static int GetUnsignedMedium(byte* bytes) =>
            *bytes << 16 |
            *(bytes + 1) << 8 |
            *(bytes + 2);

        [MethodImpl(InlineMethod.Value)]
        internal static int GetUnsignedMediumLE(byte* bytes) =>
            *bytes |
            *(bytes + 1) << 8 |
            *(bytes + 2) << 16;

        [MethodImpl(InlineMethod.Value)]
        internal static int GetInt(byte* bytes) =>
            (*bytes << 24) |
            (*(bytes + 1) << 16) |
            (*(bytes + 2) << 8) |
            (*(bytes + 3));

        [MethodImpl(InlineMethod.Value)]
        internal static int GetIntLE(byte* bytes) =>
            *bytes |
            (*(bytes + 1) << 8) |
            (*(bytes + 2) << 16) |
            (*(bytes + 3) << 24);

        [MethodImpl(InlineMethod.Value)]
        internal static long GetLong(byte* bytes)
        {
            unchecked
            {
                int i1 = (*bytes << 24) | (*(bytes + 1) << 16) | (*(bytes + 2) << 8) | (*(bytes + 3));
                int i2 = (*(bytes + 4) << 24) | (*(bytes + 5) << 16) | (*(bytes + 6) << 8) | *(bytes + 7);
                return (uint)i2 | ((long)i1 << 32);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static long GetLongLE(byte* bytes)
        {
            unchecked
            {
                int i1 = *bytes | (*(bytes + 1) << 8) | (*(bytes + 2) << 16) | (*(bytes + 3) << 24);
                int i2 = *(bytes + 4) | (*(bytes + 5) << 8) | (*(bytes + 6) << 16) | (*(bytes + 7) << 24);
                return (uint)i1 | ((long)i2 << 32);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetShort(byte* bytes, int value)
        {
            unchecked
            {
                *bytes = (byte)((ushort)value >> 8);
                *(bytes + 1) = (byte)value;
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetShortLE(byte* bytes, int value)
        {
            unchecked
            {
                *bytes = (byte)value;
                *(bytes + 1) = (byte)((ushort)value >> 8);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetMedium(byte* bytes, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                *bytes = (byte)(unsignedValue >> 16);
                *(bytes + 1) = (byte)(unsignedValue >> 8);
                *(bytes + 2) = (byte)unsignedValue;
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetMediumLE(byte* bytes, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                *bytes = (byte)unsignedValue;
                *(bytes + 1) = (byte)(unsignedValue >> 8);
                *(bytes + 2) = (byte)(unsignedValue >> 16);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetInt(byte* bytes, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                *bytes = (byte)(unsignedValue >> 24);
                *(bytes + 1) = (byte)(unsignedValue >> 16);
                *(bytes + 2) = (byte)(unsignedValue >> 8);
                *(bytes + 3) = (byte)unsignedValue;
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetIntLE(byte* bytes, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                *bytes = (byte)unsignedValue;
                *(bytes + 1) = (byte)(unsignedValue >> 8);
                *(bytes + 2) = (byte)(unsignedValue >> 16);
                *(bytes + 3) = (byte)(unsignedValue >> 24);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetLong(byte* bytes, long value)
        {
            unchecked
            {
                ulong unsignedValue = (ulong)value;
                *bytes = (byte)(unsignedValue >> 56);
                *(bytes + 1) = (byte)(unsignedValue >> 48);
                *(bytes + 2) = (byte)(unsignedValue >> 40);
                *(bytes + 3) = (byte)(unsignedValue >> 32);
                *(bytes + 4) = (byte)(unsignedValue >> 24);
                *(bytes + 5) = (byte)(unsignedValue >> 16);
                *(bytes + 6) = (byte)(unsignedValue >> 8);
                *(bytes + 7) = (byte)unsignedValue;
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetLongLE(byte* bytes, long value)
        {
            unchecked
            {
                ulong unsignedValue = (ulong)value;
                *bytes = (byte)unsignedValue;
                *(bytes + 1) = (byte)(unsignedValue >> 8);
                *(bytes + 2) = (byte)(unsignedValue >> 16);
                *(bytes + 3) = (byte)(unsignedValue >> 24);
                *(bytes + 4) = (byte)(unsignedValue >> 32);
                *(bytes + 5) = (byte)(unsignedValue >> 40);
                *(bytes + 6) = (byte)(unsignedValue >> 48);
                *(bytes + 7) = (byte)(unsignedValue >> 56);
            }
        }
    }
}

#endif