namespace DotNetty.Buffers
{
    using System;
    using System.Runtime.InteropServices;
    using CuteAnt;

    public ref partial struct ByteBufferWriter
    {
        public void WriteFloat(float value)
        {
            WriteInt(ByteBufferUtil.SingleToInt32Bits(value));
        }

        public void WriteFloatLE(float value)
        {
            WriteIntLE(ByteBufferUtil.SingleToInt32Bits(value));
        }

        public void WriteDouble(double value)
        {
            WriteLong(BitConverter.DoubleToInt64Bits(value));
        }

        public void WriteDoubleLE(double value)
        {
            WriteLongLE(BitConverter.DoubleToInt64Bits(value));
        }

        public void WriteDecimal(decimal value)
        {
            GrowAndEnsureIf(DecimalValueLength);
            SetDecimal(ref MemoryMarshal.GetReference(_buffer), value);
            AdvanceCore(DecimalValueLength);
        }

        public void WriteDecimalLE(decimal value)
        {
            GrowAndEnsureIf(DecimalValueLength);
            SetDecimalLE(ref MemoryMarshal.GetReference(_buffer), value);
            AdvanceCore(DecimalValueLength);
        }

        public void WriteDatetime(DateTime value)
        {
            WriteLong(value.ToBinary());
        }

        public void WriteDatetimeLE(DateTime value)
        {
            WriteLongLE(value.ToBinary());
        }

        public void WriteTimeSpan(TimeSpan value)
        {
            WriteLong(value.Ticks);
        }

        public void WriteTimeSpanLE(TimeSpan value)
        {
            WriteLongLE(value.Ticks);
        }

        public void WriteGuid(Guid value)
        {
            GrowAndEnsureIf(GuidValueLength);
            value.ToByteArray().AsSpan().CopyTo(_buffer);
            AdvanceCore(GuidValueLength);
        }

        public void WriteCombGuid(CombGuid value)
        {
            GrowAndEnsureIf(GuidValueLength);
            var raw = value.GetByteArray();
            raw.AsSpan().CopyTo(_buffer);
            AdvanceCore(GuidValueLength);
        }
    }
}
