// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable UseStringInterpolation
namespace DotNetty.Common.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using DotNetty.Common.Internal;

    public sealed partial class AsciiString : ICharSequence, IEquatable<AsciiString>, IComparable<AsciiString>, IComparable
    {
        public static readonly AsciiString Empty = Cached(string.Empty);
        const int MaxCharValue = 255;
        const byte Replacement = (byte)'?';
        public const int IndexNotFound = -1;

        public static readonly IHashingStrategy<ICharSequence> CaseInsensitiveHasher = new CaseInsensitiveHashingStrategy();
        public static readonly IHashingStrategy<ICharSequence> CaseSensitiveHasher = new CaseSensitiveHashingStrategy();

        static readonly ICharEqualityComparator DefaultCharComparator = new DefaultCharEqualityComparator();
        static readonly ICharEqualityComparator GeneralCaseInsensitiveComparator = new GeneralCaseInsensitiveCharEqualityComparator();
        static readonly ICharEqualityComparator AsciiCaseInsensitiveCharComparator = new AsciiCaseInsensitiveCharEqualityComparator();

        sealed class CaseInsensitiveHashingStrategy : IHashingStrategy<ICharSequence>
        {
            public int HashCode(ICharSequence obj) => AsciiString.GetHashCode(obj);

            int IEqualityComparer<ICharSequence>.GetHashCode(ICharSequence obj) => this.HashCode(obj);

            public bool Equals(ICharSequence a, ICharSequence b) => ContentEqualsIgnoreCase(a, b);
        }

        sealed class CaseSensitiveHashingStrategy : IHashingStrategy<ICharSequence>
        {
            public int HashCode(ICharSequence obj) => AsciiString.GetHashCode(obj);

            int IEqualityComparer<ICharSequence>.GetHashCode(ICharSequence obj) => this.HashCode(obj);

            public bool Equals(ICharSequence a, ICharSequence b) => ContentEquals(a, b);
        }

        readonly byte[] value;
        readonly int offset;
        readonly int length;

        int hash;

        //Used to cache the ToString() value.
        string stringValue;

        // Called by AppendableCharSequence for http headers
        internal AsciiString(byte[] value)
        {
            this.value = value;
            this.offset = 0;
            this.length = value.Length;
        }

        public AsciiString(byte[] value, bool copy) : this(value, 0, value.Length, copy)
        {
        }

        public AsciiString(byte[] value, int start, int length, bool copy)
        {
            if (copy)
            {
                this.value = new byte[length];
                PlatformDependent.CopyMemory(value, start, this.value, 0, length);
                this.offset = 0;
            }
            else
            {
                if (MathUtil.IsOutOfBounds(start, length, value.Length))
                {
                    ThrowIndexOutOfRangeException_Start(start, length, value.Length);
                }

                this.value = value;
                this.offset = start;
            }

            this.length = length;
        }

        public AsciiString(char[] value) : this(value, 0, value.Length)
        {
        }

        public unsafe AsciiString(char[] value, int start, int length)
        {
            if (MathUtil.IsOutOfBounds(start, length, value.Length))
            {
                ThrowIndexOutOfRangeException_Start(start, length, value.Length);
            }

            this.value = new byte[length];
            fixed (char* chars = value)
            fixed (byte* bytes = this.value)
                GetBytes(chars + start, length, bytes);

            this.offset = 0;
            this.length = length;
        }

        public AsciiString(char[] value, Encoding encoding) : this(value, encoding, 0, value.Length)
        {
        }

        public AsciiString(char[] value, Encoding encoding, int start, int length)
        {
            this.value = encoding.GetBytes(value, start, length);
            this.offset = 0;
            this.length = this.value.Length;
        }

        public AsciiString(ICharSequence value) : this(value, 0, value.Count)
        {
        }

        public AsciiString(ICharSequence value, int start, int length)
        {
            if (MathUtil.IsOutOfBounds(start, length, value.Count))
            {
                ThrowIndexOutOfRangeException_Start(start, length, value.Count);
            }

            this.value = new byte[length];
            for (int i = 0, j = start; i < length; i++, j++)
            {
                this.value[i] = CharToByte(value[j]);
            }

            this.offset = 0;
            this.length = length;
        }

        public AsciiString(string value, Encoding encoding) : this(value, encoding, 0, value.Length)
        {
        }

        public AsciiString(string value, Encoding encoding, int start, int length)
        {
            int count = encoding.GetMaxByteCount(length);
            var bytes = new byte[count];
            count = encoding.GetBytes(value, start, length, bytes, 0);

            var thisVal = new byte[count];
            PlatformDependent.CopyMemory(bytes, 0, thisVal, 0, count);

            this.offset = 0;
            this.length = thisVal.Length;
            this.value = thisVal;
        }

        public AsciiString(string value) : this(value, 0, value.Length)
        {
        }

        public AsciiString(string value, int start, int length)
        {
            if (MathUtil.IsOutOfBounds(start, length, value.Length))
            {
                ThrowIndexOutOfRangeException_Start(start, length, value.Length);
            }

            var len = start + length;
            var thisVal = new byte[length];
            var idx = 0;
            for (int i = start; i < len; i++)
            {
                thisVal[idx++] = CharToByte(value[i]);
            }

            this.offset = 0;
            this.length = length;
            this.value = thisVal;
        }

        public byte ByteAt(int index)
        {
            // We must do a range check here to enforce the access does not go outside our sub region of the array.
            // We rely on the array access itself to pick up the array out of bounds conditions
            if ((uint)index >= (uint)this.length)
            {
                ThrowIndexOutOfRangeException_Index(index, this.length);
            }

            return this.value[index + this.offset];
        }

        public bool IsEmpty => 0u >= (uint)this.length;

        public int Count => this.length;

        /// <summary>
        /// During normal use cases the AsciiString should be immutable, but if the
        /// underlying array is shared, and changes then this needs to be called.
        /// </summary>
        public void ArrayChanged()
        {
            this.stringValue = null;
            this.hash = 0;
        }

        public byte[] Array => this.value;

        public int Offset => this.offset;

        public bool IsEntireArrayUsed => 0u >= (uint)this.offset && this.length == this.value.Length;

        public byte[] ToByteArray(int start, int end)
        {
            int count = end - start;
            var bytes = new byte[count];
            PlatformDependent.CopyMemory(this.value, this.offset + start, bytes, 0, count);

            return bytes;
        }

        public void Copy(int srcIdx, byte[] dst, int dstIdx, int count)
        {
            if (null == dst) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.dst); }

            if (MathUtil.IsOutOfBounds(srcIdx, count, this.length))
            {
                ThrowIndexOutOfRangeException_SrcIndex(srcIdx, count, this.length);
            }
            if (0u >= (uint)count)
            {
                return;
            }

            PlatformDependent.CopyMemory(this.value, srcIdx + this.offset, dst, dstIdx, count);
        }

        public char this[int index] => ByteToChar(this.ByteAt(index));

        public ICharSequence SubSequence(int start) => this.SubSequence(start, this.length);

        public ICharSequence SubSequence(int start, int end) => this.SubSequence(start, end, true);

        public AsciiString SubSequence(int start, int end, bool copy)
        {
            if (MathUtil.IsOutOfBounds(start, end - start, this.length))
            {
                ThrowIndexOutOfRangeException_StartEnd(start, end, this.length);
            }

            if (0u >= (uint)start && end == this.length)
            {
                return this;
            }

            return end == start ? Empty : new AsciiString(this.value, start + this.offset, end - start, copy);
        }

        public static ICharSequence Trim(ICharSequence c)
        {
            if (c is AsciiString asciiString)
            {
                return asciiString.Trim();
            }
            int start = 0;
            int last = c.Count - 1;
            int end = last;
            while (start <= end && c[start] <= ' ')
            {
                start++;
            }
            while (end >= start && c[end] <= ' ')
            {
                end--;
            }
            if (0u >= (uint)start && end == last)
            {
                return c;
            }
            return c.SubSequence(start, end + 1);
        }

        public AsciiString Trim()
        {
            int start = this.offset;
            int last = this.offset + this.length - 1;
            int end = last;
            var thisValue = this.value;
            while (start <= end && thisValue[start] <= ' ')
            {
                start++;
            }
            while (end >= start && thisValue[end] <= ' ')
            {
                end--;
            }
            if (0u >= (uint)start && end == last)
            {
                return this;
            }

            return new AsciiString(thisValue, start, end - start + 1, false);
        }

        public unsafe bool ContentEquals(string a)
        {
            if (a == null)
            {
                return false;
            }
            if (this.stringValue != null)
            {
                return string.Equals(this.stringValue, a, StringComparison.Ordinal);
            }
            if (this.length != a.Length)
            {
                return false;
            }

            if (this.length > 0)
            {
                fixed (char* p = a)
                fixed (byte* b = &this.value[this.offset])
                    for (int i = 0; i < this.length; ++i)
                    {
                        if (CharToByte(*(p + i)) != *(b + i))
                        {
                            return false;
                        }
                    }
            }

            return true;
        }

        public AsciiString[] Split(char delim)
        {
            List<AsciiString> res = InternalThreadLocalMap.Get().AsciiStringList();

            int start = 0;
            int count = this.length;
            for (int i = start; i < count; i++)
            {
                if (this[i] == delim)
                {
                    if (start == i)
                    {
                        res.Add(Empty);
                    }
                    else
                    {
                        res.Add(new AsciiString(this.value, start + this.offset, i - start, false));
                    }
                    start = i + 1;
                }
            }

            if (0u >= (uint)start)
            {
                // If no delimiter was found in the value
                res.Add(this);
            }
            else
            {
                if (start != count)
                {
                    // Add the last element if it's not empty.
                    res.Add(new AsciiString(this.value, start + this.offset, count - start, false));
                }
                else
                {
                    // Truncate trailing empty elements.
                    while (res.Count > 0)
                    {
                        int i = res.Count - 1;
                        if (!res[i].IsEmpty)
                        {
                            res.RemoveAt(i);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            var strings = new AsciiString[res.Count];
            res.CopyTo(strings);
            return strings;
        }

        // ReSharper disable NonReadonlyMemberInGetHashCode
        public override int GetHashCode()
        {
            int h = this.hash;
            if (0u >= (uint)h)
            {
                h = PlatformDependent.HashCodeAscii(this.value, this.offset, this.length);
                this.hash = h;
            }

            return h;
        }

        public override string ToString()
        {
            if (this.stringValue != null)
            {
                return this.stringValue;
            }

            this.stringValue = this.ToString(0);
            return this.stringValue;
        }

        public string ToString(int start) => this.ToString(start, this.length);

        [MethodImpl(InlineMethod.Value)]
        public unsafe string ToString(int start, int end)
        {
            int count = end - start;
            if (MathUtil.IsOutOfBounds(start, count, this.length))
            {
                ThrowIndexOutOfRangeException_SrcIndex(start, count, this.length);
            }
            if (0u >= (uint)count)
            {
                return string.Empty;
            }

            fixed (byte* p = &this.value[this.offset + start])
            {
                return Marshal.PtrToStringAnsi((IntPtr)p, count);
            }
        }

        public static AsciiString Of(string value) => new AsciiString(value);

        public static AsciiString Of(ICharSequence charSequence) => charSequence is AsciiString s ? s : new AsciiString(charSequence);

        public static AsciiString Cached(string value)
        {
            var asciiString = new AsciiString(value);
            asciiString.stringValue = value;
            return asciiString;
        }

        public static int GetHashCode(ICharSequence value)
        {
            if (value == null)
            {
                return 0;
            }
            if (value is AsciiString)
            {
                return value.GetHashCode();
            }

            return PlatformDependent.HashCodeAscii(value);
        }

        public static bool Contains(ICharSequence a, ICharSequence b) => Contains(a, b, DefaultCharComparator);

        public static bool ContainsIgnoreCase(ICharSequence a, ICharSequence b) => Contains(a, b, AsciiCaseInsensitiveCharComparator);

        public static bool ContentEqualsIgnoreCase(ICharSequence a, ICharSequence b)
        {
            if (ReferenceEquals(a, b)) { return true; }
            if (a == null || b == null) { return false; }

            if (a is AsciiString stringA)
            {
                return stringA.ContentEqualsIgnoreCase(b);
            }
            if (b is AsciiString stringB)
            {
                return stringB.ContentEqualsIgnoreCase(a);
            }

            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; ++i)
            {
                if (!EqualsIgnoreCase(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool ContainsContentEqualsIgnoreCase(ICollection<ICharSequence> collection, ICharSequence value)
        {
            foreach (ICharSequence v in collection)
            {
                if (ContentEqualsIgnoreCase(value, v))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ContainsAllContentEqualsIgnoreCase(ICollection<ICharSequence> a, ICollection<AsciiString> b)
        {
            foreach (AsciiString v in b)
            {
                if (!ContainsContentEqualsIgnoreCase(a, v))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool ContentEquals(ICharSequence a, ICharSequence b)
        {
            if (a == null || b == null)
            {
                return ReferenceEquals(a, b);
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            if (a is AsciiString stringA)
            {
                return stringA.ContentEquals(b);
            }
            if (b is AsciiString stringB)
            {
                return stringB.ContentEquals(a);
            }

            for (int i = 0; i < a.Count; ++i)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        static bool Contains(ICharSequence a, ICharSequence b, ICharEqualityComparator comparator)
        {
            if (a == null || b == null || a.Count < b.Count)
            {
                return false;
            }
            if (0u >= (uint)b.Count)
            {
                return true;
            }

            int bStart = 0;
            for (int i = 0; i < a.Count; ++i)
            {
                if (comparator.CharEquals(b[bStart], a[i]))
                {
                    // If b is consumed then true.
                    if (++bStart == b.Count)
                    {
                        return true;
                    }
                }
                else if (a.Count - i < b.Count)
                {
                    // If there are not enough characters left in a for b to be contained, then false.
                    return false;
                }
                else
                {
                    bStart = 0;
                }
            }

            return false;
        }

        static bool RegionMatchesCharSequences(ICharSequence cs, int csStart,
            ICharSequence seq, int start, int length, ICharEqualityComparator charEqualityComparator)
        {
            //general purpose implementation for CharSequences
            if (csStart < 0 || length > cs.Count - csStart)
            {
                return false;
            }
            if (start < 0 || length > seq.Count - start)
            {
                return false;
            }

            int csIndex = csStart;
            int csEnd = csIndex + length;
            int stringIndex = start;

            while (csIndex < csEnd)
            {
                char c1 = cs[csIndex++];
                char c2 = seq[stringIndex++];

                if (!charEqualityComparator.CharEquals(c1, c2))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool RegionMatches(ICharSequence cs, bool ignoreCase, int csStart, ICharSequence seq, int start, int length)
        {
            if (cs == null || seq == null)
            {
                return false;
            }
            switch (cs)
            {
                case StringCharSequence stringCharSequence when seq is StringCharSequence:
                    return ignoreCase
                        ? stringCharSequence.RegionMatchesIgnoreCase(csStart, seq, start, length)
                        : stringCharSequence.RegionMatches(csStart, seq, start, length);

                case AsciiString asciiString:
                    return ignoreCase
                        ? asciiString.RegionMatchesIgnoreCase(csStart, seq, start, length)
                        : asciiString.RegionMatches(csStart, seq, start, length);

                default:
                    return RegionMatchesCharSequences(cs, csStart, seq, start, length,
                        ignoreCase ? GeneralCaseInsensitiveComparator : DefaultCharComparator);
            }
        }

        public static bool RegionMatchesAscii(ICharSequence cs, bool ignoreCase, int csStart, ICharSequence seq, int start, int length)
        {
            if (cs == null || seq == null)
            {
                return false;
            }

            switch (cs)
            {
                case StringCharSequence _ when !ignoreCase && seq is StringCharSequence:
                    //we don't call regionMatches from String for ignoreCase==true. It's a general purpose method,
                    //which make complex comparison in case of ignoreCase==true, which is useless for ASCII-only strings.
                    //To avoid applying this complex ignore-case comparison, we will use regionMatchesCharSequences
                    return cs.RegionMatches(csStart, seq, start, length);

                case AsciiString asciiString:
                    return ignoreCase
                        ? asciiString.RegionMatchesIgnoreCase(csStart, seq, start, length)
                        : asciiString.RegionMatches(csStart, seq, start, length);

                default:
                    return RegionMatchesCharSequences(cs, csStart, seq, start, length,
                        ignoreCase ? AsciiCaseInsensitiveCharComparator : DefaultCharComparator);
            }
        }

        public static int IndexOfIgnoreCase(ICharSequence str, ICharSequence searchStr, int startPos)
        {
            if (str == null || searchStr == null)
            {
                return IndexNotFound;
            }

            if (startPos < 0)
            {
                startPos = 0;
            }
            int searchStrLen = searchStr.Count;
            int endLimit = str.Count - searchStrLen + 1;
            if (startPos > endLimit)
            {
                return IndexNotFound;
            }
            if (0u >= (uint)searchStrLen)
            {
                return startPos;
            }
            for (int i = startPos; i < endLimit; i++)
            {
                if (RegionMatches(str, true, i, searchStr, 0, searchStrLen))
                {
                    return i;
                }
            }

            return IndexNotFound;
        }

        public static int IndexOfIgnoreCaseAscii(ICharSequence str, ICharSequence searchStr, int startPos)
        {
            if (str == null || searchStr == null)
            {
                return IndexNotFound;
            }

            if (startPos < 0)
            {
                startPos = 0;
            }
            int searchStrLen = searchStr.Count;
            int endLimit = str.Count - searchStrLen + 1;
            if (startPos > endLimit)
            {
                return IndexNotFound;
            }
            if (0u >= (uint)searchStrLen)
            {
                return startPos;
            }
            for (int i = startPos; i < endLimit; i++)
            {
                if (RegionMatchesAscii(str, true, i, searchStr, 0, searchStrLen))
                {
                    return i;
                }
            }

            return IndexNotFound;
        }

        public static int IndexOf(ICharSequence cs, char searchChar, int start)
        {
            switch (cs)
            {
                case StringCharSequence stringCharSequence:
                    return stringCharSequence.IndexOf(searchChar, start);

                case AsciiString asciiString:
                    return asciiString.IndexOf(searchChar, start);

                case null:
                    return IndexNotFound;
            }
            int sz = cs.Count;
            for (int i = start < 0 ? 0 : start; i < sz; i++)
            {
                if (cs[i] == searchChar)
                {
                    return i;
                }
            }
            return IndexNotFound;
        }

        [MethodImpl(InlineMethod.Value)]
        public static bool EqualsIgnoreCase(byte a, byte b)
        {
            var ua = (uint)a;
            var ub = (uint)b;
            return (ua == ub || ToLowerCase0(ua) == ToLowerCase0(ub)) ? true : false;
        }

        [MethodImpl(InlineMethod.Value)]
        public static bool EqualsIgnoreCase(char a, char b)
        {
            var ua = (uint)a;
            var ub = (uint)b;
            return (ua == ub || ToLowerCase0(ua) == ToLowerCase0(ub)) ? true : false;
        }

        [MethodImpl(InlineMethod.Value)]
        public static byte ToLowerCase(byte b) => unchecked((byte)ToLowerCase0(b));

        [MethodImpl(InlineMethod.Value)]
        public static byte ToLowerCase(uint b) => unchecked((byte)ToLowerCase0(b));
        [MethodImpl(InlineMethod.Value)]
        public static char ToLowerCase(char c) => unchecked((char)ToLowerCase0(c));
        [MethodImpl(InlineMethod.Value)]
        public static uint ToLowerCase0(uint b) => IsUpperCase(b) ? (b + 32u) : b;

        [MethodImpl(InlineMethod.Value)]
        public static byte ToUpperCase(byte b) => unchecked((byte)ToUpperCase0(b));
        [MethodImpl(InlineMethod.Value)]
        public static byte ToUpperCase(uint b) => unchecked((byte)ToUpperCase0(b));
        [MethodImpl(InlineMethod.Value)]
        public static char ToUpperCase(char c) => unchecked((char)ToUpperCase0(c));
        [MethodImpl(InlineMethod.Value)]
        public static uint ToUpperCase0(uint b) => IsLowerCase(b) ? (b - 32u) : b;

        const uint DigitDiff = '9' - '0';
        const uint HexCharDiff = 'F' - 'A';
        const uint AsciiCharDiff = 'Z' - 'A';
        const uint Ascii0 = '0';
        const uint AsciiA = 'A';
        const uint Asciia = 'a';
        [MethodImpl(InlineMethod.Value)]
        public static bool IsLowerCase(byte value) => (value - Asciia <= AsciiCharDiff) ? true : false;
        [MethodImpl(InlineMethod.Value)]
        public static bool IsLowerCase(uint value) => (value - Asciia <= AsciiCharDiff) ? true : false;
        [MethodImpl(InlineMethod.Value)]
        public static bool IsLowerCase(char value) => (value - Asciia <= AsciiCharDiff) ? true : false;

        [MethodImpl(InlineMethod.Value)]
        public static bool IsUpperCase(byte value) => (value - AsciiA <= AsciiCharDiff) ? true : false;
        [MethodImpl(InlineMethod.Value)]
        public static bool IsUpperCase(uint value) => (value - AsciiA <= AsciiCharDiff) ? true : false;
        [MethodImpl(InlineMethod.Value)]
        public static bool IsUpperCase(char value) => (value - AsciiA <= AsciiCharDiff) ? true : false;

        /// <summary>
        /// A hex digit is valid if it is in the range: [0..9] | [A..F] | [a..f]
        /// Otherwise, return false.
        /// </summary>
        [MethodImpl(InlineMethod.Value)]
        public static bool IsHexDigit(byte value) => IsHexDigit((uint)value);
        public static bool IsHexDigit(char value) => IsHexDigit((uint)value);
        [MethodImpl(InlineMethod.Value)]
        public static bool IsHexDigit(uint value) =>
            ((value - Ascii0) <= DigitDiff ||
            (value - AsciiA) <= HexCharDiff ||
            (value - Asciia) <= HexCharDiff) ? true : false;

        /// <summary>
        /// Returns <see langword="true"/> iff <paramref name="value"/> is in the range [0..9].
        /// Otherwise, returns <see langword="false"/>.
        /// </summary>
        [MethodImpl(InlineMethod.Value)]
        public static bool IsDigit(byte value) => (value - Ascii0 <= DigitDiff) ? true : false;
        [MethodImpl(InlineMethod.Value)]
        public static bool IsDigit(char value) => (value - Ascii0 <= DigitDiff) ? true : false;
        [MethodImpl(InlineMethod.Value)]
        public static bool IsDigit(uint value) => (value - Ascii0 <= DigitDiff) ? true : false;

        [MethodImpl(InlineMethod.Value)]
        public static byte CharToByte(char c) => c > MaxCharValue ? Replacement : unchecked((byte)c);

        [MethodImpl(InlineMethod.Value)]
        public static char ByteToChar(byte b) => (char)(b);

        public static explicit operator string(AsciiString value) => value?.ToString() ?? string.Empty;

        public static explicit operator AsciiString(string value) => value != null ? new AsciiString(value) : Empty;

        static unsafe void GetBytes(char* chars, int length, byte* bytes)
        {
            char* charEnd = chars + length;
            while (chars < charEnd)
            {
                char ch = *(chars++);
                // ByteToChar
                if (ch > MaxCharValue)
                {
                    *(bytes++) = Replacement;
                }
                else
                {
                    *(bytes++) = unchecked((byte)ch);
                }
            }
        }

        public int HashCode(bool ignoreCase) => !ignoreCase ? this.GetHashCode() : CaseInsensitiveHasher.GetHashCode(this);

        //
        // Compares the specified string to this string using the ASCII values of the characters. Returns 0 if the strings
        // contain the same characters in the same order. Returns a negative integer if the first non-equal character in
        // this string has an ASCII value which is less than the ASCII value of the character at the same position in the
        // specified string, or if this string is a prefix of the specified string. Returns a positive integer if the first
        // non-equal character in this string has a ASCII value which is greater than the ASCII value of the character at
        // the same position in the specified string, or if the specified string is a prefix of this string.
        // 
        public int CompareTo(AsciiString other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            int length1 = this.length;
            int length2 = other.length;
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

        public int CompareTo(object obj) => this.CompareTo(obj as AsciiString);

        public IEnumerator<char> GetEnumerator() => new CharSequenceEnumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowIndexOutOfRangeException_Start(int start, int length, int count)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("expected: 0 <= start({0}) <= start + length({1}) <= value.length({2})", start, length, count));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowIndexOutOfRangeException_StartEnd(int start, int end, int length)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("expected: 0 <= start({0}) <= end ({1}) <= length({2})", start, end, length));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowIndexOutOfRangeException_SrcIndex(int start, int count, int length)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("expected: 0 <= start({0}) <= srcIdx + length({1}) <= srcLen({2})", start, count, length));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowIndexOutOfRangeException_Index(int index, int length, int count)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("expected: 0 <= index({0} <= start + length({1}) <= length({2})", index, length, count));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowIndexOutOfRangeException_Index(int index, int length)
        {
            throw GetIndexOutOfRangeException();

            IndexOutOfRangeException GetIndexOutOfRangeException()
            {
                return new IndexOutOfRangeException(string.Format("index: {0} must be in the range [0,{1})", index, length));
            }
        }

        interface ICharEqualityComparator
        {
            bool CharEquals(char a, char b);
        }

        sealed class DefaultCharEqualityComparator : ICharEqualityComparator
        {
            public bool CharEquals(char a, char b) => a == b;
        }

        sealed class GeneralCaseInsensitiveCharEqualityComparator : ICharEqualityComparator
        {
            public bool CharEquals(char a, char b) =>
                char.ToUpper(a) == char.ToUpper(b) || char.ToLower(a) == char.ToLower(b);
        }

        sealed class AsciiCaseInsensitiveCharEqualityComparator : ICharEqualityComparator
        {
            public bool CharEquals(char a, char b) => EqualsIgnoreCase(a, b);
        }
    }
}
