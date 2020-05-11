#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial interface IByteBuffer : IBufferWriter<byte>
    {
        void AdvanceReader(int count);

        ReadOnlyMemory<byte> UnreadMemory { get; }

        ReadOnlyMemory<byte> GetReadableMemory(int index, int count);

        ReadOnlySpan<byte> UnreadSpan { get; }

        ReadOnlySpan<byte> GetReadableSpan(int index, int count);

        ReadOnlySequence<byte> UnreadSequence { get; }

        ReadOnlySequence<byte> GetSequence(int index, int count);

        Memory<byte> FreeMemory { get; }

        Memory<byte> GetMemory(int index, int count);

        Span<byte> FreeSpan { get; }

        Span<byte> GetSpan(int index, int count);

        int GetBytes(int index, Span<byte> destination);
        int GetBytes(int index, Memory<byte> destination);

        int ReadBytes(Span<byte> destination);
        int ReadBytes(Memory<byte> destination);

        IByteBuffer SetBytes(int index, in ReadOnlySpan<byte> src);
        IByteBuffer SetBytes(int index, in ReadOnlyMemory<byte> src);

        IByteBuffer WriteBytes(in ReadOnlySpan<byte> src);
        IByteBuffer WriteBytes(in ReadOnlyMemory<byte> src);

        int FindIndex(int index, int count, Predicate<byte> match);

        int FindLastIndex(int index, int count, Predicate<byte> match);

        int IndexOf(int fromIndex, int toIndex, byte value);

        int IndexOf(int fromIndex, int toIndex, in ReadOnlySpan<byte> values);

        int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1);

        int IndexOfAny(int fromIndex, int toIndex, byte value0, byte value1, byte value2);

        int IndexOfAny(int fromIndex, int toIndex, in ReadOnlySpan<byte> values);
    }
}
#endif