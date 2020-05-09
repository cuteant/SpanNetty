#if !NET40

namespace DotNetty.Buffers
{
    using System;
    using System.Runtime.CompilerServices;

    public ref partial struct ByteBufferWriter
    {
        const int DecimalValueLength = 16;
        const int GuidValueLength = 16;
        const int Int16ValueLength = 2;
        const int Int24ValueLength = 3;
        const int Int32ValueLength = 4;
        const int Int64ValueLength = 8;

        [MethodImpl(InlineMethod.Value)]
        private unsafe static void SetMedium(ref byte start, int value)
        {
            //fixed(byte* bytes = &start)
            //{
            //    UnsafeByteBufferUtil.SetMedium(bytes, value);
            //}
            uint unsignedValue = (uint)value;
            IntPtr offset = (IntPtr)0;
            Unsafe.AddByteOffset(ref start, offset) = (byte)(unsignedValue >> 16);
            Unsafe.AddByteOffset(ref start, offset + 1) = (byte)(unsignedValue >> 8);
            Unsafe.AddByteOffset(ref start, offset + 2) = (byte)unsignedValue;
        }

        [MethodImpl(InlineMethod.Value)]
        private unsafe static void SetMediumLE(ref byte start, int value)
        {
            uint unsignedValue = (uint)value;
            IntPtr offset = (IntPtr)0;
            Unsafe.AddByteOffset(ref start, offset) = (byte)unsignedValue;
            Unsafe.AddByteOffset(ref start, offset + 1) = (byte)(unsignedValue >> 8);
            Unsafe.AddByteOffset(ref start, offset + 2) = (byte)(unsignedValue >> 16);
        }

        [MethodImpl(InlineMethod.Value)]
        private unsafe static void SetDecimal(ref byte start, decimal value)
        {
            var bits = decimal.GetBits(value);
            uint lo = (uint)bits[0];
            uint mid = (uint)bits[1];
            uint high = (uint)bits[2];
            uint flags = (uint)bits[3];
            IntPtr offset = (IntPtr)0;

            Unsafe.AddByteOffset(ref start, offset) = (byte)(lo >> 24); // lo
            Unsafe.AddByteOffset(ref start, offset + 1) = (byte)(lo >> 16);
            Unsafe.AddByteOffset(ref start, offset + 2) = (byte)(lo >> 8);
            Unsafe.AddByteOffset(ref start, offset + 3) = (byte)lo;
            Unsafe.AddByteOffset(ref start, offset + 4) = (byte)(mid >> 24); // mid
            Unsafe.AddByteOffset(ref start, offset + 5) = (byte)(mid >> 16);
            Unsafe.AddByteOffset(ref start, offset + 6) = (byte)(mid >> 8);
            Unsafe.AddByteOffset(ref start, offset + 7) = (byte)mid;
            Unsafe.AddByteOffset(ref start, offset + 8) = (byte)(high >> 24); // high
            Unsafe.AddByteOffset(ref start, offset + 9) = (byte)(high >> 16);
            Unsafe.AddByteOffset(ref start, offset + 10) = (byte)(high >> 8);
            Unsafe.AddByteOffset(ref start, offset + 11) = (byte)high;
            Unsafe.AddByteOffset(ref start, offset + 12) = (byte)(flags >> 24); // flags
            Unsafe.AddByteOffset(ref start, offset + 13) = (byte)(flags >> 16);
            Unsafe.AddByteOffset(ref start, offset + 14) = (byte)(flags >> 8);
            Unsafe.AddByteOffset(ref start, offset + 15) = (byte)flags;
        }

        [MethodImpl(InlineMethod.Value)]
        private unsafe static void SetDecimalLE(ref byte start, decimal value)
        {
            var bits = decimal.GetBits(value);
            uint lo = (uint)bits[0];
            uint mid = (uint)bits[1];
            uint high = (uint)bits[2];
            uint flags = (uint)bits[3];
            IntPtr offset = (IntPtr)0;

            Unsafe.AddByteOffset(ref start, offset) = (byte)lo;
            Unsafe.AddByteOffset(ref start, offset + 1) = (byte)(lo >> 8);
            Unsafe.AddByteOffset(ref start, offset + 2) = (byte)(lo >> 16);
            Unsafe.AddByteOffset(ref start, offset + 3) = (byte)(lo >> 24); // lo
            Unsafe.AddByteOffset(ref start, offset + 4) = (byte)mid;
            Unsafe.AddByteOffset(ref start, offset + 5) = (byte)(mid >> 8);
            Unsafe.AddByteOffset(ref start, offset + 6) = (byte)(mid >> 16);
            Unsafe.AddByteOffset(ref start, offset + 7) = (byte)(mid >> 24); // mid
            Unsafe.AddByteOffset(ref start, offset + 8) = (byte)high;
            Unsafe.AddByteOffset(ref start, offset + 9) = (byte)(high >> 8);
            Unsafe.AddByteOffset(ref start, offset + 10) = (byte)(high >> 16);
            Unsafe.AddByteOffset(ref start, offset + 11) = (byte)(high >> 24); // high
            Unsafe.AddByteOffset(ref start, offset + 12) = (byte)flags;
            Unsafe.AddByteOffset(ref start, offset + 13) = (byte)(flags >> 8);
            Unsafe.AddByteOffset(ref start, offset + 14) = (byte)(flags >> 16);
            Unsafe.AddByteOffset(ref start, offset + 15) = (byte)(flags >> 24); // flags
        }

        /// <summary>Writes a 32-bit integer in a compressed format.</summary>
        /// <param name="value">The 32-bit integer to be written.</param>
        /// <param name="idx"></param>
        private void Write7BitEncodedInt0(int value, ref int idx)
        {
            const int Maximum7BitEncodedIntLength = 5;
            AdvanceAndGrowIf(ref idx, Maximum7BitEncodedIntLength);

            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint)value;   // support negative numbers
            while (v >= 0x80u)
            {
                _buffer[idx++] = ((byte)(v | 0x80u));
                v >>= 7;
            }
            _buffer[idx++] = ((byte)v);
        }
    }
}

#endif