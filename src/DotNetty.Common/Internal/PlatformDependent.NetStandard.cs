// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40

namespace DotNetty.Common.Internal
{
    using System;
    using System.Diagnostics;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Utilities;

    partial class PlatformDependent
    {
        public static bool IsX64 { get; } = IntPtr.Size >= 8;

        // Adapted from IndexOf(...)
        //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe bool Contains(ref byte searchSpace, byte value, int length)
        {
            Debug.Assert(length >= 0);

            uint uValue = value; // Use uint for comparisons to avoid unnecessary 8->32 extensions
            IntPtr offset = (IntPtr)0; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
            IntPtr lengthToExamine = (IntPtr)length;

            if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
            {
                lengthToExamine = UnalignedCountVector(ref searchSpace);
            }

        SequentialScan:
            while ((byte*)lengthToExamine >= (byte*)8)
            {
                lengthToExamine -= 8;

                if (uValue == Unsafe.AddByteOffset(ref searchSpace, offset + 0) ||
                    uValue == Unsafe.AddByteOffset(ref searchSpace, offset + 1) ||
                    uValue == Unsafe.AddByteOffset(ref searchSpace, offset + 2) ||
                    uValue == Unsafe.AddByteOffset(ref searchSpace, offset + 3) ||
                    uValue == Unsafe.AddByteOffset(ref searchSpace, offset + 4) ||
                    uValue == Unsafe.AddByteOffset(ref searchSpace, offset + 5) ||
                    uValue == Unsafe.AddByteOffset(ref searchSpace, offset + 6) ||
                    uValue == Unsafe.AddByteOffset(ref searchSpace, offset + 7))
                {
                    goto Found;
                }

                offset += 8;
            }

            if ((byte*)lengthToExamine >= (byte*)4)
            {
                lengthToExamine -= 4;

                if (uValue == Unsafe.AddByteOffset(ref searchSpace, offset + 0) ||
                    uValue == Unsafe.AddByteOffset(ref searchSpace, offset + 1) ||
                    uValue == Unsafe.AddByteOffset(ref searchSpace, offset + 2) ||
                    uValue == Unsafe.AddByteOffset(ref searchSpace, offset + 3))
                {
                    goto Found;
                }

                offset += 4;
            }

            while ((byte*)lengthToExamine > (byte*)0)
            {
                lengthToExamine -= 1;

                if (uValue == Unsafe.AddByteOffset(ref searchSpace, offset))
                    goto Found;

                offset += 1;
            }

            if (Vector.IsHardwareAccelerated && ((int)(byte*)offset < length))
            {
                lengthToExamine = (IntPtr)((length - (int)(byte*)offset) & ~(Vector<byte>.Count - 1));

                Vector<byte> values = GetVector(value);

                while ((byte*)lengthToExamine > (byte*)offset)
                {
                    var matches = Vector.Equals(values, LoadVector(ref searchSpace, offset));
                    if (Vector<byte>.Zero.Equals(matches))
                    {
                        offset += Vector<byte>.Count;
                        continue;
                    }

                    goto Found;
                }

                if ((int)(byte*)offset < length)
                {
                    lengthToExamine = (IntPtr)(length - (int)(byte*)offset);
                    goto SequentialScan;
                }
            }

            return false;

        Found:
            return true;
        }

        public static unsafe int FindIndex(ref byte searchSpace, Predicate<byte> match, int length)
        {
            Debug.Assert(length >= 0);

            IntPtr offset = (IntPtr)0; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
            IntPtr lengthToExamine = (IntPtr)length;

            while ((byte*)lengthToExamine >= (byte*)8)
            {
                lengthToExamine -= 8;

                if (match(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 4)))
                    goto Found4;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 5)))
                    goto Found5;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 6)))
                    goto Found6;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 7)))
                    goto Found7;

                offset += 8;
            }

            if ((byte*)lengthToExamine >= (byte*)4)
            {
                lengthToExamine -= 4;

                if (match(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;

                offset += 4;
            }

            while ((byte*)lengthToExamine > (byte*)0)
            {
                lengthToExamine -= 1;

                if (match(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;

                offset += 1;
            }

            return -1;
        Found: // Workaround for https://github.com/dotnet/coreclr/issues/13549
            return (int)(byte*)offset;
        Found1:
            return (int)(byte*)(offset + 1);
        Found2:
            return (int)(byte*)(offset + 2);
        Found3:
            return (int)(byte*)(offset + 3);
        Found4:
            return (int)(byte*)(offset + 4);
        Found5:
            return (int)(byte*)(offset + 5);
        Found6:
            return (int)(byte*)(offset + 6);
        Found7:
            return (int)(byte*)(offset + 7);

        }

        public static unsafe int FindLastIndex(ref byte searchSpace, Predicate<byte> match, int length)
        {
            Debug.Assert(length >= 0);

            IntPtr offset = (IntPtr)length; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
            IntPtr lengthToExamine = (IntPtr)length;

            while ((byte*)lengthToExamine >= (byte*)8)
            {
                lengthToExamine -= 8;
                offset -= 8;

                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 7)))
                    goto Found7;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 6)))
                    goto Found6;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 5)))
                    goto Found5;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 4)))
                    goto Found4;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
            }

            if ((byte*)lengthToExamine >= (byte*)4)
            {
                lengthToExamine -= 4;
                offset -= 4;

                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (match(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
            }

            while ((byte*)lengthToExamine > (byte*)0)
            {
                lengthToExamine -= 1;
                offset -= 1;

                if (match(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
            }

            return -1;
        Found: // Workaround for https://github.com/dotnet/coreclr/issues/13549
            return (int)(byte*)offset;
        Found1:
            return (int)(byte*)(offset + 1);
        Found2:
            return (int)(byte*)(offset + 2);
        Found3:
            return (int)(byte*)(offset + 3);
        Found4:
            return (int)(byte*)(offset + 4);
        Found5:
            return (int)(byte*)(offset + 5);
        Found6:
            return (int)(byte*)(offset + 6);
        Found7:
            return (int)(byte*)(offset + 7);
        }

        public static unsafe int ForEachByte(ref byte searchSpace, IByteProcessor processor, int length)
        {
            Debug.Assert(length >= 0);

            IntPtr offset = (IntPtr)0; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
            IntPtr lengthToExamine = (IntPtr)length;

            while ((byte*)lengthToExamine >= (byte*)8)
            {
                lengthToExamine -= 8;

                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 4)))
                    goto Found4;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 5)))
                    goto Found5;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 6)))
                    goto Found6;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 7)))
                    goto Found7;

                offset += 8;
            }

            if ((byte*)lengthToExamine >= (byte*)4)
            {
                lengthToExamine -= 4;

                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;

                offset += 4;
            }

            while ((byte*)lengthToExamine > (byte*)0)
            {
                lengthToExamine -= 1;

                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;

                offset += 1;
            }

            return -1;
        Found: // Workaround for https://github.com/dotnet/coreclr/issues/13549
            return (int)(byte*)offset;
        Found1:
            return (int)(byte*)(offset + 1);
        Found2:
            return (int)(byte*)(offset + 2);
        Found3:
            return (int)(byte*)(offset + 3);
        Found4:
            return (int)(byte*)(offset + 4);
        Found5:
            return (int)(byte*)(offset + 5);
        Found6:
            return (int)(byte*)(offset + 6);
        Found7:
            return (int)(byte*)(offset + 7);

        }

        public static unsafe int ForEachByteDesc(ref byte searchSpace, IByteProcessor processor, int length)
        {
            Debug.Assert(length >= 0);

            IntPtr offset = (IntPtr)length; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
            IntPtr lengthToExamine = (IntPtr)length;

            while ((byte*)lengthToExamine >= (byte*)8)
            {
                lengthToExamine -= 8;
                offset -= 8;

                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 7)))
                    goto Found7;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 6)))
                    goto Found6;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 5)))
                    goto Found5;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 4)))
                    goto Found4;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
            }

            if ((byte*)lengthToExamine >= (byte*)4)
            {
                lengthToExamine -= 4;
                offset -= 4;

                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 3)))
                    goto Found3;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 2)))
                    goto Found2;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset + 1)))
                    goto Found1;
                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
            }

            while ((byte*)lengthToExamine > (byte*)0)
            {
                lengthToExamine -= 1;
                offset -= 1;

                if (!processor.Process(Unsafe.AddByteOffset(ref searchSpace, offset)))
                    goto Found;
            }

            return -1;
        Found: // Workaround for https://github.com/dotnet/coreclr/issues/13549
            return (int)(byte*)offset;
        Found1:
            return (int)(byte*)(offset + 1);
        Found2:
            return (int)(byte*)(offset + 2);
        Found3:
            return (int)(byte*)(offset + 3);
        Found4:
            return (int)(byte*)(offset + 4);
        Found5:
            return (int)(byte*)(offset + 5);
        Found6:
            return (int)(byte*)(offset + 6);
        Found7:
            return (int)(byte*)(offset + 7);
        }

        // Vector sub-search adapted from https://github.com/aspnet/KestrelHttpServer/pull/1138
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int LocateFirstFoundByte(Vector<byte> match)
        {
            var vector64 = Vector.AsVectorUInt64(match);
            ulong candidate = 0;
            int i = 0;
            // Pattern unrolled by jit https://github.com/dotnet/coreclr/pull/8001
            for (; i < Vector<ulong>.Count; i++)
            {
                candidate = vector64[i];
                if (candidate != 0)
                {
                    break;
                }
            }

            // Single LEA instruction with jitted const (using function result)
            return i * 8 + LocateFirstFoundByte(candidate);
        }

        // Vector sub-search adapted from https://github.com/aspnet/KestrelHttpServer/pull/1138
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int LocateLastFoundByte(Vector<byte> match)
        {
            var vector64 = Vector.AsVectorUInt64(match);
            ulong candidate = 0;
            int i = Vector<ulong>.Count - 1;
            // Pattern unrolled by jit https://github.com/dotnet/coreclr/pull/8001
            for (; i >= 0; i--)
            {
                candidate = vector64[i];
                if (candidate != 0)
                {
                    break;
                }
            }

            // Single LEA instruction with jitted const (using function result)
            return i * 8 + LocateLastFoundByte(candidate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int LocateFirstFoundByte(ulong match)
        {
            // Flag least significant power of two bit
            var powerOfTwoFlag = match ^ (match - 1);
            // Shift all powers of two into the high byte and extract
            return (int)((powerOfTwoFlag * XorPowerOfTwoToHighByte) >> 57);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int LocateLastFoundByte(ulong match)
        {
            //return 7 - (BitOperations.LeadingZeroCount(match) >> 3);
            // Find the most significant byte that has its highest bit set
            int index = 7;
            while ((long)match > 0)
            {
                match = match << 8;
                index--;
            }
            return index;
        }

        private const ulong XorPowerOfTwoToHighByte = (0x07ul |
                                                       0x06ul << 8 |
                                                       0x05ul << 16 |
                                                       0x04ul << 24 |
                                                       0x03ul << 32 |
                                                       0x02ul << 40 |
                                                       0x01ul << 48) + 1;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector<byte> GetVector(byte vectorByte)
        {
#if !NETCOREAPP
            // Vector<byte> .ctor doesn't become an intrinsic due to detection issue
            // However this does cause it to become an intrinsic (with additional multiply and reg->reg copy)
            // https://github.com/dotnet/coreclr/issues/7459#issuecomment-253965670
            return Vector.AsVectorByte(new Vector<uint>(vectorByte * 0x01010101u));
#else
            return new Vector<byte>(vectorByte);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe UIntPtr LoadUIntPtr(ref byte start, IntPtr offset)
            => Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.AddByteOffset(ref start, offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe Vector<byte> LoadVector(ref byte start, IntPtr offset)
            => Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref start, offset));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe IntPtr GetByteVectorSpanLength(IntPtr offset, int length)
            => (IntPtr)((length - (int)(byte*)offset) & ~(Vector<byte>.Count - 1));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe IntPtr UnalignedCountVector(ref byte searchSpace)
        {
            int unaligned = (int)Unsafe.AsPointer(ref searchSpace) & (Vector<byte>.Count - 1);
            return (IntPtr)((Vector<byte>.Count - unaligned) & (Vector<byte>.Count - 1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe IntPtr UnalignedCountVectorFromEnd(ref byte searchSpace, int length)
        {
            int unaligned = (int)Unsafe.AsPointer(ref searchSpace) & (Vector<byte>.Count - 1);
            return (IntPtr)(((length & (Vector<byte>.Count - 1)) + unaligned) & (Vector<byte>.Count - 1));
        }
    }
}

#endif