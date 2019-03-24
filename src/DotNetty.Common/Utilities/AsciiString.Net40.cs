// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET40

namespace DotNetty.Common.Utilities
{
    using System;
    using DotNetty.Common.Internal;

    partial class AsciiString
    {
        public int ForEachByte(IByteProcessor visitor) => this.ForEachByte0(0, this.length, visitor);

        public int ForEachByte(int index, int count, IByteProcessor visitor)
        {
            if (MathUtil.IsOutOfBounds(index, count, this.length))
            {
                ThrowIndexOutOfRangeException_Index(index, count, this.length);
            }
            return this.ForEachByte0(index, count, visitor);
        }

        int ForEachByte0(int index, int count, IByteProcessor visitor)
        {
            int len = this.offset + index + count;
            for (int i = this.offset + index; i < len; ++i)
            {
                if (!visitor.Process(this.value[i]))
                {
                    return i - this.offset;
                }
            }

            return -1;
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

        int ForEachByteDesc0(int index, int count, IByteProcessor visitor)
        {
            int end = this.offset + index;
            for (int i = this.offset + index + count - 1; i >= end; --i)
            {
                if (!visitor.Process(this.value[i]))
                {
                    return i - this.offset;
                }
            }

            return -1;
        }

        public int CompareTo(ICharSequence other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

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
            if (thatLen == 0)
            {
                return this;
            }

            byte[] newValue;
            if (charSequence is AsciiString that)
            {
                if (this.IsEmpty)
                {
                    return that;
                }

                newValue = new byte[thisLen + thatLen];
                PlatformDependent.CopyMemory(this.value, this.offset, newValue, 0, thisLen);
                PlatformDependent.CopyMemory(that.value, that.offset, newValue, thisLen, thatLen);

                return new AsciiString(newValue, false);
            }

            if (this.IsEmpty)
            {
                return new AsciiString(charSequence);
            }

            newValue = new byte[thisLen + thatLen];
            PlatformDependent.CopyMemory(this.value, this.offset, newValue, 0, thisLen);
            for (int i = thisLen, j = 0; i < newValue.Length; i++, j++)
            {
                newValue[i] = CharToByte(charSequence[j]);
            }

            return new AsciiString(newValue, false);
        }

        public bool ContentEquals(ICharSequence a)
        {
            if (ReferenceEquals(this, a)) { return true; }

            if (a == null || a.Count != this.length)
            {
                return false;
            }

            if (a is AsciiString asciiString)
            {
                return this.Equals(asciiString);
            }

            for (int i = this.offset, j = 0; j < a.Count; ++i, ++j)
            {
                if (ByteToChar(this.value[i]) != a[j])
                {
                    return false;
                }
            }

            return true;
        }

        public bool ContentEqualsIgnoreCase(ICharSequence other)
        {
            if (ReferenceEquals(this, other)) { return true; }
            if (other == null || other.Count != this.length)
            {
                return false;
            }

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
            if (count == 0)
            {
                return EmptyArrays.EmptyChars;
            }

            if (MathUtil.IsOutOfBounds(start, count, this.length))
            {
                ThrowIndexOutOfRangeException_SrcIndex(start, count, this.length);
            }

            var buffer = new char[count];
            for (int i = 0, j = start + this.offset; i < count; i++, j++)
            {
                buffer[i] = ByteToChar(this.value[j]);
            }

            return buffer;
        }

        public void Copy(int srcIdx, char[] dst, int dstIdx, int count)
        {
            if (null == dst) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }

            if (MathUtil.IsOutOfBounds(srcIdx, count, this.length))
            {
                ThrowIndexOutOfRangeException_SrcIndex(srcIdx, count, this.length);
            }

            int dstEnd = dstIdx + count;
            for (int i = dstIdx, j = srcIdx + this.offset; i < dstEnd; i++, j++)
            {
                dst[i] = ByteToChar(this.value[j]);
            }
        }

        public int IndexOf(ICharSequence subString, int start)
        {
            if (start < 0)
            {
                start = 0;
            }

            int thisLen = this.length;

            int subCount = subString.Count;
            if (subCount <= 0)
            {
                return start < thisLen ? start : thisLen;
            }
            if (subCount > thisLen - start)
            {
                return IndexNotFound;
            }

            char firstChar = subString[0];
            if (firstChar > MaxCharValue)
            {
                return IndexNotFound;
            }

            var thisOffset = this.offset;
            var firstCharAsByte = (byte)firstChar;
            var len = thisOffset + length - subCount;
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
            if (ch > MaxCharValue)
            {
                return IndexNotFound;
            }

            if (start < 0)
            {
                start = 0;
            }

            var thisOffset = this.offset;
            var thisValue = this.value;
            byte chAsByte = (byte)ch;
            int len = thisOffset + this.length;
            for (int i = start + thisOffset; i < len; ++i)
            {
                if (thisValue[i] == chAsByte)
                {
                    return i - thisOffset;
                }
            }
            return IndexNotFound;
        }

        public int LastIndexOf(ICharSequence subString, int start)
        {
            int thisLen = this.length;
            int subCount = subString.Count;

            if (start < 0)
            {
                start = 0;
            }
            if (subCount <= 0)
            {
                return start < thisLen ? start : thisLen;
            }
            if (subCount > thisLen - start)
            {
                return IndexNotFound;
            }

            char firstChar = subString[0];
            if (firstChar > MaxCharValue)
            {
                return IndexNotFound;
            }
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

            if (start < 0 || seq.Count - start < count)
            {
                return false;
            }

            int thisLen = this.length;
            if (thisStart < 0 || thisLen - thisStart < count)
            {
                return false;
            }

            if (count <= 0)
            {
                return true;
            }

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

            int thisLen = this.length;
            if (thisStart < 0 || count > thisLen - thisStart)
            {
                return false;
            }
            if (start < 0 || count > seq.Count - start)
            {
                return false;
            }

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

            var oldCharAsByte = CharToByte(oldChar);
            var newCharAsByte = CharToByte(newChar);
            var thisLen = this.length;
            var thisOffset = this.offset;
            var thisVal = this.value;
            var len = thisOffset + thisLen;
            for (int i = thisOffset; i < len; ++i)
            {
                if (thisVal[i] == oldCharAsByte)
                {
                    byte[] buffer = new byte[thisLen];
                    System.Array.Copy(thisVal, thisOffset, buffer, 0, i - thisOffset);
                    buffer[i - thisOffset] = newCharAsByte;
                    ++i;
                    for (; i < len; ++i)
                    {
                        byte oldValue = thisVal[i];
                        buffer[i - thisOffset] = oldValue != oldCharAsByte ? oldValue : newCharAsByte;
                    }
                    return new AsciiString(buffer, false);
                }
            }
            return this;
        }

        public AsciiString ToLowerCase()
        {
            bool lowercased = true;
            int i, j;
            int len = this.length + this.offset;
            for (i = this.offset; i < len; ++i)
            {
                byte b = this.value[i];
                if (b >= 'A' && b <= 'Z')
                {
                    lowercased = false;
                    break;
                }
            }

            // Check if this string does not contain any uppercase characters.
            if (lowercased)
            {
                return this;
            }

            var newValue = new byte[this.length];
            for (i = 0, j = this.offset; i < newValue.Length; ++i, ++j)
            {
                newValue[i] = ToLowerCase(this.value[j]);
            }

            return new AsciiString(newValue, false);
        }

        public AsciiString ToUpperCase()
        {
            bool uppercased = true;
            int i, j;
            int len = this.length + this.offset;
            for (i = this.offset; i < len; ++i)
            {
                byte b = this.value[i];
                if (b >= 'a' && b <= 'z')
                {
                    uppercased = false;
                    break;
                }
            }

            // Check if this string does not contain any lowercase characters.
            if (uppercased)
            {
                return this;
            }

            var newValue = new byte[this.length];
            for (i = 0, j = this.offset; i < newValue.Length; ++i, ++j)
            {
                newValue[i] = ToUpperCase(this.value[j]);
            }

            return new AsciiString(newValue, false);
        }

        public bool Equals(AsciiString other)
        {
            if (ReferenceEquals(this, other)) { return true; }

            return other != null && this.length == other.length
                && this.GetHashCode() == other.GetHashCode()
                && PlatformDependent.ByteArrayEquals(this.value, this.offset, other.value, other.offset, this.length);
        }

        public override bool Equals(object obj)
        {
            return this.ContentEquals(obj as ICharSequence); ;
        }

        bool IEquatable<ICharSequence>.Equals(ICharSequence other)
        {
            return this.ContentEquals(other);
        }
    }
}

#endif