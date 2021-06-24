// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using DotNetty.Buffers;
using Xunit;

namespace System.Memory.Tests.BufferReader
{
    public class ArrayByte : SingleSegment
    {
        public ArrayByte() : base(ReadOnlySequenceFactory<byte>.ArrayFactory, s_byteInputData) { }
    }

    public class MemoryByte : SingleSegment
    {
        public MemoryByte() : base(ReadOnlySequenceFactory<byte>.MemoryFactory, s_byteInputData) { }
    }

    public class SingleSegmentByte : SingleSegment
    {
        public SingleSegmentByte() : base(s_byteInputData) { }
    }

    public abstract class SingleSegment : ReaderBasicTests
    {
        public SingleSegment(byte[] inputData) : base(ReadOnlySequenceFactory<byte>.SingleSegmentFactory, inputData) { }
        internal SingleSegment(ReadOnlySequenceFactory<byte> factory, byte[] inputData) : base(factory, inputData) { }

        [Fact]
        public void AdvanceSingleBufferSkipsValues()
        {
            ByteBufferReader reader = new ByteBufferReader(SequenceFactory.Create(GetInputData(5)));
            Assert.Equal(5, reader.Length);
            Assert.Equal(5, reader.Remaining);
            Assert.Equal(0, reader.Consumed);
            Assert.Equal(0, reader.CurrentSpanIndex);

            // Advance 2 positions
            reader.Advance(2);
            Assert.Equal(5, reader.Length);
            Assert.Equal(3, reader.Remaining);
            Assert.Equal(2, reader.Consumed);
            Assert.Equal(2, reader.CurrentSpanIndex);
            Assert.Equal(InputData[2], reader.CurrentSpan[reader.CurrentSpanIndex]);
            Assert.True(reader.TryPeek(out byte value));
            Assert.Equal(InputData[2], value);

            // Advance 2 positions
            reader.Advance(2);
            Assert.Equal(1, reader.Remaining);
            Assert.Equal(4, reader.Consumed);
            Assert.Equal(4, reader.CurrentSpanIndex);
            Assert.Equal(InputData[4], reader.CurrentSpan[reader.CurrentSpanIndex]);
            Assert.True(reader.TryPeek(out value));
            Assert.Equal(InputData[4], value);
        }

        [Fact]
        public void TryReadReturnsValueAndAdvances()
        {
            ByteBufferReader reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(2)));
            Assert.Equal(2, reader.Length);
            Assert.Equal(2, reader.Remaining);
            Assert.Equal(0, reader.Consumed);
            Assert.Equal(0, reader.CurrentSpanIndex);
            Assert.Equal(InputData[0], reader.CurrentSpan[reader.CurrentSpanIndex]);

            // Read 1st value
            Assert.True(reader.TryRead(out byte value));
            Assert.Equal(InputData[0], value);
            Assert.Equal(1, reader.Remaining);
            Assert.Equal(1, reader.Consumed);
            Assert.Equal(1, reader.CurrentSpanIndex);
            Assert.Equal(InputData[1], reader.CurrentSpan[reader.CurrentSpanIndex]);

            // Read 2nd value
            Assert.True(reader.TryRead(out value));
            Assert.Equal(InputData[1], value);
            Assert.Equal(0, reader.Remaining);
            Assert.Equal(2, reader.Consumed);
            Assert.Equal(2, reader.CurrentSpanIndex);

            // Read at end
            Assert.False(reader.TryRead(out value));
            Assert.Equal(default, value);
            Assert.Equal(0, reader.Remaining);
            Assert.Equal(2, reader.Consumed);
            Assert.Equal(2, reader.CurrentSpanIndex);
            Assert.True(reader.End);
        }

        [Fact]
        public void DefaultState()
        {
            byte[] array = new byte[] { default };
            ByteBufferReader reader = default;
            Assert.Equal(0, reader.CurrentSpan.Length);
            Assert.Equal(0, reader.UnreadSpan.Length);
            Assert.Equal(0, reader.UnreadSequence.Length);
            Assert.Equal(0, reader.Consumed);
            Assert.Equal(0, reader.CurrentSpanIndex);
            Assert.Equal(0, reader.Length);
            Assert.Equal(0, reader.Remaining);
            Assert.True(reader.End);
            Assert.False(reader.TryPeek(out byte value));
            Assert.Equal(default, value);
            Assert.False(reader.TryRead(out value));
            Assert.Equal(default, value);
            Assert.Equal(0, reader.AdvancePast(default));
            Assert.Equal(0, reader.AdvancePastAny(array));
            Assert.Equal(0, reader.AdvancePastAny(default));
            Assert.False(reader.TryReadTo(out ReadOnlySequence<byte> sequence, default(byte)));
            Assert.True(sequence.IsEmpty);
            Assert.False(reader.TryReadTo(out sequence, array));
            Assert.True(sequence.IsEmpty);
            Assert.False(reader.TryReadTo(out ReadOnlySpan<byte> span, default(byte)));
            Assert.True(span.IsEmpty);
            Assert.False(reader.TryReadTo(out span, array));
            Assert.True(span.IsEmpty);
            Assert.False(reader.TryReadToAny(out sequence, array));
            Assert.True(sequence.IsEmpty);
            Assert.False(reader.TryReadToAny(out span, array));
            Assert.True(span.IsEmpty);
            Assert.False(reader.TryAdvanceTo(default));
            Assert.False(reader.TryAdvanceToAny(array));
            Assert.Equal(0, reader.CurrentSpan.Length);
            Assert.Equal(0, reader.UnreadSpan.Length);
            Assert.Equal(0, reader.UnreadSequence.Length);
            Assert.Equal(0, reader.Consumed);
            Assert.Equal(0, reader.CurrentSpanIndex);
            Assert.Equal(0, reader.Length);
            Assert.Equal(0, reader.Remaining);
        }
    }

    public class SegmentPerByte : ReaderBasicTests
    {
        public SegmentPerByte() : base(ReadOnlySequenceFactory<byte>.SegmentPerItemFactory, s_byteInputData) { }
    }

    public abstract class ReaderBasicTests
    {
        internal static byte[] s_byteInputData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        private byte[] _inputData;

        internal ReadOnlySequenceFactory<byte> Factory { get; }
        protected ReadOnlySpan<byte> InputData { get => _inputData; }

        public byte[] GetInputData(int count) => InputData.Slice(0, count).ToArray();

        internal ReaderBasicTests(ReadOnlySequenceFactory<byte> factory, byte[] inputData)
        {
            Factory = factory;
            _inputData = inputData;
        }

        [Fact]
        public void TryPeekReturnsWithoutMoving()
        {
            ByteBufferReader reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(2)));
            Assert.Equal(0, reader.Consumed);
            Assert.Equal(2, reader.Remaining);
            Assert.True(reader.TryPeek(out byte value));
            Assert.Equal(InputData[0], value);
            Assert.Equal(0, reader.Consumed);
            Assert.Equal(2, reader.Remaining);
            Assert.True(reader.TryPeek(out value));
            Assert.Equal(InputData[0], value);
            Assert.Equal(0, reader.Consumed);
            Assert.Equal(2, reader.Remaining);
        }

        [Fact]
        public void TryPeekOffset()
        {
            ByteBufferReader reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(10)));
            Assert.True(reader.TryRead(out byte first));
            Assert.Equal(InputData[0], first);
            Assert.True(reader.TryRead(out byte second));
            Assert.Equal(InputData[1], second);

            Assert.True(reader.TryPeek(7, out byte value));
            Assert.Equal(InputData[9], value);

            Assert.False(reader.TryPeek(8, out byte defaultValue));
            Assert.Equal(default, defaultValue);

            Assert.Equal(2, reader.Consumed);
            Assert.Equal(8, reader.Remaining);
        }

        [Fact]
        public void TryPeekOffset_AfterEnd()
        {
            ByteBufferReader reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(2)));
            Assert.True(reader.TryRead(out byte first));
            Assert.Equal(InputData[0], first);

            Assert.True(reader.TryPeek(0, out byte value));
            Assert.Equal(InputData[1], value);
            Assert.Equal(1, reader.Remaining);

            Assert.False(reader.TryPeek(1, out byte defaultValue));
            Assert.Equal(default, defaultValue);
        }

        [Fact]
        public void TryPeekOffset_RemainsZeroOffsetZero()
        {
            ByteBufferReader reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(1)));
            Assert.True(reader.TryRead(out byte first));
            Assert.Equal(InputData[0], first);
            Assert.Equal(0, reader.Remaining);
            Assert.False(reader.TryPeek(0, out byte defaultValue));
            Assert.Equal(default, defaultValue);
        }

        [Fact]
        public void TryPeekOffset_Empty()
        {
            ByteBufferReader reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(0)));
            Assert.False(reader.TryPeek(0, out byte defaultValue));
            Assert.Equal(default, defaultValue);
        }

        [Fact]
        public void TryPeekOffset_MultiSegment_StarAhead()
        {
            ReadOnlySpan<byte> data = (byte[])_inputData.Clone();

            SequenceSegment<byte> last = new SequenceSegment<byte>();
            last.SetMemory(new OwnedArray<byte>(data.Slice(5).ToArray()), 0, 5);

            SequenceSegment<byte> first = new SequenceSegment<byte>();
            first.SetMemory(new OwnedArray<byte>(data.Slice(0, 5).ToArray()), 0, 5);
            first.SetNext(last);

            ReadOnlySequence<byte> sequence = new ReadOnlySequence<byte>(first, first.Start, last, last.End);
            ByteBufferReader reader = new ByteBufferReader(sequence);

            // Move by 2 element
            for (int i = 0; i < 2; i++)
            {
                Assert.True(reader.TryRead(out byte val));
                Assert.Equal(InputData[i], val);
            }

            // We're on element 3 we peek last element of first segment
            Assert.True(reader.TryPeek(2, out byte lastElementFirstSegment));
            Assert.Equal(InputData[4], lastElementFirstSegment);

            // We're on element 3 we peek first element of first segment
            Assert.True(reader.TryPeek(3, out byte fistElementSecondSegment));
            Assert.Equal(InputData[5], fistElementSecondSegment);

            // We're on element 3 we peek last element of second segment
            Assert.True(reader.TryPeek(7, out byte lastElementSecondSegment));
            Assert.Equal(InputData[9], lastElementSecondSegment);

            // 3 + 8 out of bounds
            Assert.False(reader.TryPeek(8, out byte defaultValue));
            Assert.Equal(default, defaultValue);

            Assert.Equal(2, reader.Consumed);
            Assert.Equal(8, reader.Remaining);
        }

        [Fact]
        public void TryPeekOffset_MultiSegment_GetFirstGetLast()
        {
            ReadOnlySpan<byte> data = (byte[])_inputData.Clone();

            SequenceSegment<byte> last = new SequenceSegment<byte>();
            last.SetMemory(new OwnedArray<byte>(data.Slice(5).ToArray()), 0, 5);

            SequenceSegment<byte> first = new SequenceSegment<byte>();
            first.SetMemory(new OwnedArray<byte>(data.Slice(0, 5).ToArray()), 0, 5);
            first.SetNext(last);

            ReadOnlySequence<byte> sequence = new ReadOnlySequence<byte>(first, first.Start, last, last.End);
            ByteBufferReader reader = new ByteBufferReader(sequence);

            Assert.True(reader.TryPeek(0, out byte firstElement));
            Assert.Equal(InputData[0], firstElement);

            Assert.True(reader.TryPeek(data.Length - 1, out byte lastElemen));
            Assert.Equal(InputData[data.Length - 1], lastElemen);

            Assert.Equal(0, reader.Consumed);
            Assert.Equal(10, reader.Remaining);
        }

        [Fact]
        public void TryPeekOffset_InvalidOffset()
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                ByteBufferReader reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(10)));
                reader.TryPeek(-1, out _);
            });

            Assert.Equal("offset", exception.ParamName);
        }

        [Fact]
        public void CursorIsCorrectAtEnd()
        {
            ByteBufferReader reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(2)));
            reader.TryRead(out byte _);
            reader.TryRead(out byte _);
            Assert.True(reader.End);
        }

        [Fact]
        public void CursorIsCorrectWithEmptyLastBlock()
        {
            SequenceSegment<byte> last = new SequenceSegment<byte>();
            last.SetMemory(new OwnedArray<byte>(new byte[4]), 0, 4);

            SequenceSegment<byte> first = new SequenceSegment<byte>();
            first.SetMemory(new OwnedArray<byte>(GetInputData(2)), 0, 2);
            first.SetNext(last);

            ByteBufferReader reader = new ByteBufferReader(new ReadOnlySequence<byte>(first, first.Start, last, last.Start));
            reader.TryRead(out byte _);
            reader.TryRead(out byte _);
            reader.TryRead(out byte _);
            Assert.Same(last, reader.Position.GetObject());
            Assert.Equal(0, reader.Position.GetInteger());
            Assert.True(reader.End);
        }

        [Fact]
        public void TryPeekReturnsDefaultInTheEnd()
        {
            ByteBufferReader reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(2)));
            Assert.True(reader.TryRead(out byte value));
            Assert.Equal(InputData[0], value);
            Assert.True(reader.TryRead(out value));
            Assert.Equal(InputData[1], value);
            Assert.False(reader.TryRead(out value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void AdvanceToEndThenPeekReturnsDefault()
        {
            ByteBufferReader reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(5)));
            reader.Advance(5);
            Assert.True(reader.End);
            Assert.False(reader.TryPeek(out byte value));
            Assert.Equal(default, value);
        }

        [Fact]
        public void AdvancingPastLengthThrows()
        {
            ByteBufferReader reader = new ByteBufferReader(Factory.CreateWithContent(GetInputData(5)));
            try
            {
                reader.Advance(6);
                Assert.True(false);
            }
            catch (Exception ex)
            {
                Assert.True(ex is ArgumentOutOfRangeException);
                Assert.Equal(5, reader.Consumed);
                Assert.Equal(0, reader.Remaining);
                Assert.True(reader.End);
            }
        }

        [Fact]
        public void CtorFindsFirstNonEmptySegment()
        {
            ReadOnlySequence<byte> buffer = Factory.CreateWithContent(GetInputData(1));
            ByteBufferReader reader = new ByteBufferReader(buffer);

            Assert.True(reader.TryPeek(out byte value));
            Assert.Equal(InputData[0], value);
            Assert.Equal(0, reader.Consumed);
            Assert.Equal(1, reader.Remaining);
        }

        [Fact]
        public void EmptySegmentsAreSkippedOnMoveNext()
        {
            ReadOnlySequence<byte> buffer = Factory.CreateWithContent(GetInputData(2));
            ByteBufferReader reader = new ByteBufferReader(buffer);

            Assert.True(reader.TryPeek(out byte value));
            Assert.Equal(InputData[0], value);
            reader.Advance(1);
            Assert.True(reader.TryPeek(out value));
            Assert.Equal(InputData[1], value);
        }

        [Fact]
        public void TryPeekGoesToEndIfAllEmptySegments()
        {
            ReadOnlySequence<byte> buffer = SequenceFactory.Create(new[] { new byte[] { }, new byte[] { }, new byte[] { }, new byte[] { } });
            ByteBufferReader reader = new ByteBufferReader(buffer);

            Assert.False(reader.TryPeek(out byte value));
            Assert.Equal(default, value);
            Assert.True(reader.End);
        }

        [Fact]
        public void AdvanceTraversesSegments()
        {
            ReadOnlySequence<byte> buffer = Factory.CreateWithContent(GetInputData(3));
            ByteBufferReader reader = new ByteBufferReader(buffer);

            reader.Advance(2);
            Assert.Equal(InputData[2], reader.CurrentSpan[reader.CurrentSpanIndex]);
            Assert.True(reader.TryRead(out byte value));
            Assert.Equal(InputData[2], value);
        }

        [Fact]
        public void AdvanceThrowsPastLengthMultipleSegments()
        {
            ReadOnlySequence<byte> buffer = Factory.CreateWithContent(GetInputData(3));
            ByteBufferReader reader = new ByteBufferReader(buffer);

            try
            {
                reader.Advance(4);
                Assert.True(false);
            }
            catch (Exception ex)
            {
                Assert.True(ex is ArgumentOutOfRangeException);
                Assert.Equal(3, reader.Consumed);
                Assert.Equal(0, reader.Remaining);
                Assert.True(reader.End);
            }
        }

        [Fact]
        public void TryReadTraversesSegments()
        {
            ReadOnlySequence<byte> buffer = Factory.CreateWithContent(GetInputData(3));
            ByteBufferReader reader = new ByteBufferReader(buffer);

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
        public void TryPeekTraversesSegments()
        {
            ReadOnlySequence<byte> buffer = Factory.CreateWithContent(GetInputData(2));
            ByteBufferReader reader = new ByteBufferReader(buffer);

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
            ReadOnlySequence<byte> buffer = Factory.CreateWithContent(GetInputData(1));
            ByteBufferReader reader = new ByteBufferReader(buffer);

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
        public void WorksWithEmptyBuffer()
        {
            ByteBufferReader reader = new ByteBufferReader(Factory.CreateWithContent(new byte[] { }));

            Assert.Equal(0, reader.CurrentSpanIndex);
            Assert.Equal(0, reader.CurrentSpan.Length);
            Assert.Equal(0, reader.Length);
            Assert.Equal(0, reader.Remaining);
            Assert.True(reader.End);
            Assert.False(reader.TryPeek(out byte value));
            Assert.Equal(default, value);
            Assert.False(reader.TryRead(out value));
            Assert.Equal(default, value);
            Assert.True(reader.End);
        }

        [Theory,
            InlineData(0, false),
            InlineData(5, false),
            InlineData(10, false),
            InlineData(11, true),
            InlineData(12, true),
            InlineData(15, true)]
        public void ReturnsCorrectCursor(int takes, bool end)
        {
            ReadOnlySequence<byte> readableBuffer = Factory.CreateWithContent(GetInputData(10));
            ByteBufferReader reader = new ByteBufferReader(readableBuffer);
            for (int i = 0; i < takes; i++)
            {
                reader.TryRead(out _);
            }

            byte[] expected = end ? new byte[] { } : readableBuffer.Slice(takes).ToArray();
            Assert.Equal(expected, readableBuffer.Slice(reader.Position).ToArray());
        }

        [Fact]
        public void SlicingBufferReturnsCorrectCursor()
        {
            ReadOnlySequence<byte> buffer = Factory.CreateWithContent(GetInputData(10));
            ReadOnlySequence<byte> sliced = buffer.Slice(2L);

            ByteBufferReader reader = new ByteBufferReader(sliced);
            Assert.Equal(sliced.ToArray(), buffer.Slice(reader.Position).ToArray());
            Assert.True(reader.TryPeek(out byte value));
            Assert.Equal(InputData[2], value);
            Assert.Equal(0, reader.CurrentSpanIndex);
        }

        [Fact]
        public void ReaderIndexIsCorrect()
        {
            ReadOnlySequence<byte> buffer = Factory.CreateWithContent(GetInputData(10));
            ByteBufferReader reader = new ByteBufferReader(buffer);

            int counter = 0;
            while (!reader.End)
            {
                ReadOnlySpan<byte> span = reader.CurrentSpan;
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
        public void AdvanceTo_End()
        {
            ReadOnlySpan<byte> data = (byte[])_inputData.Clone();

            SequenceSegment<byte> last = new SequenceSegment<byte>();
            last.SetMemory(new OwnedArray<byte>(data.Slice(5).ToArray()), 0, 5);

            SequenceSegment<byte> first = new SequenceSegment<byte>();
            first.SetMemory(new OwnedArray<byte>(data.Slice(0, 5).ToArray()), 0, 5);
            first.SetNext(last);

            ReadOnlySequence<byte> sequence = new ReadOnlySequence<byte>(first, first.Start, last, last.End);
            ByteBufferReader reader = new ByteBufferReader(sequence);

            reader.AdvanceToEnd();

            Assert.Equal(data.Length, reader.Length);
            Assert.Equal(data.Length, reader.Consumed);
            Assert.Equal(reader.Length, reader.Consumed);
            Assert.True(reader.End);
            Assert.Equal(0, reader.CurrentSpanIndex);
            Assert.Equal(sequence.End, reader.Position);
            Assert.Equal(0, reader.Remaining);
            Assert.True(default == reader.UnreadSpan);
            Assert.True(default == reader.CurrentSpan);
        }

        [Fact]
        public void AdvanceTo_End_EmptySegment()
        {
            ReadOnlySpan<byte> data = (byte[])_inputData.Clone();

            // Empty segment
            SequenceSegment<byte> third = new SequenceSegment<byte>();

            SequenceSegment<byte> second = new SequenceSegment<byte>();
            second.SetMemory(new OwnedArray<byte>(data.Slice(5).ToArray()), 0, 5);
            second.SetNext(third);

            SequenceSegment<byte> first = new SequenceSegment<byte>();
            first.SetMemory(new OwnedArray<byte>(data.Slice(0, 5).ToArray()), 0, 5);
            first.SetNext(second);

            ReadOnlySequence<byte> sequence = new ReadOnlySequence<byte>(first, first.Start, third, third.End);
            ByteBufferReader reader = new ByteBufferReader(sequence);

            reader.AdvanceToEnd();

            Assert.Equal(first.Length + second.Length, reader.Length);
            Assert.Equal(first.Length + second.Length, reader.Consumed);
            Assert.Equal(reader.Length, reader.Consumed);
            Assert.True(reader.End);
            Assert.Equal(0, reader.CurrentSpanIndex);
            Assert.Equal(sequence.End, reader.Position);
            Assert.Equal(0, reader.Remaining);
            Assert.True(default == reader.UnreadSpan);
            Assert.True(default == reader.CurrentSpan);
        }

        [Fact]
        public void AdvanceTo_End_Rewind_Advance()
        {
            ReadOnlySpan<byte> data = (byte[])_inputData.Clone();

            SequenceSegment<byte> last = new SequenceSegment<byte>();
            last.SetMemory(new OwnedArray<byte>(data.Slice(5).ToArray()), 0, 5);

            SequenceSegment<byte> first = new SequenceSegment<byte>();
            first.SetMemory(new OwnedArray<byte>(data.Slice(0, 5).ToArray()), 0, 5);
            first.SetNext(last);

            ReadOnlySequence<byte> sequence = new ReadOnlySequence<byte>(first, first.Start, last, last.End);
            ByteBufferReader reader = new ByteBufferReader(sequence);

            reader.AdvanceToEnd();

            Assert.Equal(data.Length, reader.Length);
            Assert.Equal(data.Length, reader.Consumed);
            Assert.Equal(reader.Length, reader.Consumed);
            Assert.True(reader.End);
            Assert.Equal(0, reader.CurrentSpanIndex);
            Assert.Equal(sequence.End, reader.Position);
            Assert.Equal(0, reader.Remaining);
            Assert.True(default == reader.UnreadSpan);
            Assert.True(default == reader.CurrentSpan);

            // Rewind to second element
            reader.Rewind(9);

            Assert.Equal(1, reader.Consumed);
            Assert.False(reader.End);
            Assert.Equal(1, reader.CurrentSpanIndex);
            Assert.Equal(9, reader.Remaining);
            Assert.Equal(sequence.Slice(1), reader.UnreadSequence);

            // Consume next five elements and stop at second element of second segment
            reader.Advance(5);

            Assert.Equal(6, reader.Consumed);
            Assert.False(reader.End);
            Assert.Equal(1, reader.CurrentSpanIndex);
            Assert.Equal(4, reader.Remaining);
            Assert.Equal(sequence.Slice(6), reader.UnreadSequence);

            reader.AdvanceToEnd();

            Assert.Equal(data.Length, reader.Length);
            Assert.Equal(data.Length, reader.Consumed);
            Assert.Equal(reader.Length, reader.Consumed);
            Assert.True(reader.End);
            Assert.Equal(0, reader.CurrentSpanIndex);
            Assert.Equal(sequence.End, reader.Position);
            Assert.Equal(0, reader.Remaining);
            Assert.True(default == reader.UnreadSpan);
            Assert.True(default == reader.CurrentSpan);
        }

        [Fact]
        public void AdvanceTo_End_Multiple()
        {
            ReadOnlySpan<byte> data = (byte[])_inputData.Clone();

            SequenceSegment<byte> last = new SequenceSegment<byte>();
            last.SetMemory(new OwnedArray<byte>(data.Slice(5).ToArray()), 0, 5);

            SequenceSegment<byte> first = new SequenceSegment<byte>();
            first.SetMemory(new OwnedArray<byte>(data.Slice(0, 5).ToArray()), 0, 5);
            first.SetNext(last);

            ReadOnlySequence<byte> sequence = new ReadOnlySequence<byte>(first, first.Start, last, last.End);
            ByteBufferReader reader = new ByteBufferReader(sequence);

            reader.AdvanceToEnd();
            reader.AdvanceToEnd();
            reader.AdvanceToEnd();

            Assert.Equal(data.Length, reader.Length);
            Assert.Equal(data.Length, reader.Consumed);
            Assert.Equal(reader.Length, reader.Consumed);
            Assert.True(reader.End);
            Assert.Equal(0, reader.CurrentSpanIndex);
            Assert.Equal(sequence.End, reader.Position);
            Assert.Equal(0, reader.Remaining);
            Assert.True(default == reader.UnreadSpan);
            Assert.True(default == reader.CurrentSpan);
        }

        [Fact]
        public void UnreadSequence()
        {
            ReadOnlySpan<byte> data = (byte[])_inputData.Clone();

            SequenceSegment<byte> last = new SequenceSegment<byte>();
            last.SetMemory(new OwnedArray<byte>(data.Slice(5).ToArray()), 0, 5);

            SequenceSegment<byte> first = new SequenceSegment<byte>();
            first.SetMemory(new OwnedArray<byte>(data.Slice(0, 5).ToArray()), 0, 5);
            first.SetNext(last);

            ReadOnlySequence<byte> sequence = new ReadOnlySequence<byte>(first, first.Start, last, last.End);
            ByteBufferReader reader = new ByteBufferReader(sequence);

            Assert.Equal(sequence, reader.UnreadSequence);
            Assert.Equal(data.Length, reader.UnreadSequence.Length);
            Assert.True(reader.TryRead(out byte _));
            Assert.True(reader.TryRead(out byte _));
            Assert.Equal(sequence.Slice(2), reader.UnreadSequence);
            // Advance to the end
            reader.Advance(8);
            Assert.Equal(0, reader.UnreadSequence.Length);
        }

        [Fact]
        public void UnreadSequence_EmptySegment()
        {
            ReadOnlySpan<byte> data = (byte[])_inputData.Clone();

            // Empty segment
            SequenceSegment<byte> third = new SequenceSegment<byte>();

            SequenceSegment<byte> second = new SequenceSegment<byte>();
            second.SetMemory(new OwnedArray<byte>(data.Slice(5).ToArray()), 0, 5);
            second.SetNext(third);

            SequenceSegment<byte> first = new SequenceSegment<byte>();
            first.SetMemory(new OwnedArray<byte>(data.Slice(0, 5).ToArray()), 0, 5);
            first.SetNext(second);

            ReadOnlySequence<byte> sequence = new ReadOnlySequence<byte>(first, first.Start, third, third.End);
            ByteBufferReader reader = new ByteBufferReader(sequence);

            // Drain until the expected end of data with simple read
            for (int i = 0; i < data.Length; i++)
            {
                reader.TryRead(out byte _);
            }

            Assert.Equal(sequence.Slice(data.Length), reader.UnreadSequence);
            Assert.Equal(0, reader.UnreadSequence.Length);
            Assert.False(reader.TryRead(out byte _));
        }

        [Fact]
        public void CopyToSmallerBufferWorks()
        {
            byte[] content = (byte[])_inputData.Clone();

            Span<byte> buffer = new byte[content.Length];
            ByteBufferReader reader = new ByteBufferReader(Factory.CreateWithContent(content));

            // this loop skips more and more items in the reader
            for (int i = 0; i < content.Length; i++)
            {
                // this loop makes the destination buffer smaller and smaller
                for (int j = 0; j < buffer.Length - i; j++)
                {
                    Span<byte> bufferSlice = buffer.Slice(0, j);
                    bufferSlice.Clear();
                    Assert.True(reader.TryCopyTo(bufferSlice));
                    Assert.Equal(Math.Min(bufferSlice.Length, content.Length - i), bufferSlice.Length);

                    Assert.True(bufferSlice.SequenceEqual(content.AsSpan(i, j)));
                }

                reader.Advance(1);
            }
        }
    }
}
