// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using CuteAnt.IO.Pipelines.Tests;
using Xunit;

namespace DotNetty.Buffers.Tests
{
    public class ArrayByte : SingleSegment
    {
        public ArrayByte() : base(ReadOnlyBufferFactory<byte>.Array, s_byteInputData) { }
    }

    public class OwnedMemoryByte : SingleSegment
    {
        public OwnedMemoryByte() : base(ReadOnlyBufferFactory<byte>.OwnedMemory, s_byteInputData) { }
    }

    public class SingleSegmentByte : SingleSegment
    {
        public SingleSegmentByte() : base(s_byteInputData) { }
    }

    public abstract class SingleSegment : ReaderBasicTests
    {
        public SingleSegment(byte[] inputData) : base(ReadOnlyBufferFactory<byte>.SingleSegment, inputData) { }
        internal SingleSegment(ReadOnlyBufferFactory<byte> factory, byte[] inputData) : base(factory, inputData) { }

        [Fact]
        public void AdvanceSingleBufferSkipsBytes()
        {
            var reader = new ByteBufferReader(BufferUtilities.CreateBuffer(GetInputData(5)));
            reader.Advance(2);
            Assert.Equal(2, reader.CurrentSpanIndex);
            Assert.Equal(InputData[2], reader.CurrentSpan[reader.CurrentSpanIndex]);
            Assert.True(reader.TryPeek(out byte value));
            Assert.Equal(InputData[2], value);
            reader.Advance(2);
            Assert.True(reader.TryPeek(out value));
            Assert.Equal(InputData[4], value);
            Assert.Equal(4, reader.CurrentSpanIndex);
            Assert.Equal(InputData[4], reader.CurrentSpan[reader.CurrentSpanIndex]);
        }

        [Fact]
        public void TakeReturnsByteAndMoves()
        {
            var reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(2)));
            Assert.Equal(0, reader.CurrentSpanIndex);
            Assert.Equal(InputData[0], reader.CurrentSpan[reader.CurrentSpanIndex]);
            Assert.True(reader.TryRead(out byte value));
            Assert.Equal(InputData[0], value);
            Assert.Equal(1, reader.CurrentSpanIndex);
            Assert.Equal(InputData[1], reader.CurrentSpan[reader.CurrentSpanIndex]);
            Assert.True(reader.TryRead(out value));
            Assert.Equal(InputData[1], value);
            Assert.False(reader.TryRead(out value));
            Assert.Equal(default, value);
            Assert.True(reader.End);
        }

        [Fact]
        public void DefaultState()
        {
            byte[] array = new byte[] { default };
            ByteBufferReader reader = default;
            Assert.True(reader.End);
            Assert.False(reader.TryPeek(out byte value));
            Assert.Equal(default, value);
            Assert.False(reader.TryRead(out value));
            Assert.Equal(default, value);
            Assert.Equal(0, reader.CurrentSpan.Length);
            Assert.Equal(0, reader.UnreadSpan.Length);
            Assert.Equal(0, reader.Consumed);
            Assert.Equal(0, reader.CurrentSpanIndex);
            Assert.Equal(0, reader.AdvancePast(default));
            Assert.Equal(0, reader.AdvancePastAny(array));
            Assert.Equal(0, reader.AdvancePastAny(default));
            Assert.False(reader.TryReadTo(out ReadOnlySequence<byte> sequence, default(byte)));
            Assert.True(sequence.IsEmpty);
            Assert.False(reader.TryReadTo(out sequence, array));
            Assert.True(sequence.IsEmpty);
            Assert.False(reader.TryReadTo(out ReadOnlySpan<byte> span, default(byte)));
            Assert.True(span.IsEmpty);
            Assert.False(reader.TryReadToAny(out sequence, array));
            Assert.True(sequence.IsEmpty);
            Assert.False(reader.TryReadToAny(out span, array));
            Assert.True(span.IsEmpty);
            Assert.False(reader.TryAdvanceTo(default));
            Assert.False(reader.TryAdvanceToAny(array));
        }
    }

    public class SegmentPerByte : ReaderBasicTests
    {
        public SegmentPerByte() : base(ReadOnlyBufferFactory<byte>.SegmentPerPosition, s_byteInputData) { }
    }

    public abstract class ReaderBasicTests
    {
        internal static byte[] s_byteInputData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        private byte[] _inputData;

        internal ReadOnlyBufferFactory<byte> Factory { get; }
        protected ReadOnlySpan<byte> InputData { get => _inputData; }

        public byte[] GetInputData(int count) => InputData.Slice(0, count).ToArray();

        internal ReaderBasicTests(ReadOnlyBufferFactory<byte> factory, byte[] inputData)
        {
            Factory = factory;
            _inputData = inputData;
        }

        [Fact]
        public void PeekReturnsWithoutMoving()
        {
            var reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(2)));
            Assert.True(reader.TryPeek(out byte value));
            Assert.Equal(InputData[0], value);
            Assert.True(reader.TryPeek(out value));
            Assert.Equal(InputData[0], value);
        }

        [Fact]
        public void CursorIsCorrectAtEnd()
        {
            var reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(2)));
            reader.TryRead(out byte _);
            reader.TryRead(out byte _);
            Assert.True(reader.End);
        }

        [Fact]
        public void CursorIsCorrectWithEmptyLastBlock()
        {
            var last = new BufferSegment<byte>();
            last.SetMemory(new OwnedArray<byte>(new byte[4]), 0, 4);

            var first = new BufferSegment<byte>();
            first.SetMemory(new OwnedArray<byte>(GetInputData(2)), 0, 2);
            first.SetNext(last);

            var reader = new ByteBufferReader(new ReadOnlySequence<byte>(first, first.Start, last, last.Start));
            reader.TryRead(out byte _);
            reader.TryRead(out byte _);
            reader.TryRead(out byte _);
            Assert.Same(last, reader.Position.GetObject());
            Assert.Equal(0, reader.Position.GetInteger());
            Assert.True(reader.End);
        }

        [Fact]
        public void PeekReturnsDefaultInTheEnd()
        {
            var reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(2)));
            Assert.True(reader.TryRead(out byte value));
            Assert.Equal(InputData[0], value);
            Assert.True(reader.TryRead(out  value));
            Assert.Equal(InputData[1], value);
            Assert.False(reader.TryRead(out value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void AdvanceToEndThenPeekReturnsDefault()
        {
            var reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(5)));
            reader.Advance(5);
            Assert.True(reader.End);
            Assert.False(reader.TryPeek(out byte value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void AdvancingPastLengthThrows()
        {
            var reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(5)));
            try
            {
                reader.Advance(6);
                Assert.True(false);
            }
            catch (Exception ex)
            {
                Assert.True(ex is ArgumentOutOfRangeException);
                Assert.Equal(5, reader.Consumed);
            }
        }

        [Fact]
        public void CtorFindsFirstNonEmptySegment()
        {
            var buffer = Factory.CreateWithContent(GetInputData(1));
            var reader = new ByteBufferReader(buffer);

            Assert.True(reader.TryPeek(out byte value));
            Assert.Equal(InputData[0], value);
        }

        [Fact]
        public void EmptySegmentsAreSkippedOnMoveNext()
        {
            var buffer = Factory.CreateWithContent(GetInputData(2));
            var reader = new ByteBufferReader(buffer);

            Assert.True(reader.TryPeek(out byte value));
            Assert.Equal(InputData[0], value);
            reader.Advance(1);
            Assert.True(reader.TryPeek(out value));
            Assert.Equal(InputData[1], value);
        }

        [Fact]
        public void PeekGoesToEndIfAllEmptySegments()
        {
            var buffer = BufferUtilities.CreateBuffer(new[] { new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { } });
            var reader = new ByteBufferReader(buffer);

            Assert.False(reader.TryPeek(out byte value));
            Assert.Equal(default, value);
            Assert.True(reader.End);
        }

        [Fact]
        public void AdvanceTraversesSegments()
        {
            var buffer = Factory.CreateWithContent(GetInputData(3));
            var reader = new ByteBufferReader(buffer);

            reader.Advance(2);
            Assert.Equal(InputData[2], reader.CurrentSpan[reader.CurrentSpanIndex]);
            Assert.True(reader.TryRead(out byte value));
            Assert.Equal(InputData[2], value);
        }

        [Fact]
        public void AdvanceThrowsPastLengthMultipleSegments()
        {
            var buffer = Factory.CreateWithContent(GetInputData(3));
            var reader = new ByteBufferReader(buffer);

            try
            {
                reader.Advance(4);
                Assert.True(false);
            }
            catch (Exception ex)
            {
                Assert.True(ex is ArgumentOutOfRangeException);
            }
        }

        [Fact]
        public void TakeTraversesSegments()
        {
            var buffer = Factory.CreateWithContent(GetInputData(3));
            var reader = new ByteBufferReader(buffer);

            Assert.True(reader.TryRead(out byte value));
            Assert.Equal(InputData[0], value);
            Assert.True(reader.TryRead(out value));
            Assert.Equal(InputData[1], value);
            Assert.True(reader.TryRead(out value));
            Assert.Equal(InputData[2], value);
            Assert.False(reader.TryRead(out value));
            Assert.Equal(default, value);
            Assert.True(reader.End);
        }

        [Fact]
        public void PeekTraversesSegments()
        {
            var buffer = Factory.CreateWithContent(GetInputData(2));
            var reader = new ByteBufferReader(buffer);

            Assert.Equal(InputData[0], reader.CurrentSpan[reader.CurrentSpanIndex]);
            Assert.True(reader.TryRead(out byte value));
            Assert.Equal(InputData[0], value);

            Assert.Equal(InputData[1], reader.CurrentSpan[reader.CurrentSpanIndex]);
            Assert.True(reader.TryPeek(out value));
            Assert.Equal(InputData[1], value);
            Assert.True(reader.TryRead(out value));
            Assert.Equal(InputData[1], value);
            Assert.False(reader.TryPeek(out value));
            Assert.False(reader.TryRead(out value));
            Assert.Equal(default, value);
            Assert.Equal(default, value);
        }

        [Fact]
        public void PeekWorkesWithEmptySegments()
        {
            var buffer = Factory.CreateWithContent(GetInputData(1));
            var reader = new ByteBufferReader(buffer);

            Assert.Equal(0, reader.CurrentSpanIndex);
            Assert.Equal(1, reader.CurrentSpan.Length);
            Assert.True(reader.TryPeek(out byte value));
            Assert.Equal(InputData[0], value);
            Assert.True(reader.TryRead(out value));
            Assert.Equal(InputData[0], value);
            Assert.False(reader.TryPeek(out value));
            Assert.False(reader.TryRead(out value));
            Assert.Equal(default, value);
            Assert.Equal(default, value);
        }

        [Fact]
        public void WorkesWithEmptyBuffer()
        {
            var reader = new ByteBufferReader(Factory.CreateWithContent(new byte[] { }));

            Assert.Equal(0, reader.CurrentSpanIndex);
            Assert.Equal(0, reader.CurrentSpan.Length);
            Assert.False(reader.TryPeek(out byte value));
            Assert.Equal(default, value);
            Assert.False(reader.TryRead(out value));
            Assert.Equal(default, value);
            Assert.True(reader.End);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(5, false)]
        [InlineData(10, false)]
        [InlineData(11, true)]
        [InlineData(12, true)]
        [InlineData(15, true)]
        public void ReturnsCorrectCursor(int takes, bool end)
        {
            var readableBuffer = Factory.CreateWithContent(GetInputData(10));
            var reader = new ByteBufferReader(readableBuffer);
            for (int i = 0; i < takes; i++)
            {
                reader.TryRead(out _);
            }

            var expected = end ? new byte[] { } : readableBuffer.Slice(takes).ToArray();
            Assert.Equal(expected, readableBuffer.Slice(reader.Position).ToArray());
        }

        [Fact]
        public void SlicingBufferReturnsCorrectCursor()
        {
            var buffer = Factory.CreateWithContent(GetInputData(10));
            var sliced = buffer.Slice(2L);

            var reader = new ByteBufferReader(sliced);
            Assert.Equal(sliced.ToArray(), buffer.Slice(reader.Position).ToArray());
            Assert.True(reader.TryPeek(out byte value));
            Assert.Equal(InputData[2], value);
            Assert.Equal(0, reader.CurrentSpanIndex);
        }

        [Fact]
        public void ReaderIndexIsCorrect()
        {
            var buffer = Factory.CreateWithContent(GetInputData(10));
            var reader = new ByteBufferReader(buffer);

            int counter = 0;
            while (!reader.End)
            {
                var span = reader.CurrentSpan;
                for (int i = reader.CurrentSpanIndex; i < span.Length; i++)
                {
                    Assert.Equal(InputData[counter++], reader.CurrentSpan[i]);
                }
                reader.Advance(span.Length);
            }
            Assert.Equal(buffer.Length, reader.Consumed);
        }

        [Theory,
            InlineData(1),
            InlineData(2),
            InlineData(3)]
        public void Advance_PositionIsCorrect(int advanceBy)
        {
            // Check that advancing through the reader gives the same position
            // as returned directly from the buffer.

            ReadOnlySequence<byte> buffer = Factory.CreateWithContent(GetInputData(10));
            ByteBufferReader reader = new ByteBufferReader(buffer);

            SequencePosition readerPosition = reader.Position;
            SequencePosition bufferPosition = buffer.GetPosition(0);
            Assert.Equal(readerPosition.GetInteger(), bufferPosition.GetInteger());
            Assert.Same(readerPosition.GetObject(), readerPosition.GetObject());

            for (int i = advanceBy; i <= buffer.Length; i += advanceBy)
            {
                reader.Advance(advanceBy);
                readerPosition = reader.Position;
                bufferPosition = buffer.GetPosition(i);
                Assert.Equal(readerPosition.GetInteger(), bufferPosition.GetInteger());
                Assert.Same(readerPosition.GetObject(), readerPosition.GetObject());
            }
        }

        [Fact]
        public void AdvanceTo()
        {
            // Ensure we can advance to each of the items in the buffer

            byte[] inputData = GetInputData(10);
            ReadOnlySequence<byte> buffer = Factory.CreateWithContent(inputData);

            for (int i = 0; i < buffer.Length; i++)
            {
                ByteBufferReader reader = new ByteBufferReader(buffer);
                Assert.True(reader.TryAdvanceTo(inputData[i], advancePastDelimiter: false));
                Assert.True(reader.TryPeek(out byte value));
                Assert.Equal(inputData[i], value);
            }
        }

        [Fact]
        public void AdvanceTo_AdvancePast()
        {
            // Ensure we can advance to each of the items in the buffer (skipping what we advanced to)

            byte[] inputData = GetInputData(10);
            ReadOnlySequence<byte> buffer = Factory.CreateWithContent(inputData);

            for (int start = 0; start < 2; start++)
            {
                for (int i = start; i < buffer.Length - 1; i += 2)
                {
                    ByteBufferReader reader = new ByteBufferReader(buffer);
                    Assert.True(reader.TryAdvanceTo(inputData[i], advancePastDelimiter: true));
                    Assert.True(reader.TryPeek(out byte value));
                    Assert.Equal(inputData[i + 1], value);
                }
            }
        }

        [Fact]
        public void CopyToLargerBufferWorks()
        {
            var content = (byte[])_inputData.Clone();

            Span<byte> buffer = new byte[content.Length + 1];
            var reader = new ByteBufferReader(Factory.CreateWithContent(content));

            // this loop skips more and more items in the reader
            for (int i = 0; i < content.Length; i++)
            {
                var copied = Math.Min(buffer.Length, (int)reader.Remaining);
                reader.CopyTo(buffer.Slice(0, copied));
                Assert.Equal(content.Length - i, copied);
                Assert.True(buffer.Slice(0, copied).SequenceEqual(content.AsSpan(i)));

                // make sure that nothing more got written, i.e. tail is empty
                for (int r = copied; r < buffer.Length; r++)
                {
                    Assert.Equal(default, buffer[r]);
                }

                reader.Advance(1);
                buffer.Clear();
            }
        }

        [Fact]
        public void CopyToSmallerBufferWorks()
        {
            var content = (byte[])_inputData.Clone();

            Span<byte> buffer = new byte[content.Length];
            var reader = new ByteBufferReader(Factory.CreateWithContent(content));

            // this loop skips more and more items in the reader
            for (int i = 0; i < content.Length; i++)
            {
                // this loop makes the destination buffer smaller and smaller
                for (int j = 0; j < buffer.Length - i; j++)
                {
                    Span<byte> bufferSlice = buffer.Slice(0, j);
                    bufferSlice.Clear();
                    //ReadOnlySpan<byte> peeked = reader.Peek(bufferSlice);
                    var copied = Math.Min(bufferSlice.Length, (int)reader.Remaining);
                    reader.CopyTo(bufferSlice.Slice(0, copied));
                    Assert.Equal(Math.Min(bufferSlice.Length, content.Length - i), copied);

                    Assert.True(/*peeked*/bufferSlice.Slice(0, copied).SequenceEqual(content.AsSpan(i, j)));
                }

                reader.Advance(1);
            }
        }
    }
}
