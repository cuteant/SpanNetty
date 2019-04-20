// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40

namespace DotNetty.Buffers
{
    using System;
    using System.Runtime.CompilerServices;

    static unsafe partial class UnsafeByteBufferUtil
    {
        [MethodImpl(InlineMethod.Value)]
        internal static short GetShort(ref byte source, int srcIndex)
        {
            IntPtr offset = (IntPtr)srcIndex;

            return unchecked((short)((Unsafe.AddByteOffset(ref source, offset) << 8) | Unsafe.AddByteOffset(ref source, offset + 1)));
        }

        [MethodImpl(InlineMethod.Value)]
        internal static short GetShortLE(ref byte source, int srcIndex)
        {
            IntPtr offset = (IntPtr)srcIndex;

            return unchecked((short)(Unsafe.AddByteOffset(ref source, offset) | (Unsafe.AddByteOffset(ref source, offset + 1) << 8)));
        }

        [MethodImpl(InlineMethod.Value)]
        internal static int GetUnsignedMedium(ref byte source, int srcIndex)
        {
            IntPtr offset = (IntPtr)srcIndex;

            return
                Unsafe.AddByteOffset(ref source, offset) << 16 |
                Unsafe.AddByteOffset(ref source, offset + 1) << 8 |
                Unsafe.AddByteOffset(ref source, offset + 2);
        }

        [MethodImpl(InlineMethod.Value)]
        internal static int GetUnsignedMediumLE(ref byte source, int srcIndex)
        {
            IntPtr offset = (IntPtr)srcIndex;

            return
                Unsafe.AddByteOffset(ref source, offset) |
                Unsafe.AddByteOffset(ref source, offset + 1) << 8 |
                Unsafe.AddByteOffset(ref source, offset + 2) << 16;
        }

        [MethodImpl(InlineMethod.Value)]
        internal static int GetInt(ref byte source, int srcIndex)
        {
            IntPtr offset = (IntPtr)srcIndex;

            return
                (Unsafe.AddByteOffset(ref source, offset) << 24) |
                (Unsafe.AddByteOffset(ref source, offset + 1) << 16) |
                (Unsafe.AddByteOffset(ref source, offset + 2) << 8) |
                Unsafe.AddByteOffset(ref source, offset + 3);
        }

        [MethodImpl(InlineMethod.Value)]
        internal static int GetIntLE(ref byte source, int srcIndex)
        {
            IntPtr offset = (IntPtr)srcIndex;

            return
                Unsafe.AddByteOffset(ref source, offset) |
                (Unsafe.AddByteOffset(ref source, offset + 1) << 8) |
                (Unsafe.AddByteOffset(ref source, offset + 2) << 16) |
                (Unsafe.AddByteOffset(ref source, offset + 3) << 24);
        }

        [MethodImpl(InlineMethod.Value)]
        internal static long GetLong(ref byte source, int srcIndex)
        {
            unchecked
            {
                IntPtr offset = (IntPtr)srcIndex;

                int i1 =
                    (Unsafe.AddByteOffset(ref source, offset) << 24) |
                    (Unsafe.AddByteOffset(ref source, offset + 1) << 16) |
                    (Unsafe.AddByteOffset(ref source, offset + 2) << 8) |
                    Unsafe.AddByteOffset(ref source, offset + 3);
                int i2 =
                    (Unsafe.AddByteOffset(ref source, offset + 4) << 24) |
                    (Unsafe.AddByteOffset(ref source, offset + 5) << 16) |
                    (Unsafe.AddByteOffset(ref source, offset + 6) << 8) |
                    Unsafe.AddByteOffset(ref source, offset + 7);
                return (uint)i2 | ((long)i1 << 32);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static long GetLongLE(ref byte source, int srcIndex)
        {
            unchecked
            {
                IntPtr offset = (IntPtr)srcIndex;

                int i1 =
                    Unsafe.AddByteOffset(ref source, offset) |
                    (Unsafe.AddByteOffset(ref source, offset + 1) << 8) |
                    (Unsafe.AddByteOffset(ref source, offset + 2) << 16) |
                    (Unsafe.AddByteOffset(ref source, offset + 3) << 24);
                int i2 =
                    Unsafe.AddByteOffset(ref source, offset + 4) |
                    (Unsafe.AddByteOffset(ref source, offset + 5) << 8) |
                    (Unsafe.AddByteOffset(ref source, offset + 6) << 16) |
                    (Unsafe.AddByteOffset(ref source, offset + 7) << 24);
                return (uint)i1 | ((long)i2 << 32);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetShort(ref byte destination, int dstIndex, int value)
        {
            unchecked
            {
                IntPtr offset = (IntPtr)dstIndex;

                Unsafe.AddByteOffset(ref destination, offset) = (byte)((ushort)value >> 8);
                Unsafe.AddByteOffset(ref destination, offset + 1) = (byte)value;
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetShortLE(ref byte destination, int dstIndex, int value)
        {
            unchecked
            {
                IntPtr offset = (IntPtr)dstIndex;

                Unsafe.AddByteOffset(ref destination, offset) = (byte)value;
                Unsafe.AddByteOffset(ref destination, offset + 1) = (byte)((ushort)value >> 8);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetMedium(ref byte destination, int dstIndex, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                IntPtr offset = (IntPtr)dstIndex;

                Unsafe.AddByteOffset(ref destination, offset) = (byte)(unsignedValue >> 16);
                Unsafe.AddByteOffset(ref destination, offset + 1) = (byte)(unsignedValue >> 8);
                Unsafe.AddByteOffset(ref destination, offset + 2) = (byte)unsignedValue;
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetMediumLE(ref byte destination, int dstIndex, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                IntPtr offset = (IntPtr)dstIndex;

                Unsafe.AddByteOffset(ref destination, offset) = (byte)unsignedValue;
                Unsafe.AddByteOffset(ref destination, offset + 1) = (byte)(unsignedValue >> 8);
                Unsafe.AddByteOffset(ref destination, offset + 2) = (byte)(unsignedValue >> 16);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetInt(ref byte destination, int dstIndex, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                IntPtr offset = (IntPtr)dstIndex;

                Unsafe.AddByteOffset(ref destination, offset) = (byte)(unsignedValue >> 24);
                Unsafe.AddByteOffset(ref destination, offset + 1) = (byte)(unsignedValue >> 16);
                Unsafe.AddByteOffset(ref destination, offset + 2) = (byte)(unsignedValue >> 8);
                Unsafe.AddByteOffset(ref destination, offset + 3) = (byte)unsignedValue;
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetIntLE(ref byte destination, int dstIndex, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                IntPtr offset = (IntPtr)dstIndex;

                Unsafe.AddByteOffset(ref destination, offset) = (byte)unsignedValue;
                Unsafe.AddByteOffset(ref destination, offset + 1) = (byte)(unsignedValue >> 8);
                Unsafe.AddByteOffset(ref destination, offset + 2) = (byte)(unsignedValue >> 16);
                Unsafe.AddByteOffset(ref destination, offset + 3) = (byte)(unsignedValue >> 24);
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetLong(ref byte destination, int dstIndex, long value)
        {
            unchecked
            {
                ulong unsignedValue = (ulong)value;
                IntPtr offset = (IntPtr)dstIndex;

                Unsafe.AddByteOffset(ref destination, offset) = (byte)(unsignedValue >> 56);
                Unsafe.AddByteOffset(ref destination, offset + 1) = (byte)(unsignedValue >> 48);
                Unsafe.AddByteOffset(ref destination, offset + 2) = (byte)(unsignedValue >> 40);
                Unsafe.AddByteOffset(ref destination, offset + 3) = (byte)(unsignedValue >> 32);
                Unsafe.AddByteOffset(ref destination, offset + 4) = (byte)(unsignedValue >> 24);
                Unsafe.AddByteOffset(ref destination, offset + 5) = (byte)(unsignedValue >> 16);
                Unsafe.AddByteOffset(ref destination, offset + 6) = (byte)(unsignedValue >> 8);
                Unsafe.AddByteOffset(ref destination, offset + 7) = (byte)unsignedValue;
            }
        }

        [MethodImpl(InlineMethod.Value)]
        internal static void SetLongLE(ref byte destination, int dstIndex, long value)
        {
            unchecked
            {
                ulong unsignedValue = (ulong)value;
                IntPtr offset = (IntPtr)dstIndex;

                Unsafe.AddByteOffset(ref destination, offset) = (byte)unsignedValue;
                Unsafe.AddByteOffset(ref destination, offset + 1) = (byte)(unsignedValue >> 8);
                Unsafe.AddByteOffset(ref destination, offset + 2) = (byte)(unsignedValue >> 16);
                Unsafe.AddByteOffset(ref destination, offset + 3) = (byte)(unsignedValue >> 24);
                Unsafe.AddByteOffset(ref destination, offset + 4) = (byte)(unsignedValue >> 32);
                Unsafe.AddByteOffset(ref destination, offset + 5) = (byte)(unsignedValue >> 40);
                Unsafe.AddByteOffset(ref destination, offset + 6) = (byte)(unsignedValue >> 48);
                Unsafe.AddByteOffset(ref destination, offset + 7) = (byte)(unsignedValue >> 56);
            }
        }
    }
}

#endif