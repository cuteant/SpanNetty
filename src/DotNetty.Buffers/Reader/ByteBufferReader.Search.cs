// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// borrowed from https://github.com/dotnet/corefx/blob/release/3.0/src/System.Memory/src/System/Buffers/SequenceReader.Search.cs

#if !NET40

namespace DotNetty.Buffers
{
    using System;
    using System.Buffers;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using DotNetty.Common.Internal;

    public ref partial struct ByteBufferReader
    {
        private const int IndexNotFound = -1;
        private const uint NIndexNotFound = unchecked((uint)IndexNotFound);

        /// <summary>Try to read everything up to the given <paramref name="delimiter"/>.</summary>
        /// <param name="span">The read data, if any.</param>
        /// <param name="delimiter">The delimiter to look for.</param>
        /// <param name="advancePastDelimiter">True to move past the <paramref name="delimiter"/> if found.</param>
        /// <returns>True if the <paramref name="delimiter"/> was found.</returns>
        public bool TryReadTo(out ReadOnlySpan<byte> span, byte delimiter, bool advancePastDelimiter = true)
        {
            ReadOnlySpan<byte> remaining = UnreadSpan;
            int index = remaining.IndexOf(delimiter);

            uint uIndex = (uint)index;
            if (uIndex < NIndexNotFound)
            {
                span = 0u >= uIndex ? default : remaining.Slice(0, index);
                AdvanceCurrentSpan(index + (advancePastDelimiter ? 1 : 0));
                return true;
            }

            return TryReadToSlow(out span, delimiter, advancePastDelimiter);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryReadToSlow(out ReadOnlySpan<byte> span, byte delimiter, bool advancePastDelimiter)
        {
            if (!TryReadToInternal(out ReadOnlySequence<byte> sequence, delimiter, advancePastDelimiter, _currentSpan.Length - _currentSpanIndex))
            {
                span = default;
                return false;
            }

            span = sequence.IsSingleSegment ? sequence.First.Span : sequence.ToArray();
            return true;
        }

        /// <summary>Try to read everything up to the given <paramref name="delimiter"/>, ignoring delimiters that are
        /// preceded by <paramref name="delimiterEscape"/>.</summary>
        /// <param name="span">The read data, if any.</param>
        /// <param name="delimiter">The delimiter to look for.</param>
        /// <param name="delimiterEscape">If found prior to <paramref name="delimiter"/> it will skip that occurrence.</param>
        /// <param name="advancePastDelimiter">True to move past the <paramref name="delimiter"/> if found.</param>
        /// <returns>True if the <paramref name="delimiter"/> was found.</returns>
        public bool TryReadTo(out ReadOnlySpan<byte> span, byte delimiter, byte delimiterEscape, bool advancePastDelimiter = true)
        {
            ReadOnlySpan<byte> remaining = UnreadSpan;
            int index = remaining.IndexOf(delimiter);

            if ((index > 0 && !remaining[index - 1].Equals(delimiterEscape)) || 0u >= (uint)index)
            {
                span = remaining.Slice(0, index);
                AdvanceCurrentSpan(index + (advancePastDelimiter ? 1 : 0));
                return true;
            }

            // This delimiter might be skipped, go down the slow path
            return TryReadToSlow(out span, delimiter, delimiterEscape, index, advancePastDelimiter);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryReadToSlow(out ReadOnlySpan<byte> span, byte delimiter, byte delimiterEscape, int index, bool advancePastDelimiter)
        {
            if (!TryReadToSlow(out ReadOnlySequence<byte> sequence, delimiter, delimiterEscape, index, advancePastDelimiter))
            {
                span = default;
                return false;
            }

            Debug.Assert(sequence.Length > 0);
            span = sequence.IsSingleSegment ? sequence.First.Span : sequence.ToArray();
            return true;
        }

        private bool TryReadToSlow(out ReadOnlySequence<byte> sequence, byte delimiter, byte delimiterEscape, int index, bool advancePastDelimiter)
        {
            ByteBufferReader copy = this;

            ReadOnlySpan<byte> remaining = UnreadSpan;
            bool priorEscape = false;

            do
            {
                uint uIndex = (uint)index;
                if (uIndex < NIndexNotFound) // index >= 0
                {
                    if (0u >= uIndex && priorEscape) // index == 0
                    {
                        // We were in the escaped state, so skip this delimiter
                        priorEscape = false;
                        Advance(index + 1);
                        remaining = UnreadSpan;
                        goto Continue;
                    }
                    else if (index > 0 && remaining[index - 1].Equals(delimiterEscape))
                    {
                        // This delimiter might be skipped

                        // Count our escapes
                        int escapeCount = 1;
                        //int i = index - 2;
                        //for (; i >= 0; i--)
                        //{
                        //    if (!remaining[i].Equals(delimiterEscape)) { break; }
                        //}
                        var result = PlatformDependent.ForEachByteDesc(
                                ref MemoryMarshal.GetReference(remaining),
                                new ByteBufferReaderHelper.IndexNotOfProcessor(delimiterEscape),
                                index - 1);
                        int i = (uint)result < NIndexNotFound ? result : IndexNotFound;
                        if (i < 0 && priorEscape)
                        {
                            // Started and ended with escape, increment once more
                            escapeCount++;
                        }
                        escapeCount += index - 2 - i;

                        if ((escapeCount & 1) != 0)
                        {
                            // An odd escape count means we're currently escaped,
                            // skip the delimiter and reset escaped state.
                            Advance(index + 1);
                            priorEscape = false;
                            remaining = UnreadSpan;
                            goto Continue;
                        }
                    }

                    // Found the delimiter. Move to it, slice, then move past it.
                    AdvanceCurrentSpan(index);

                    sequence = _sequence.Slice(copy.Position, Position);
                    if (advancePastDelimiter)
                    {
                        Advance(1);
                    }
                    return true;
                }
                else
                {
                    // No delimiter, need to check the end of the span for odd number of escapes then advance
                    var remainingLen = remaining.Length;
                    if (remainingLen > 0 && remaining[remainingLen - 1].Equals(delimiterEscape))
                    {
                        int escapeCount = 1;
                        //int i = remainingLen - 2;
                        //for (; i >= 0; i--)
                        //{
                        //    if (!remaining[i].Equals(delimiterEscape)) { break; }
                        //}
                        var result = PlatformDependent.ForEachByteDesc(
                                ref MemoryMarshal.GetReference(remaining),
                                new ByteBufferReaderHelper.IndexNotOfProcessor(delimiterEscape),
                                remainingLen - 1);
                        int i = (uint)result < NIndexNotFound ? result : IndexNotFound;

                        escapeCount += remainingLen - 2 - i;
                        if (i < 0 && priorEscape)
                        {
                            priorEscape = (escapeCount & 1) == 0;   // equivalent to incrementing escapeCount before setting priorEscape
                        }
                        else
                        {
                            priorEscape = (escapeCount & 1) != 0;
                        }
                    }
                    else
                    {
                        priorEscape = false;
                    }
                }

                // Nothing in the current span, move to the end, checking for the skip delimiter
                AdvanceCurrentSpan(remaining.Length);
                remaining = _currentSpan;

            Continue:
                index = remaining.IndexOf(delimiter);
            } while (!End);

            // Didn't find anything, reset our original state.
            this = copy;
            sequence = default;
            return false;
        }

        /// <summary>Try to read everything up to the given <paramref name="delimiter"/>.</summary>
        /// <param name="sequence">The read data, if any.</param>
        /// <param name="delimiter">The delimiter to look for.</param>
        /// <param name="advancePastDelimiter">True to move past the <paramref name="delimiter"/> if found.</param>
        /// <returns>True if the <paramref name="delimiter"/> was found.</returns>
        public bool TryReadTo(out ReadOnlySequence<byte> sequence, byte delimiter, bool advancePastDelimiter = true)
        {
            return TryReadToInternal(out sequence, delimiter, advancePastDelimiter);
        }

        private bool TryReadToInternal(out ReadOnlySequence<byte> sequence, byte delimiter, bool advancePastDelimiter, int skip = 0)
        {
            Debug.Assert(skip >= 0);
            ByteBufferReader copy = this;
            if (skip > 0) { Advance(skip); }
            ReadOnlySpan<byte> remaining = UnreadSpan;

            while (_moreData)
            {
                int index = remaining.IndexOf(delimiter);
                if ((uint)index < NIndexNotFound)
                {
                    // Found the delimiter. Move to it, slice, then move past it.
                    if (index > 0)
                    {
                        AdvanceCurrentSpan(index);
                    }

                    sequence = _sequence.Slice(copy.Position, Position);
                    if (advancePastDelimiter)
                    {
                        Advance(1);
                    }
                    return true;
                }

                AdvanceCurrentSpan(remaining.Length);
                remaining = _currentSpan;
            }

            // Didn't find anything, reset our original state.
            this = copy;
            sequence = default;
            return false;
        }

        /// <summary>Try to read everything up to the given <paramref name="delimiter"/>, ignoring delimiters that are
        /// preceded by <paramref name="delimiterEscape"/>.</summary>
        /// <param name="sequence">The read data, if any.</param>
        /// <param name="delimiter">The delimiter to look for.</param>
        /// <param name="delimiterEscape">If found prior to <paramref name="delimiter"/> it will skip that occurrence.</param>
        /// <param name="advancePastDelimiter">True to move past the <paramref name="delimiter"/> if found.</param>
        /// <returns>True if the <paramref name="delimiter"/> was found.</returns>
        public bool TryReadTo(out ReadOnlySequence<byte> sequence, byte delimiter, byte delimiterEscape, bool advancePastDelimiter = true)
        {
            ByteBufferReader copy = this;

            ReadOnlySpan<byte> remaining = UnreadSpan;
            bool priorEscape = false;

            while (_moreData)
            {
                int index = remaining.IndexOf(delimiter);
                uint uIndex = (uint)index;
                if (uIndex < NIndexNotFound) // index != -1
                {
                    if (0u >= uIndex && priorEscape) // index == 0
                    {
                        // We were in the escaped state, so skip this delimiter
                        priorEscape = false;
                        Advance(index + 1);
                        remaining = UnreadSpan;
                        continue;
                    }
                    else if (index > 0 && remaining[index - 1].Equals(delimiterEscape))
                    {
                        // This delimiter might be skipped

                        // Count our escapes
                        //int escapeCount = 0;
                        //for (int i = index; i > 0 && remaining[i - 1].Equals(delimiterEscape); i--, escapeCount++) { }
                        var result = PlatformDependent.ForEachByteDesc(
                                ref MemoryMarshal.GetReference(remaining),
                                new ByteBufferReaderHelper.IndexNotOfProcessor(delimiterEscape),
                                index);
                        int escapeCount = (uint)result < NIndexNotFound ? index - result - 1 : index;

                        if (escapeCount == index && priorEscape)
                        {
                            // Started and ended with escape, increment once more
                            escapeCount++;
                        }

                        priorEscape = false;
                        if ((escapeCount & 1) != 0)
                        {
                            // Odd escape count means we're in the escaped state, so skip this delimiter
                            Advance(index + 1);
                            remaining = UnreadSpan;
                            continue;
                        }
                    }

                    // Found the delimiter. Move to it, slice, then move past it.
                    if (index > 0) { Advance(index); }

                    sequence = _sequence.Slice(copy.Position, Position);
                    if (advancePastDelimiter) { Advance(1); }
                    return true;
                }

                // No delimiter, need to check the end of the span for odd number of escapes then advance
                {
                    var remainingLen = remaining.Length;
                    //int escapeCount = 0;
                    //for (int i = remainingLen; i > 0 && remaining[i - 1].Equals(delimiterEscape); i--, escapeCount++) { }
                    var result = PlatformDependent.ForEachByteDesc(
                            ref MemoryMarshal.GetReference(remaining),
                            new ByteBufferReaderHelper.IndexNotOfProcessor(delimiterEscape),
                            remainingLen);
                    int escapeCount = (uint)result < NIndexNotFound ? remainingLen - result - 1 : remainingLen;

                    if (priorEscape && escapeCount == remainingLen)
                    {
                        escapeCount++;
                    }
                    priorEscape = escapeCount % 2 != 0;
                }

                // Nothing in the current span, move to the end, checking for the skip delimiter
                Advance(remaining.Length);
                remaining = _currentSpan;
            }

            // Didn't find anything, reset our original state.
            this = copy;
            sequence = default;
            return false;
        }

        /// <summary>Try to read everything up to the given <paramref name="delimiters"/>.</summary>
        /// <param name="span">The read data, if any.</param>
        /// <param name="delimiters">The delimiters to look for.</param>
        /// <param name="advancePastDelimiter">True to move past the first found instance of any of the given <paramref name="delimiters"/>.</param>
        /// <returns>True if any of the <paramref name="delimiters"/> were found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReadToAny(out ReadOnlySpan<byte> span, ReadOnlySpan<byte> delimiters, bool advancePastDelimiter = true)
        {
            ReadOnlySpan<byte> remaining = UnreadSpan;
            int index = delimiters.Length == 2
                ? remaining.IndexOfAny(delimiters[0], delimiters[1])
                : remaining.IndexOfAny(delimiters);

            if ((uint)index < NIndexNotFound)
            {
                span = remaining.Slice(0, index);
                Advance(index + (advancePastDelimiter ? 1 : 0));
                return true;
            }

            return TryReadToAnySlow(out span, delimiters, advancePastDelimiter);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryReadToAnySlow(out ReadOnlySpan<byte> span, ReadOnlySpan<byte> delimiters, bool advancePastDelimiter)
        {
            if (!TryReadToAnyInternal(out ReadOnlySequence<byte> sequence, delimiters, advancePastDelimiter, _currentSpan.Length - _currentSpanIndex))
            {
                span = default;
                return false;
            }

            span = sequence.IsSingleSegment ? sequence.First.Span : sequence.ToArray();
            return true;
        }

        /// <summary>Try to read everything up to the given <paramref name="delimiters"/>.</summary>
        /// <param name="sequence">The read data, if any.</param>
        /// <param name="delimiters">The delimiters to look for.</param>
        /// <param name="advancePastDelimiter">True to move past the first found instance of any of the given <paramref name="delimiters"/>.</param>
        /// <returns>True if any of the <paramref name="delimiters"/> were found.</returns>
        public bool TryReadToAny(out ReadOnlySequence<byte> sequence, ReadOnlySpan<byte> delimiters, bool advancePastDelimiter = true)
        {
            return TryReadToAnyInternal(out sequence, delimiters, advancePastDelimiter);
        }

        private bool TryReadToAnyInternal(out ReadOnlySequence<byte> sequence, ReadOnlySpan<byte> delimiters, bool advancePastDelimiter, int skip = 0)
        {
            ByteBufferReader copy = this;
            if (skip > 0) { Advance(skip); }
            ReadOnlySpan<byte> remaining = UnreadSpan;

            while (!End)
            {
                int index = delimiters.Length == 2
                    ? remaining.IndexOfAny(delimiters[0], delimiters[1])
                    : remaining.IndexOfAny(delimiters);

                if ((uint)index < NIndexNotFound)
                {
                    // Found one of the delimiters. Move to it, slice, then move past it.
                    if (index > 0) { AdvanceCurrentSpan(index); }

                    sequence = _sequence.Slice(copy.Position, Position);
                    if (advancePastDelimiter) { Advance(1); }
                    return true;
                }

                Advance(remaining.Length);
                remaining = _currentSpan;
            }

            // Didn't find anything, reset our original state.
            this = copy;
            sequence = default;
            return false;
        }

        /// <summary>Try to read data until the entire given <paramref name="delimiter"/> matches.</summary>
        /// <param name="sequence">The read data, if any.</param>
        /// <param name="delimiter">The multi (byte) delimiter.</param>
        /// <param name="advancePastDelimiter">True to move past the <paramref name="delimiter"/> if found.</param>
        /// <returns>True if the <paramref name="delimiter"/> was found.</returns>
        public bool TryReadTo(out ReadOnlySequence<byte> sequence, ReadOnlySpan<byte> delimiter, bool advancePastDelimiter = true)
        {
            if (delimiter.Length == 0)
            {
                sequence = default;
                return true;
            }

            ByteBufferReader copy = this;

            bool advanced = false;
            while (!End)
            {
                if (!TryReadTo(out sequence, delimiter[0], advancePastDelimiter: false))
                {
                    this = copy;
                    return false;
                }

                if (delimiter.Length == 1)
                {
                    if (advancePastDelimiter)
                    {
                        Advance(1);
                    }
                    return true;
                }

                if (IsNext(delimiter))
                {
                    // Probably a faster way to do this, potentially by avoiding the Advance in the previous TryReadTo call
                    if (advanced)
                    {
                        sequence = copy._sequence.Slice(copy._consumed, _consumed - copy._consumed);
                    }

                    if (advancePastDelimiter)
                    {
                        Advance(delimiter.Length);
                    }
                    return true;
                }
                else
                {
                    Advance(1);
                    advanced = true;
                }
            }

            this = copy;
            sequence = default;
            return false;
        }

        /// <summary>Advance until the given <paramref name="delimiter"/>, if found.</summary>
        /// <param name="delimiter">The delimiter to search for.</param>
        /// <param name="advancePastDelimiter">True to move past the <paramref name="delimiter"/> if found.</param>
        /// <returns>True if the given <paramref name="delimiter"/> was found.</returns>
        public bool TryAdvanceTo(byte delimiter, bool advancePastDelimiter = true)
        {
            ReadOnlySpan<byte> remaining = UnreadSpan;
            int index = remaining.IndexOf(delimiter);
            if ((uint)index < NIndexNotFound)
            {
                Advance(advancePastDelimiter ? index + 1 : index);
                return true;
            }

            return TryReadToInternal(out _, delimiter, advancePastDelimiter);
        }

        /// <summary>Advance until any of the given <paramref name="delimiters"/>, if found.</summary>
        /// <param name="delimiters">The delimiters to search for.</param>
        /// <param name="advancePastDelimiter">True to move past the first found instance of any of the given <paramref name="delimiters"/>.</param>
        /// <returns>True if any of the given <paramref name="delimiters"/> were found.</returns>
        public bool TryAdvanceToAny(ReadOnlySpan<byte> delimiters, bool advancePastDelimiter = true)
        {
            ReadOnlySpan<byte> remaining = UnreadSpan;
            int index = remaining.IndexOfAny(delimiters);
            if ((uint)index < NIndexNotFound)
            {
                AdvanceCurrentSpan(index + (advancePastDelimiter ? 1 : 0));
                return true;
            }

            return TryReadToAnyInternal(out _, delimiters, advancePastDelimiter);
        }

        /// <summary>Advance past consecutive instances of the given <paramref name="value"/>.</summary>
        /// <returns>How many positions the reader has been advanced.</returns>
        public long AdvancePast(byte value)
        {
            long start = _consumed;

            do
            {
                // Advance past all matches in the current span
                //int i;
                //for (i = CurrentSpanIndex; i < CurrentSpan.Length && CurrentSpan[i].Equals(value); i++) { }
                //int advanced = i - CurrentSpanIndex;

                var searchSpan = _currentSpan.Slice(_currentSpanIndex);
                var result = PlatformDependent.ForEachByte(
                        ref MemoryMarshal.GetReference(searchSpan),
                        new ByteBufferReaderHelper.PastValueProcessor(value),
                        searchSpan.Length);
                int advanced = (uint)result < NIndexNotFound ? result : _currentSpan.Length - _currentSpanIndex;
                if (advanced == 0)
                {
                    // Didn't advance at all in this span, exit.
                    break;
                }

                AdvanceCurrentSpan(advanced);

                // If we're at postion 0 after advancing and not at the End,
                // we're in a new span and should continue the loop.
            } while (_currentSpanIndex == 0 && !End);

            return _consumed - start;
        }

        /// <summary>Skip consecutive instances of any of the given <paramref name="values"/>.</summary>
        /// <returns>How many positions the reader has been advanced.</returns>
        public long AdvancePastAny(ReadOnlySpan<byte> values)
        {
            long start = _consumed;

            do
            {
                // Advance past all matches in the current span
                var searchSpan = _currentSpan.Slice(_currentSpanIndex);
                var result = ByteBufferReaderHelper.PastAny(
                        ref MemoryMarshal.GetReference(searchSpan),
                        searchSpan.Length,
                        ref MemoryMarshal.GetReference(values),
                        values.Length);
                int advanced = (uint)result < NIndexNotFound ? _currentSpanIndex + result : _currentSpan.Length - _currentSpanIndex;
                if (advanced == 0)
                {
                    // Didn't advance at all in this span, exit.
                    break;
                }

                AdvanceCurrentSpan(advanced);

                // If we're at postion 0 after advancing and not at the End,
                // we're in a new span and should continue the loop.
            } while (_currentSpanIndex == 0 && !End);

            return _consumed - start;
        }

        /// <summary>Advance past consecutive instances of any of the given values.</summary>
        /// <returns>How many positions the reader has been advanced.</returns>
        public long AdvancePastAny(byte value0, byte value1, byte value2, byte value3)
        {
            long start = _consumed;

            do
            {
                // Advance past all matches in the current span
                var searchSpan = _currentSpan.Slice(_currentSpanIndex);
                var result = PlatformDependent.ForEachByte(
                        ref MemoryMarshal.GetReference(searchSpan),
                        new ByteBufferReaderHelper.PastValue4Processor(value0, value1, value2, value3),
                        searchSpan.Length);
                int advanced = (uint)result < NIndexNotFound ? result : _currentSpan.Length - _currentSpanIndex;
                if (advanced == 0)
                {
                    // Didn't advance at all in this span, exit.
                    break;
                }

                AdvanceCurrentSpan(advanced);

                // If we're at postion 0 after advancing and not at the End,
                // we're in a new span and should continue the loop.
            } while (_currentSpanIndex == 0 && !End);

            return _consumed - start;
        }

        /// <summary>Advance past consecutive instances of any of the given values.</summary>
        /// <returns>How many positions the reader has been advanced.</returns>
        public long AdvancePastAny(byte value0, byte value1, byte value2)
        {
            long start = _consumed;

            do
            {
                // Advance past all matches in the current span
                var searchSpan = _currentSpan.Slice(_currentSpanIndex);
                var result = PlatformDependent.ForEachByte(
                        ref MemoryMarshal.GetReference(searchSpan),
                        new ByteBufferReaderHelper.PastValue3Processor(value0, value1, value2),
                        searchSpan.Length);
                int advanced = (uint)result < NIndexNotFound ? result : _currentSpan.Length - _currentSpanIndex;
                if (advanced == 0)
                {
                    // Didn't advance at all in this span, exit.
                    break;
                }

                AdvanceCurrentSpan(advanced);

                // If we're at postion 0 after advancing and not at the End,
                // we're in a new span and should continue the loop.
            } while (_currentSpanIndex == 0 && !End);

            return _consumed - start;
        }

        /// <summary>Advance past consecutive instances of any of the given values.</summary>
        /// <returns>How many positions the reader has been advanced.</returns>
        public long AdvancePastAny(byte value0, byte value1)
        {
            long start = _consumed;

            do
            {
                // Advance past all matches in the current span
                var searchSpan = _currentSpan.Slice(_currentSpanIndex);
                var result = PlatformDependent.ForEachByte(
                        ref MemoryMarshal.GetReference(searchSpan),
                        new ByteBufferReaderHelper.PastValue2Processor(value0, value1),
                        searchSpan.Length);
                int advanced = (uint)result < NIndexNotFound ? result : _currentSpan.Length - _currentSpanIndex;
                if (advanced == 0)
                {
                    // Didn't advance at all in this span, exit.
                    break;
                }

                AdvanceCurrentSpan(advanced);

                // If we're at postion 0 after advancing and not at the End,
                // we're in a new span and should continue the loop.
            } while (_currentSpanIndex == 0 && !End);

            return _consumed - start;
        }

        /// <summary>Check to see if the given <paramref name="next"/> value is next.</summary>
        /// <param name="next">The value to compare the next items to.</param>
        /// <param name="advancePast">Move past the <paramref name="next"/> value if found.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNext(byte next, bool advancePast = false)
        {
            if (End) { return false; }

            if (_currentSpan[_currentSpanIndex].Equals(next))
            {
                if (advancePast) { AdvanceCurrentSpan(1); }
                return true;
            }
            return false;
        }

        /// <summary>Check to see if the given <paramref name="next"/> values are next.</summary>
        /// <param name="next">The span to compare the next items to.</param>
        /// <param name="advancePast">Move past the <paramref name="next"/> values if found.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNext(ReadOnlySpan<byte> next, bool advancePast = false)
        {
            ReadOnlySpan<byte> unread = UnreadSpan;
            if (unread.StartsWith(next))
            {
                if (advancePast) { AdvanceCurrentSpan(next.Length); }
                return true;
            }

            // Only check the slow path if there wasn't enough to satisfy next
            return unread.Length < next.Length && IsNextSlow(next, advancePast);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private unsafe bool IsNextSlow(ReadOnlySpan<byte> next, bool advancePast)
        {
            ReadOnlySpan<byte> currentSpan = UnreadSpan;

            // We should only come in here if we need more data than we have in our current span
            Debug.Assert(currentSpan.Length < next.Length);

            int fullLength = next.Length;
            SequencePosition nextPosition = _nextPosition;

            while (next.StartsWith(currentSpan))
            {
                if (next.Length == currentSpan.Length)
                {
                    // Fully matched
                    if (advancePast) { Advance(fullLength); }
                    return true;
                }

                // Need to check the next segment
                while (true)
                {
                    if (!_sequence.TryGet(ref nextPosition, out ReadOnlyMemory<byte> nextSegment, advance: true))
                    {
                        // Nothing left
                        return false;
                    }

                    if (nextSegment.Length > 0)
                    {
                        next = next.Slice(currentSpan.Length);
                        currentSpan = nextSegment.Span;
                        if (currentSpan.Length > next.Length)
                        {
                            currentSpan = currentSpan.Slice(0, next.Length);
                        }
                        break;
                    }
                }
            }

            return false;
        }
    }
}

#endif