// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// borrowed from https://github.com/dotnet/corefxlab/blob/e075d78df60452b68d212e3333fd3f37cd28d4f0/src/System.Buffers.ReaderWriter/System/Buffers/Reader/BufferReader.cs#L38

#if !NET40

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Utilities;

    internal static class ByteBufferReaderHelper
    {
        private const int FlagBitMask = 1 << 31;
        private const int IndexBitMask = ~FlagBitMask;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void GetFirstSpan(in ReadOnlySequence<byte> buffer, out ReadOnlySpan<byte> first, out SequencePosition next)
        {
            first = default;
            next = default;
            SequencePosition start = buffer.Start;
            int startIndex = start.GetInteger();
            object startObject = start.GetObject();

            if (startObject != null)
            {
                SequencePosition end = buffer.End;
                int endIndex = end.GetInteger();
                bool isMultiSegment = startObject != end.GetObject();

                // A == 0 && B == 0 means SequenceType.MultiSegment
                if (startIndex >= 0)
                {
                    if (endIndex >= 0)  // SequenceType.MultiSegment
                    {
                        ReadOnlySequenceSegment<byte> segment = (ReadOnlySequenceSegment<byte>)startObject;
                        next = new SequencePosition(segment.Next, 0);
                        first = segment.Memory.Span;
                        if (isMultiSegment)
                        {
                            first = first.Slice(startIndex);
                        }
                        else
                        {
                            first = first.Slice(startIndex, endIndex - startIndex);
                        }
                    }
                    else
                    {
                        if (isMultiSegment) { ThrowHelper.ThrowInvalidOperationException_EndPositionNotReached(); }

                        first = new ReadOnlySpan<byte>((byte[])startObject, startIndex, (endIndex & IndexBitMask) - startIndex);
                    }
                }
                else
                {
                    first = GetFirstSpanSlow(startObject, startIndex, endIndex, isMultiSegment);
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ReadOnlySpan<byte> GetFirstSpanSlow(object startObject, int startIndex, int endIndex, bool isMultiSegment)
        {
            Debug.Assert(startIndex < 0 || endIndex < 0);
            if (isMultiSegment) { ThrowHelper.ThrowInvalidOperationException_EndPositionNotReached(); }

            // The type == char check here is redundant. However, we still have it to allow
            // the JIT to see when that the code is unreachable and eliminate it.
            // A == 1 && B == 1 means SequenceType.String
            //if (typeof(T) == typeof(char) && endIndex < 0)
            //{
            //    var memory = (ReadOnlyMemory<T>)(object)((string)startObject).AsMemory();

            //    // No need to remove the FlagBitMask since (endIndex - startIndex) == (endIndex & ReadOnlySequence.IndexBitMask) - (startIndex & ReadOnlySequence.IndexBitMask)
            //    return memory.Span.Slice(startIndex & IndexBitMask, endIndex - startIndex);
            //}
            //else // endIndex >= 0, A == 1 && B == 0 means SequenceType.MemoryManager
            {
                startIndex &= IndexBitMask;
                return ((MemoryManager<byte>)startObject).Memory.Span.Slice(startIndex, endIndex - startIndex);
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe int PastAny(ref byte searchSpace, int searchSpaceLength, ref byte value, int valueLength)
        {
            Debug.Assert(searchSpaceLength >= 0);
            Debug.Assert(valueLength >= 0);

            if (0u >= (uint)valueLength)
                return 0;  // A zero-length sequence is always treated as "found" at the start of the search space.

            uint uValue = value; // Use uint for comparisons to avoid unnecessary 8->32 extensions
            IntPtr offset = (IntPtr)0; // Use IntPtr for arithmetic to avoid unnecessary 64->32->64 truncations
            IntPtr lengthToExamine = (IntPtr)searchSpaceLength;

            uint lookUp;
            while ((byte*)lengthToExamine >= (byte*)8)
            {
                lengthToExamine -= 8;

                lookUp = Unsafe.AddByteOffset(ref searchSpace, offset);
                if (!IndexOfAny(lookUp, ref value, valueLength))
                    goto Found;
                lookUp = Unsafe.AddByteOffset(ref searchSpace, offset + 1);
                if (!IndexOfAny(lookUp, ref value, valueLength))
                    goto Found1;
                lookUp = Unsafe.AddByteOffset(ref searchSpace, offset + 2);
                if (!IndexOfAny(lookUp, ref value, valueLength))
                    goto Found2;
                lookUp = Unsafe.AddByteOffset(ref searchSpace, offset + 3);
                if (!IndexOfAny(lookUp, ref value, valueLength))
                    goto Found3;
                lookUp = Unsafe.AddByteOffset(ref searchSpace, offset + 4);
                if (!IndexOfAny(lookUp, ref value, valueLength))
                    goto Found4;
                lookUp = Unsafe.AddByteOffset(ref searchSpace, offset + 5);
                if (!IndexOfAny(lookUp, ref value, valueLength))
                    goto Found5;
                lookUp = Unsafe.AddByteOffset(ref searchSpace, offset + 6);
                if (!IndexOfAny(lookUp, ref value, valueLength))
                    goto Found6;
                lookUp = Unsafe.AddByteOffset(ref searchSpace, offset + 7);
                if (!IndexOfAny(lookUp, ref value, valueLength))
                    goto Found7;

                offset += 8;
            }

            if ((byte*)lengthToExamine >= (byte*)4)
            {
                lengthToExamine -= 4;

                lookUp = Unsafe.AddByteOffset(ref searchSpace, offset);
                if (!IndexOfAny(lookUp, ref value, valueLength))
                    goto Found;
                lookUp = Unsafe.AddByteOffset(ref searchSpace, offset + 1);
                if (!IndexOfAny(lookUp, ref value, valueLength))
                    goto Found1;
                lookUp = Unsafe.AddByteOffset(ref searchSpace, offset + 2);
                if (!IndexOfAny(lookUp, ref value, valueLength))
                    goto Found2;
                lookUp = Unsafe.AddByteOffset(ref searchSpace, offset + 3);
                if (!IndexOfAny(lookUp, ref value, valueLength))
                    goto Found3;

                offset += 4;
            }

            while ((byte*)lengthToExamine > (byte*)0)
            {
                lengthToExamine -= 1;

                lookUp = Unsafe.AddByteOffset(ref searchSpace, offset);
                if (!IndexOfAny(lookUp, ref value, valueLength))
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IndexOfAny(uint lookUp, ref byte value, int valueLength)
        {
            for (int i = 0; i < valueLength; i++)
            {
                if (lookUp == Unsafe.Add(ref value, i))
                {
                    return true;
                }
            }
            return false;
        }

        internal readonly struct IndexNotOfProcessor : IByteProcessor
        {
            readonly uint byteToNotFind; // Use uint for comparisons to avoid unnecessary 8->32 extensions

            public IndexNotOfProcessor(byte byteToNotFind)
            {
                this.byteToNotFind = byteToNotFind;
            }

            public bool Process(byte value) => value == this.byteToNotFind;
        }

        internal readonly struct PastValueProcessor : IByteProcessor
        {
            private readonly uint _value;

            public PastValueProcessor(byte value)
            {
                _value = value;
            }

            public bool Process(byte value)
            {
                return _value == value;
            }
        }

        internal readonly struct PastValue2Processor : IByteProcessor
        {
            private readonly uint _value0;
            private readonly uint _value1;

            public PastValue2Processor(byte value0, byte value1)
            {
                _value0 = value0;
                _value1 = value1;
            }

            public bool Process(byte value)
            {
                uint nValue = value;
                return _value0 == nValue || _value1 == nValue;
            }
        }

        internal readonly struct PastValue3Processor : IByteProcessor
        {
            private readonly uint _value0;
            private readonly uint _value1;
            private readonly uint _value2;

            public PastValue3Processor(byte value0, byte value1, byte value2)
            {
                _value0 = value0;
                _value1 = value1;
                _value2 = value2;
            }

            public bool Process(byte value)
            {
                uint nValue = value;
                return _value0 == nValue || _value1 == nValue || _value2 == nValue;
            }
        }

        internal readonly struct PastValue4Processor : IByteProcessor
        {
            private readonly uint _value0;
            private readonly uint _value1;
            private readonly uint _value2;
            private readonly uint _value3;

            public PastValue4Processor(byte value0, byte value1, byte value2, byte value3)
            {
                _value0 = value0;
                _value1 = value1;
                _value2 = value2;
                _value3 = value3;
            }

            public bool Process(byte value)
            {
                uint nValue = value;
                return _value0 == nValue || _value1 == nValue || _value2 == nValue || _value3 == nValue;
            }
        }
    }
}

#endif