// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using DotNetty.Common.Internal;

    public sealed partial class StringBuilderCharSequence : ICharSequence, IEquatable<StringBuilderCharSequence>
    {
        internal readonly StringBuilder builder;
        readonly int offset;
        int size;

        public StringBuilderCharSequence(int capacity = 0)
        {
            if ((uint)capacity > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(capacity, ExceptionArgument.capacity); }

            this.builder = new StringBuilder(capacity);
            this.offset = 0;
            this.size = 0;
        }

        public StringBuilderCharSequence(StringBuilder builder) : this(builder, 0, builder.Length)
        {
        }

        public StringBuilderCharSequence(StringBuilder builder, int offset, int count)
        {
            if (builder is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.builder); }
            if (MathUtil.IsOutOfBounds(offset, count, builder.Length))
            {
                ThrowHelper.ThrowIndexOutOfRangeException_Index(offset, count, builder.Length);
            }

            this.builder = builder;
            this.offset = offset;
            this.size = count;
        }

        public ICharSequence SubSequence(int start) => this.SubSequence(start, this.size);

        public ICharSequence SubSequence(int start, int end)
        {
            if ((uint)start > SharedConstants.TooBigOrNegative)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_StartIndex(ExceptionArgument.start);
            }
            if (end < start)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_EndIndexLessThanStartIndex();
            }
            if (end > this.size)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException_IndexLargerThanLength(ExceptionArgument.end);
            }

            return end == start
                ? new StringBuilderCharSequence()
                : new StringBuilderCharSequence(this.builder, this.offset + start, end - start);
        }

        public int Count => this.size;

        public char this[int index]
        {
            get
            {
                var uIdx = (uint)index;
                if (uIdx > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(index, ExceptionArgument.index); }
                if (uIdx >= (uint)this.size) { ThrowHelper.ThrowArgumentOutOfRangeException_IndexLargerThanLength(ExceptionArgument.index); }
                return this.builder[this.offset + index];
            }
        }

        public void Append(string value)
        {
            this.builder.Append(value);
            this.size += value.Length;
        }

        public void Append(string value, int index, int count)
        {
            this.builder.Append(value, index, count);
            this.size += count;
        }

        public void Append(ICharSequence value)
        {
            if (value == null || 0u >= (uint)value.Count)
            {
                return;
            }

            this.builder.Append(value);
            this.size += value.Count;
        }

        public void Append(ICharSequence value, int index, int count)
        {
            if (value == null || 0u >= (uint)count)
            {
                return;
            }

            this.Append(value.SubSequence(index, index + count));
        }

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
        public void Append(in ReadOnlySpan<char> value)
        {
            this.builder.Append(value);
            this.size += value.Length;
        }

        public void Append(in ReadOnlyMemory<char> value)
        {
            this.builder.Append(value);
            this.size += value.Length;
        }
#endif

        public void Append(char value)
        {
            this.builder.Append(value);
            this.size++;
        }

        public void Insert(int start, char value)
        {
            if (start < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(start, ExceptionArgument.start); }
            if (start >= this.size) { ThrowHelper.ThrowArgumentOutOfRangeException_IndexLargerThanLength(ExceptionArgument.start); }

            this.builder.Insert(this.offset + start, value);
            this.size++;
        }

        public bool RegionMatches(int thisStart, ICharSequence seq, int start, int length) =>
            CharUtil.RegionMatches(this, this.offset + thisStart, seq, start, length);

        public bool RegionMatchesIgnoreCase(int thisStart, ICharSequence seq, int start, int length) =>
            CharUtil.RegionMatchesIgnoreCase(this, this.offset + thisStart, seq, start, length);

        public int IndexOf(char ch, int start = 0) => CharUtil.IndexOf(this, ch, start);

        public string ToString(int start)
        {
            var uStart = (uint)start;
            if (uStart > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(start, ExceptionArgument.start); }
            if (uStart >= (uint)this.size) { ThrowHelper.ThrowArgumentOutOfRangeException_IndexLargerThanLength(ExceptionArgument.start); }

            return this.builder.ToString(this.offset + start, this.size);
        }

        public override string ToString() => 0u >= (uint)this.size ? string.Empty : this.ToString(0);

        public bool Equals(StringBuilderCharSequence other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other is object && this.size == other.size && string.Equals(this.builder.ToString(this.offset, this.size), other.builder.ToString(other.offset, this.size)
#if NETCOREAPP_3_0_GREATER || NETSTANDARD_2_0_GREATER
                );
#else
                , StringComparison.Ordinal);
#endif
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            switch (obj)
            {
                case StringBuilderCharSequence other:
                    return this.size == other.size && string.Equals(this.builder.ToString(this.offset, this.size), other.builder.ToString(other.offset, this.size)
#if NETCOREAPP_3_0_GREATER || NETSTANDARD_2_0_GREATER
                        );
#else
                        , StringComparison.Ordinal);
#endif
                case ICharSequence seq:
                    return this.ContentEquals(seq);
                default:
                    return false;
            }
        }

        public int HashCode(bool ignoreCase) => ignoreCase
            ? StringComparer.OrdinalIgnoreCase.GetHashCode(this.ToString())
            : StringComparer.Ordinal.GetHashCode(this.ToString());

        public override int GetHashCode() => this.HashCode(true);

        public bool ContentEquals(ICharSequence other) => CharUtil.ContentEquals(this, other);

        public bool ContentEqualsIgnoreCase(ICharSequence other) => CharUtil.ContentEqualsIgnoreCase(this, other);

        public IEnumerator<char> GetEnumerator() => new CharSequenceEnumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
