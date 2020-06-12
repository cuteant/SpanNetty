namespace DotNetty.Buffers
{
    using System;
    using System.Text;
    using DotNetty.Common.Utilities;

    public interface IByteBuffer2
    {
        IByteBuffer Copy();

        ArraySegment<byte> GetIoBuffer();

        ArraySegment<byte>[] GetIoBuffers();

        int IndexOf(byte value);

        int BytesBefore(byte value);

        int BytesBefore(int length, byte value);

        int BytesBefore(int index, int length, byte value);

        int FindIndex(Predicate<byte> match);

        int FindLastIndex(Predicate<byte> match);

        int ForEachByte(IByteProcessor processor);

        int ForEachByteDesc(IByteProcessor processor);

        string ToString(Encoding encoding);

        string ToString(int index, int length, Encoding encoding);
    }
}
