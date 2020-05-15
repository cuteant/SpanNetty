namespace DotNetty.Buffers
{
    using System;
    using System.Buffers.Binary;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    public ref partial struct ByteBufferWriter
    {
        /// <summary>Write a UInt16 into the <see cref="IByteBuffer"/> of bytes as big endian.</summary>
        public void WriteUnsignedShort(ushort value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }

            GrowAndEnsureIf(Int16ValueLength);
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(_buffer), value);
            AdvanceCore(Int16ValueLength);
        }

        /// <summary>Write a UInt16 into the <see cref="IByteBuffer"/> of bytes as little endian.</summary>
        public void WriteUnsignedShortLE(ushort value)
        {
            if (!BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }

            GrowAndEnsureIf(Int16ValueLength);
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(_buffer), value);
            AdvanceCore(Int16ValueLength);
        }

        /// <summary>Write a UInt32 into the <see cref="IByteBuffer"/> of bytes as big endian.</summary>
        public void WriteUnsignedInt(uint value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }

            GrowAndEnsureIf(Int32ValueLength);
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(_buffer), value);
            AdvanceCore(Int32ValueLength);
        }

        /// <summary>Write a UInt32 into the <see cref="IByteBuffer"/> of bytes as little endian.</summary>
        public void WriteUnsignedIntLE(uint value)
        {
            if (!BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }

            GrowAndEnsureIf(Int32ValueLength);
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(_buffer), value);
            AdvanceCore(Int32ValueLength);
        }

        /// <summary>Write a UInt64 into the <see cref="IByteBuffer"/> of bytes as big endian.</summary>
        public void WriteUnsignedLong(ulong value)
        {
            if (BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }

            GrowAndEnsureIf(Int64ValueLength);
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(_buffer), value);
            AdvanceCore(Int64ValueLength);
        }

        /// <summary>Write a UInt64 into the <see cref="IByteBuffer"/> of bytes as little endian.</summary>
        public void WriteUnsignedLongLE(ulong value)
        {
            if (!BitConverter.IsLittleEndian)
            {
                value = BinaryPrimitives.ReverseEndianness(value);
            }

            GrowAndEnsureIf(Int64ValueLength);
            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(_buffer), value);
            AdvanceCore(Int64ValueLength);
        }
    }
}
