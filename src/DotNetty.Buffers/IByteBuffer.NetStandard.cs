#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial interface IByteBuffer : IBufferWriter<byte>
    {
        ReadOnlyMemory<byte> GetReadableMemory(int index, int count);

        ReadOnlySpan<byte> GetReadableSpan(int index, int count);

        ReadOnlySequence<byte> GetSequence(int index, int count);

        Memory<byte> FreeMemory { get; }

        Memory<byte> GetMemory(int index, int count);

        Span<byte> Free { get; }

        Span<byte> GetSpan(int index, int count);

        int GetBytes(int index, Span<byte> destination);
        int GetBytes(int index, Memory<byte> destination);

        int ReadBytes(Span<byte> destination);
        int ReadBytes(Memory<byte> destination);

        IByteBuffer SetBytes(int index, ReadOnlySpan<byte> src);
        IByteBuffer SetBytes(int index, ReadOnlyMemory<byte> src);

        IByteBuffer WriteBytes(ReadOnlySpan<byte> src);
        IByteBuffer WriteBytes(ReadOnlyMemory<byte> src);

        int FindIndex(int index, int count, Predicate<byte> match);

        int FindLastIndex(int index, int count, Predicate<byte> match);

        int IndexOf(int fromIndex, int toIndex, byte value);

        int IndexOf(int fromIndex, int toIndex, ReadOnlySpan<byte> values);

        int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1);

        int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1, byte value2);

        int IndexOfAny(int fromIndex, int toIndex, ReadOnlySpan<byte> values);
    }
}
#endif