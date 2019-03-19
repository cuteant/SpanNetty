#if !NET40
namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;

    partial interface IByteBuffer : IBufferWriter<byte>
    {
        ReadOnlyMemory<byte> GetReadableMemory();
        ReadOnlyMemory<byte> GetReadableMemory(int index, int count);

        ReadOnlySpan<byte> GetReadableSpan();
        ReadOnlySpan<byte> GetReadableSpan(int index, int count);

        ReadOnlySequence<byte> GetSequence();
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
    }
}
#endif