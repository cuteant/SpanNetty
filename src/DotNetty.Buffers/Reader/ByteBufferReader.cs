// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// borrowed from https://github.com/dotnet/corefx/blob/release/3.0/src/System.Memory/src/System/Buffers/SequenceReader.cs

#if !NET40

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;

    public ref partial struct ByteBufferReader
    {
        private SequencePosition _currentPosition;
        private SequencePosition _nextPosition;
        private bool _moreData;
        private long _length;

        private readonly ReadOnlySequence<byte> _sequence;
        private ReadOnlySpan<byte> _currentSpan;
        private int _currentSpanIndex;
        private long _consumed;

        /// <summary>Create a <see cref="ByteBufferReader" /> over the given <see cref="IByteBuffer"/>.</summary>
        public ByteBufferReader(IByteBuffer buffer) : this(buffer.UnreadSequence) { }

        /// <summary>Create a <see cref="ByteBufferReader" /> over the given <see cref="ReadOnlySequence{T}"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ByteBufferReader(ReadOnlySequence<byte> buffer)
        {
            _currentSpanIndex = 0;
            _consumed = 0;
            _sequence = buffer;
            _currentPosition = buffer.Start;
            _length = -1;

            ByteBufferReaderHelper.GetFirstSpan(buffer, out ReadOnlySpan<byte> first, out _nextPosition);
            _currentSpan = first;
            _moreData = first.Length > 0;

            if (!buffer.IsSingleSegment && !_moreData)
            {
                _moreData = true;
                GetNextSpan();
            }
        }

        /// <summary>Return true if we're in the last segment.</summary>
        public bool IsLastSegment => _nextPosition.GetObject() == null;

        /// <summary>True when there is no more data in the <see cref="Sequence"/>.</summary>
        public bool End => !_moreData;

        /// <summary>The underlying <see cref="ReadOnlySequence{T}"/> for the reader.</summary>
        public ReadOnlySequence<byte> Sequence => _sequence;

        /// <summary>The current position in the <see cref="Sequence"/>.</summary>
        public SequencePosition Position
            => new SequencePosition(_currentPosition.GetObject(), _currentSpanIndex + (_currentPosition.GetInteger() & ~(1 << 31)));

        /// <summary>The current segment in the <see cref="Sequence"/>.</summary>
        public ReadOnlySpan<byte> CurrentSpan => _currentSpan;

        /// <summary>The index in the <see cref="CurrentSpan"/>.</summary>
        public int CurrentSpanIndex => _currentSpanIndex;

        /// <summary>The unread portion of the <see cref="CurrentSpan"/>.</summary>
        public ReadOnlySpan<byte> UnreadSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _currentSpan.Slice(_currentSpanIndex);
        }

        /// <summary>The total number of <see cref="byte"/>'s processed by the reader.</summary>
        public long Consumed => _consumed;

        /// <summary>Remaining <see cref="byte"/>'s in the reader's <see cref="Sequence"/>.</summary>
        public long Remaining => Length - _consumed;

        /// <summary>Count of <see cref="byte"/> in the reader's <see cref="Sequence"/>.</summary>
        public long Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_length < 0)
                {
                    // Cache the length
                    _length = _sequence.Length;
                }
                return _length;
            }
        }

        /// <summary>Peeks at the next value without advancing the reader.</summary>
        /// <param name="value">The next value or default if at the end.</param>
        /// <returns>False if at the end of the reader.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out byte value)
        {
            if (_moreData)
            {
                value = _currentSpan[_currentSpanIndex];
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <summary>Read the next value and advance the reader.</summary>
        /// <param name="value">The next value or default if at the end.</param>
        /// <returns>False if at the end of the reader.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRead(out byte value)
        {
            if (End)
            {
                value = default;
                return false;
            }

            value = _currentSpan[_currentSpanIndex];
            _currentSpanIndex++;
            _consumed++;

            if (_currentSpanIndex >= _currentSpan.Length)
            {
                GetNextSpan();
            }

            return true;
        }

        /// <summary>Move the reader back the specified number of items.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rewind(long count)
        {
            if (count < 0) { ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count); }

            _consumed -= count;

            if (_currentSpanIndex >= count)
            {
                _currentSpanIndex -= (int)count;
                _moreData = true;
            }
            else
            {
                // Current segment doesn't have enough data, scan backward through segments
                RetreatToPreviousSpan(_consumed);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RetreatToPreviousSpan(long consumed)
        {
            ResetReader();
            Advance(consumed);
        }

        private void ResetReader()
        {
            _currentSpanIndex = 0;
            _consumed = 0;
            _currentPosition = _sequence.Start;
            _nextPosition = _currentPosition;

            if (_sequence.TryGet(ref _nextPosition, out ReadOnlyMemory<byte> memory, advance: true))
            {
                _moreData = true;

                if (0u >= (uint)memory.Length)
                {
                    _currentSpan = default;
                    // No data in the first span, move to one with data
                    GetNextSpan();
                }
                else
                {
                    _currentSpan = memory.Span;
                }
            }
            else
            {
                // No data in any spans and at end of sequence
                _moreData = false;
                _currentSpan = default;
            }
        }

        /// <summary>Get the next segment with available data, if any.</summary>
        private void GetNextSpan()
        {
            if (!_sequence.IsSingleSegment)
            {
                SequencePosition previousNextPosition = _nextPosition;
                while (_sequence.TryGet(ref _nextPosition, out ReadOnlyMemory<byte> memory, advance: true))
                {
                    _currentPosition = previousNextPosition;
                    if (memory.Length > 0)
                    {
                        _currentSpan = memory.Span;
                        _currentSpanIndex = 0;
                        return;
                    }
                    else
                    {
                        _currentSpan = default;
                        _currentSpanIndex = 0;
                        previousNextPosition = _nextPosition;
                    }
                }
            }
            _moreData = false;
        }

        /// <summary>Move the reader ahead the specified number of items.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(long count)
        {
            const long TooBigOrNegative = unchecked((long)0xFFFFFFFF80000000);
            if (0u >= (uint)(count & TooBigOrNegative) && (_currentSpan.Length - _currentSpanIndex) > (int)count)
            {
                _currentSpanIndex += (int)count;
                _consumed += count;
            }
            else
            {
                // Can't satisfy from the current span
                AdvanceToNextSpan(count);
            }
        }

        /// <summary>Unchecked helper to avoid unnecessary checks where you know count is valid.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AdvanceCurrentSpan(long count)
        {
            Debug.Assert(count >= 0);

            _consumed += count;
            _currentSpanIndex += (int)count;
            if (_currentSpanIndex >= _currentSpan.Length) { GetNextSpan(); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AdvanceWithinSpan(long count)
        {
            // Only call this helper if you know that you are advancing in the current span
            // with valid count and there is no need to fetch the next one.
            Debug.Assert(count >= 0);

            _consumed += count;
            _currentSpanIndex += (int)count;

            Debug.Assert(_currentSpanIndex < _currentSpan.Length);
        }

        private void AdvanceToNextSpan(long count)
        {
            if (count < 0) { ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count); }

            _consumed += count;
            while (_moreData)
            {
                int remaining = _currentSpan.Length - _currentSpanIndex;

                if (remaining > count)
                {
                    _currentSpanIndex += (int)count;
                    count = 0;
                    break;
                }

                // As there may not be any further segments we need to
                // push the current index to the end of the span.
                _currentSpanIndex += remaining;
                count -= remaining;
                Debug.Assert(count >= 0);

                GetNextSpan();

                if (count == 0L) { break; }
            }

            if (count != 0)
            {
                // Not enough space left- adjust for where we actually ended and throw
                _consumed -= count;
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count);
            }
        }

        /// <summary>Copies data from the current <see cref="Position"/> to the given <paramref name="destination"/> span.</summary>
        /// <param name="destination">Destination to copy to.</param>
        /// <returns>True if there is enough data to copy to the <paramref name="destination"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCopyTo(Span<byte> destination)
        {
            ReadOnlySpan<byte> firstSpan = UnreadSpan;
            int destLen = destination.Length;
            if ((uint)firstSpan.Length >= (uint)destLen)
            {
                firstSpan.Slice(0, destLen).CopyTo(destination);
                return true;
            }

            return TryCopyMultisegment(destination);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal bool TryCopyMultisegment(Span<byte> destination)
        {
            int destinationLen = destination.Length;
            if (Remaining < destinationLen) { return false; }

            ReadOnlySpan<byte> firstSpan = UnreadSpan;
            Debug.Assert(firstSpan.Length < destinationLen);
            firstSpan.CopyTo(destination);
            int copied = firstSpan.Length;

            SequencePosition next = _nextPosition;
            while (_sequence.TryGet(ref next, out ReadOnlyMemory<byte> nextSegment, true))
            {
                if (nextSegment.Length > 0)
                {
                    ReadOnlySpan<byte> nextSpan = nextSegment.Span;
                    int toCopy = Math.Min(nextSpan.Length, destinationLen - copied);
                    nextSpan.Slice(0, toCopy).CopyTo(destination.Slice(copied));
                    copied += toCopy;
                    if ((uint)copied >= (uint)destinationLen) { break; }
                }
            }

            return true;
        }
    }
}

#endif