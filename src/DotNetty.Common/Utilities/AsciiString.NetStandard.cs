// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using DotNetty.Common.Internal;

    partial class AsciiString : IHasAsciiSpan, IHasUtf16Span
    {
        public ReadOnlySpan<byte> AsciiSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var count = this.length;
                if (0u >= (uint)count) { return ReadOnlySpan<byte>.Empty; }
                return new ReadOnlySpan<byte>(this.value, this.offset, count);
            }
        }

        public ReadOnlySpan<char> Utf16Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (0u >= (uint)this.length) { return ReadOnlySpan<char>.Empty; }
                var thisValue = this.stringValue;
                if (thisValue != null) { return thisValue.AsSpan(); }
                return this.ToString().AsSpan();
            }
        }

        public int ForEachByte(IByteProcessor visitor) => this.ForEachByte0(0, this.length, visitor);

        public int ForEachByte(int index, int count, IByteProcessor visitor)
        {
            if (MathUtil.IsOutOfBounds(index, count, this.length))
            {
                ThrowIndexOutOfRangeException_Index(index, count, this.length);
            }
            return this.ForEachByte0(index, count, visitor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int ForEachByte0(int index, int count, IByteProcessor visitor)
        {
            return PlatformDependent.ForEachByte(ref this.value[this.offset + index], visitor, count);
        }

        public int ForEachByteDesc(IByteProcessor visitor) => this.ForEachByteDesc0(0, this.length, visitor);

        public int ForEachByteDesc(int index, int count, IByteProcessor visitor)
        {
            if (MathUtil.IsOutOfBounds(index, count, this.length))
            {
                ThrowIndexOutOfRangeException_Index(index, count, this.length);
            }

            return this.ForEachByteDesc0(index, count, visitor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int ForEachByteDesc0(int index, int count, IByteProcessor visitor)
        {
            return PlatformDependent.ForEachByteDesc(ref this.value[this.offset + index], visitor, count);
        }

        public int CompareTo(ICharSequence other)
        {
            if (ReferenceEquals(this, other)) { return 0; }

            if (other is IHasAsciiSpan ascii)
            {
                return this.AsciiSpan.SequenceCompareTo(ascii.AsciiSpan);
            }

            if (other is IHasUtf16Span utf16Span)
            {
                return this.Utf16Span.SequenceCompareTo(utf16Span.Utf16Span);
            }

            return CompareTo0(other);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int CompareTo0(ICharSequence other)
        {
            int length1 = this.length;
            int length2 = other.Count;
            int minLength = Math.Min(length1, length2);
            for (int i = 0, j = this.offset; i < minLength; i++, j++)
            {
                int result = ByteToChar(this.value[j]) - other[i];
                if (result != 0)
                {
                    return result;
                }
            }

            return length1 - length2;
        }

        public AsciiString Concat(ICharSequence charSequence)
        {
            int thisLen = this.length;
            int thatLen = charSequence.Count;
            if (thatLen == 0) { return this; }

            if (this.IsEmpty)
            {
                return charSequence is AsciiString asciiStr ? asciiStr : new AsciiString(charSequence);
            }

            if (charSequence is IHasAsciiSpan that)
            {
                var newValue = new byte[thisLen + thatLen];
                var span = new Span<byte>(newValue);
                this.AsciiSpan.CopyTo(span);
                that.AsciiSpan.CopyTo(span.Slice(thisLen));
                return new AsciiString(newValue, false);
            }

            return Concat0(charSequence);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private AsciiString Concat0(ICharSequence charSequence)
        {
            int thisLen = this.length;
            int thatLen = charSequence.Count;

            var newValue = new byte[thisLen + thatLen];
            PlatformDependent.CopyMemory(this.value, this.offset, newValue, 0, thisLen);
            for (int i = thisLen, j = 0; i < newValue.Length; i++, j++)
            {
                newValue[i] = CharToByte(charSequence[j]);
            }

            return new AsciiString(newValue, false);
        }

        public bool ContentEquals(ICharSequence other)
        {
            if (ReferenceEquals(this, other)) { return true; }

            if (null == other || this.length != other.Count) { return false; }

            if (other is AsciiString asciiStr)
            {
                return this.GetHashCode() == asciiStr.GetHashCode()
                    && this.AsciiSpan.SequenceEqual(asciiStr.AsciiSpan);
            }

            if (other is IHasAsciiSpan comparand)
            {
                return this.AsciiSpan.SequenceEqual(comparand.AsciiSpan);
            }

            if (other is IHasUtf16Span hasUtf16)
            {
                return this.Utf16Span.SequenceEqual(hasUtf16.Utf16Span);
            }

            return ContentEquals0(other);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ContentEquals0(ICharSequence other)
        {
            for (int i = this.offset, j = 0; j < other.Count; ++i, ++j)
            {
                if (ByteToChar(this.value[i]) != other[j])
                {
                    return false;
                }
            }

            return true;
        }

        public bool ContentEqualsIgnoreCase(ICharSequence other)
        {
            if (ReferenceEquals(this, other)) { return true; }
            if (other == null || other.Count != this.length) { return false; }

            if (other is IHasUtf16Span utf16Span)
            {
                return this.Utf16Span.Equals(utf16Span.Utf16Span, StringComparison.OrdinalIgnoreCase);
            }

            return ContentEqualsIgnoreCase0(other);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ContentEqualsIgnoreCase0(ICharSequence other)
        {
            if (other is AsciiString rhs)
            {
                for (int i = this.offset, j = rhs.offset; i < this.length; ++i, ++j)
                {
                    if (!EqualsIgnoreCase(this.value[i], rhs.value[j]))
                    {
                        return false;
                    }
                }
                return true;
            }

            for (int i = this.offset, j = 0; i < this.length; ++i, ++j)
            {
                if (!EqualsIgnoreCase(ByteToChar(this.value[i]), other[j]))
                {
                    return false;
                }
            }

            return true;
        }

        public char[] ToCharArray(int start, int end)
        {
            int count = end - start;
            if (0u >= (uint)count)
            {
                return EmptyArrays.EmptyChars;
            }

            if (MathUtil.IsOutOfBounds(start, count, this.length))
            {
                ThrowIndexOutOfRangeException_SrcIndex(start, count, this.length);
            }

            return this.Utf16Span.Slice(start, count).ToArray();
        }

        public void Copy(int srcIdx, char[] dst, int dstIdx, int count)
        {
            if (null == dst) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }

            if (MathUtil.IsOutOfBounds(srcIdx, count, this.length))
            {
                ThrowIndexOutOfRangeException_SrcIndex(srcIdx, count, this.length);
            }

            this.Utf16Span.Slice(srcIdx, count).CopyTo(new Span<char>(dst, dstIdx, count));
        }

        public int IndexOf(ICharSequence subString, int start)
        {
            int thisLen = this.length;
            if (0u >= (uint)thisLen) { return IndexNotFound; }

            if (start < 0) { start = 0; }

            int subCount = subString.Count;
            if (0u >= (uint)subCount)
            {
                return start < thisLen ? start : thisLen;
            }
            if (subCount > thisLen - start) { return IndexNotFound; }

            char firstChar = subString[0];
            if (firstChar > MaxCharValue) { return IndexNotFound; }

            if (0u >= (uint)start)
            {
                if (subString is IHasAsciiSpan hasAscii)
                {
                    return this.AsciiSpan.IndexOf(hasAscii.AsciiSpan);
                }
                if (subString is IHasUtf16Span hasUtf16)
                {
                    return this.Utf16Span.IndexOf(hasUtf16.Utf16Span);
                }
            }
            else
            {
                if (subString is IHasAsciiSpan hasAscii)
                {
                    var result = this.AsciiSpan.Slice(start).IndexOf(hasAscii.AsciiSpan);
                    return result >= 0 ? start + result : IndexNotFound;
                }
                if (subString is IHasUtf16Span hasUtf16)
                {
                    var result = this.Utf16Span.Slice(start).IndexOf(hasUtf16.Utf16Span);
                    return result >= 0 ? start + result : IndexNotFound;
                }
            }

            return IndexOf0(subString, start);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int IndexOf0(ICharSequence subString, int start)
        {
            int subCount = subString.Count;
            char firstChar = subString[0];

            var thisOffset = this.offset;
            var firstCharAsByte = (byte)firstChar;
            var len = thisOffset + this.length - subCount;
            var thisValue = this.value;
            for (int i = start + thisOffset; i <= len; ++i)
            {
                if (thisValue[i] == firstCharAsByte)
                {
                    int o1 = i, o2 = 0;
                    while (++o2 < subCount && ByteToChar(thisValue[++o1]) == subString[o2])
                    {
                        // Intentionally empty
                    }
                    if (o2 == subCount)
                    {
                        return i - thisOffset;
                    }
                }
            }
            return IndexNotFound;
        }

        public int IndexOf(char ch, int start)
        {
            if (0u >= (uint)this.length) { return IndexNotFound; }
            if (ch > MaxCharValue) { return IndexNotFound; }

            if (start < 0) { start = 0; }

            if (0u >= (uint)start)
            {
                return this.AsciiSpan.IndexOf((byte)ch);
            }
            var result = this.AsciiSpan.Slice(start).IndexOf((byte)ch);
            return result >= 0 ? start + result : IndexNotFound;
        }

        public int LastIndexOf(ICharSequence subString, int start)
        {
            int thisLen = this.length;
            if (0u >= (uint)thisLen) { return IndexNotFound; }

            int subCount = subString.Count;

            if (start < 0) { start = 0; }
            if (subCount <= 0) { return start < thisLen ? start : thisLen; }
            if (subCount > thisLen - start) { return IndexNotFound; }

            if (0u >= (uint)start)
            {
                if (subString is IHasAsciiSpan hasAscii)
                {
                    return this.AsciiSpan.LastIndexOf(hasAscii.AsciiSpan);
                }
                if (subString is IHasUtf16Span hasUtf16)
                {
                    return this.Utf16Span.LastIndexOf(hasUtf16.Utf16Span);
                }
            }
            else
            {
                if (subString is IHasAsciiSpan hasAscii)
                {
                    var result = this.AsciiSpan.Slice(start).LastIndexOf(hasAscii.AsciiSpan);
                    return result >= 0 ? start + result : IndexNotFound;
                }
                if (subString is IHasUtf16Span hasUtf16)
                {
                    var result = this.Utf16Span.Slice(start).LastIndexOf(hasUtf16.Utf16Span);
                    return result >= 0 ? start + result : IndexNotFound;
                }
            }

            return LastIndexOf0(subString, start);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int LastIndexOf0(ICharSequence subString, int start)
        {
            char firstChar = subString[0];
            if (firstChar > MaxCharValue) { return IndexNotFound; }

            int thisLen = this.length;
            int subCount = subString.Count;

            byte firstCharAsByte = (byte)firstChar;
            int end = offset + start;
            for (int i = offset + thisLen - subCount; i >= end; --i)
            {
                if (value[i] == firstCharAsByte)
                {
                    int o1 = i, o2 = 0;
                    while (++o2 < subCount && ByteToChar(value[++o1]) == subString[o2])
                    {
                        // Intentionally empty
                    }
                    if (o2 == subCount)
                    {
                        return i - offset;
                    }
                }
            }
            return IndexNotFound;
        }

        public bool RegionMatches(int thisStart, ICharSequence seq, int start, int count)
        {
            if (null == seq) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.seq); }

            if (0u >= (uint)count)
            {
                return true;
            }

            if (start < 0 || seq.Count - start < count)
            {
                return false;
            }

            int thisLen = this.length;
            if (thisStart < 0 || thisLen - thisStart < count)
            {
                return false;
            }

            if (seq is IHasAsciiSpan hasAscii)
            {
                return this.AsciiSpan.Slice(thisStart, count).SequenceEqual(hasAscii.AsciiSpan.Slice(start, count));
            }
            if (seq is IHasUtf16Span hasUtf16)
            {
                return this.Utf16Span.Slice(thisStart, count).SequenceEqual(hasUtf16.Utf16Span.Slice(start, count));
            }

            return RegionMatches0(thisStart, seq, start, count);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool RegionMatches0(int thisStart, ICharSequence seq, int start, int count)
        {
            int thatEnd = start + count;
            for (int i = start, j = thisStart + this.offset; i < thatEnd; i++, j++)
            {
                if (ByteToChar(this.value[j]) != seq[i])
                {
                    return false;
                }
            }

            return true;
        }

        public bool RegionMatchesIgnoreCase(int thisStart, ICharSequence seq, int start, int count)
        {
            if (null == seq) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.seq); }

            if (0u >= (uint)count)
            {
                return true;
            }

            int thisLen = this.length;
            if (thisStart < 0 || count > thisLen - thisStart)
            {
                return false;
            }
            if (start < 0 || count > seq.Count - start)
            {
                return false;
            }

            if (seq is IHasUtf16Span hasUtf16)
            {
                return this.Utf16Span.Slice(thisStart, count).Equals(hasUtf16.Utf16Span.Slice(start, count), StringComparison.OrdinalIgnoreCase);
            }

            return RegionMatchesIgnoreCase0(thisStart, seq, start, count);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool RegionMatchesIgnoreCase0(int thisStart, ICharSequence seq, int start, int count)
        {
            thisStart += this.offset;
            int thisEnd = thisStart + count;
            while (thisStart < thisEnd)
            {
                if (!EqualsIgnoreCase(ByteToChar(this.value[thisStart++]), seq[start++]))
                {
                    return false;
                }
            }

            return true;
        }

        public AsciiString Replace(char oldChar, char newChar)
        {
            if (oldChar > MaxCharValue)
            {
                return this;
            }

            var thisLen = this.length;
            if (0u >= thisLen) { return this; }

            var oldCharAsByte = CharToByte(oldChar);
            var newCharAsByte = CharToByte(newChar);

            var thisSpan = this.AsciiSpan;
            var pos = thisSpan.IndexOf(oldCharAsByte);
            if (pos < 0) { return this; }

            byte[] buffer = new byte[thisLen];
            var span = new Span<byte>(buffer);
            if (pos > 0)
            {
                thisSpan.Slice(0, pos).CopyTo(span.Slice(0, pos));
            }
            span[pos++] = newCharAsByte;
            for (var idx = pos; idx < thisLen; idx++)
            {
                byte oldValue = thisSpan[idx];
                span[idx] = oldValue != oldCharAsByte ? oldValue : newCharAsByte;
            }
            return new AsciiString(buffer, false);
        }

        public AsciiString ToLowerCase()
        {
            var thisLen = this.length;
            if (0u >= (uint)thisLen) { return this; }

            var thisSpan = this.AsciiSpan;
            var result = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(thisSpan), x => IsUpperCase(x), thisLen);
            if (result < 0) { return this; }

            byte[] buffer = new byte[thisLen];
            var span = new Span<byte>(buffer);
            if (result > 0)
            {
                thisSpan.Slice(0, result).CopyTo(span.Slice(0, result));
            }
            for (var idx = result; idx < thisLen; idx++)
            {
                byte oldValue = thisSpan[idx];
                span[idx] = ToLowerCase(thisSpan[idx]);
            }
            return new AsciiString(buffer, false);
        }

        public AsciiString ToUpperCase()
        {
            var thisLen = this.length;
            if (0u >= (uint)thisLen) { return this; }

            var thisSpan = this.AsciiSpan;
            var result = PlatformDependent.FindIndex(ref MemoryMarshal.GetReference(thisSpan), x => IsLowerCase(x), thisLen);
            if (result < 0) { return this; }

            byte[] buffer = new byte[thisLen];
            var span = new Span<byte>(buffer);
            if (result > 0)
            {
                thisSpan.Slice(0, result).CopyTo(span.Slice(0, result));
            }
            for (var idx = result; idx < thisLen; idx++)
            {
                byte oldValue = thisSpan[idx];
                span[idx] = ToUpperCase(thisSpan[idx]);
            }
            return new AsciiString(buffer, false);
        }

        public bool Equals(AsciiString other)
        {
            if (ReferenceEquals(this, other)) { return true; }

            return other != null && this.length == other.length
                && this.GetHashCode() == other.GetHashCode()
                && this.AsciiSpan.SequenceEqual(other.AsciiSpan);
        }

        public override bool Equals(object obj)
        {
            return this.ContentEquals(obj as ICharSequence);
        }

        bool IEquatable<ICharSequence>.Equals(ICharSequence other)
        {
            return this.ContentEquals(other);
        }
    }
}

#endif