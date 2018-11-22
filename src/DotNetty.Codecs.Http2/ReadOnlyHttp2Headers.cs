// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// A variant of <see cref="IHttp2Headers"/> which only supports read-only methods.
    /// <para>
    /// Any array passed to this class may be used directly in the underlying data structures of this class. If these
    /// arrays may be modified it is the caller's responsibility to supply this class with a copy of the array.
    /// </para>
    /// This may be a good alternative to <see cref="DefaultHttp2Headers"/> if your have a fixed set of headers which will not
    /// change.
    /// </summary>
    public sealed class ReadOnlyHttp2Headers : IHttp2Headers
    {
        private const byte PseudoHeaderToken = (byte)':';
        private readonly AsciiString[] pseudoHeaders;
        private readonly AsciiString[] otherHeaders;

        /// <summary>
        /// Used to create read only object designed to represent trailers.
        /// <para>If this is used for a purpose other than trailers you may violate the header serialization ordering defined by
        /// <a href="https://tools.ietf.org/html/rfc7540#section-8.1.2.1">RFC 7540, 8.1.2.1</a>.</para>
        /// </summary>
        /// <param name="validateHeaders"><c>true</c> will run validation on each header name/value pair to ensure protocol
        /// compliance.</param>
        /// <param name="otherHeaders">A an array of key:value pairs. Must not contain any
        /// <a href="https://tools.ietf.org/html/rfc7540#section-8.1.2.1">pseudo headers</a>
        /// or <c>null</c> names/values.
        /// A copy will <strong>NOT</strong> be made of this array. If the contents of this array
        /// may be modified externally you are responsible for passing in a copy.</param>
        /// <returns>A read only representation of the headers.</returns>
        public static ReadOnlyHttp2Headers Trailers(bool validateHeaders, params AsciiString[] otherHeaders)
        {
            return new ReadOnlyHttp2Headers(validateHeaders, EmptyArrays.EmptyAsciiStrings, otherHeaders);
        }

        /// <summary>
        /// Create a new read only representation of headers used by clients.
        /// </summary>
        /// <param name="validateHeaders"><c>true</c> will run validation on each header name/value pair to ensure protocol
        /// compliance.</param>
        /// <param name="method">The value for <see cref="PseudoHeaderName.Method"/>.</param>
        /// <param name="path">The value for <see cref="PseudoHeaderName.Path"/>.</param>
        /// <param name="scheme">The value for <see cref="PseudoHeaderName.Scheme"/>.</param>
        /// <param name="authority">The value for <see cref="PseudoHeaderName.Authority"/>.</param>
        /// <param name="otherHeaders">A an array of key:value pairs. Must not contain any
        /// <a href="https://tools.ietf.org/html/rfc7540#section-8.1.2.1">pseudo headers</a>
        /// or <c>null</c> names/values.
        /// A copy will <strong>NOT</strong> be made of this array. If the contents of this array
        /// may be modified externally you are responsible for passing in a copy.</param>
        /// <returns>a new read only representation of headers used by clients.</returns>
        public static ReadOnlyHttp2Headers ClientHeaders(bool validateHeaders,
            AsciiString method, AsciiString path, AsciiString scheme, AsciiString authority,
            params AsciiString[] otherHeaders)
        {
            return new ReadOnlyHttp2Headers(validateHeaders,
                    new AsciiString[]
                    {
                        PseudoHeaderName.Method.Value, method, PseudoHeaderName.Path.Value, path,
                        PseudoHeaderName.Scheme.Value, scheme, PseudoHeaderName.Authority.Value, authority
                    },
                    otherHeaders);
        }

        /// <summary>
        /// Create a new read only representation of headers used by servers.
        /// </summary>
        /// <param name="validateHeaders"><c>true</c> will run validation on each header name/value pair to ensure protocol
        /// compliance.</param>
        /// <param name="status">The value for <see cref="PseudoHeaderName.Status"/>.</param>
        /// <param name="otherHeaders">A an array of key:value pairs. Must not contain any
        /// <a href="https://tools.ietf.org/html/rfc7540#section-8.1.2.1">pseudo headers</a>
        /// or <c>null</c> names/values.
        /// A copy will <strong>NOT</strong> be made of this array. If the contents of this array
        /// may be modified externally you are responsible for passing in a copy.</param>
        /// <returns>a new read only representation of headers used by servers.</returns>
        public static ReadOnlyHttp2Headers ServerHeaders(bool validateHeaders,
            AsciiString status, params AsciiString[] otherHeaders)
        {
            return new ReadOnlyHttp2Headers(validateHeaders,
                                            new AsciiString[] { PseudoHeaderName.Status.Value, status },
                                            otherHeaders);
        }

        private ReadOnlyHttp2Headers(bool validateHeaders, AsciiString[] pseudoHeaders, params AsciiString[] otherHeaders)
        {
            Debug.Assert((pseudoHeaders.Length & 1) == 0); // pseudoHeaders are only set internally so assert should be enough.
            if ((otherHeaders.Length & 1) != 0)
            {
                ThrowHelper.ThrowArgumentException_InvalidArraySize();
            }
            if (validateHeaders)
            {
                ValidateHeaders(pseudoHeaders, otherHeaders);
            }
            this.pseudoHeaders = pseudoHeaders;
            this.otherHeaders = otherHeaders;
        }

        private static void ValidateHeaders(AsciiString[] pseudoHeaders, params AsciiString[] otherHeaders)
        {
            // We are only validating values... so start at 1 and go until end.
            for (int i = 1; i < pseudoHeaders.Length; i += 2)
            {
                // pseudoHeaders names are only set internally so they are assumed to be valid.
                if (pseudoHeaders[i] == null)
                {
                    ThrowHelper.ThrowArgumentException_PseudoHeadersValueIsNull(i);
                }
            }

            var seenNonPseudoHeader = false;
            var otherHeadersEnd = otherHeaders.Length - 1;
            for (int i = 0; i < otherHeadersEnd; i += 2)
            {
                AsciiString name = otherHeaders[i];

                DefaultHttp2Headers.Http2NameValidator.Instance.ValidateName(name);
                if (!seenNonPseudoHeader && !name.IsEmpty && name.ByteAt(0) != PseudoHeaderToken)
                {
                    seenNonPseudoHeader = true;
                }
                else if (seenNonPseudoHeader && !name.IsEmpty && name.ByteAt(0) == PseudoHeaderToken)
                {
                    ThrowHelper.ThrowArgumentException_OtherHeadersNameIsPseudoHeader(i);
                }
                var idx = i + 1;
                if (otherHeaders[idx] == null)
                {
                    ThrowHelper.ThrowArgumentException_OtherHeadersValueIsNull(idx);
                }
            }
        }

        private AsciiString Get0(ICharSequence name)
        {
            var nameHash = AsciiString.GetHashCode(name);

            var pseudoHeadersEnd = pseudoHeaders.Length - 1;
            for (int i = 0; i < pseudoHeadersEnd; i += 2)
            {
                AsciiString roName = pseudoHeaders[i];
                if (roName.GetHashCode() == nameHash && roName.ContentEqualsIgnoreCase(name))
                {
                    return pseudoHeaders[i + 1];
                }
            }

            var otherHeadersEnd = otherHeaders.Length - 1;
            for (int i = 0; i < otherHeadersEnd; i += 2)
            {
                AsciiString roName = otherHeaders[i];
                if (roName.GetHashCode() == nameHash && roName.ContentEqualsIgnoreCase(name))
                {
                    return otherHeaders[i + 1];
                }
            }
            return null;
        }

        public ICharSequence Method { get => Get(PseudoHeaderName.Method.Value, null); set => throw new NotSupportedException("read only"); }
        public ICharSequence Scheme { get => Get(PseudoHeaderName.Scheme.Value, null); set => throw new NotSupportedException("read only"); }
        public ICharSequence Authority { get => Get(PseudoHeaderName.Authority.Value, null); set => throw new NotSupportedException("read only"); }
        public ICharSequence Path { get => Get(PseudoHeaderName.Path.Value, null); set => throw new NotSupportedException("read only"); }
        public ICharSequence Status { get => Get(PseudoHeaderName.Status.Value, null); set => throw new NotSupportedException("read only"); }

        public int Size => (this.pseudoHeaders.Length + this.otherHeaders.Length).RightUShift(1);

        public bool IsEmpty => this.pseudoHeaders.Length == 0 && this.otherHeaders.Length == 0;

        public bool Contains(ICharSequence name, ICharSequence value, bool caseInsensitive)
        {
            var nameHash = AsciiString.GetHashCode(name);
            var strategy = caseInsensitive ? AsciiString.CaseInsensitiveHasher : AsciiString.CaseSensitiveHasher;
            var valueHash = strategy.HashCode(value);

            return Contains(name, nameHash, value, valueHash, strategy, otherHeaders)
                || Contains(name, nameHash, value, valueHash, strategy, pseudoHeaders);
        }

        private static bool Contains(ICharSequence name, int nameHash, ICharSequence value, int valueHash,
            IHashingStrategy<ICharSequence> hashingStrategy, AsciiString[] headers)
        {
            var headersEnd = headers.Length - 1;
            for (int i = 0; i < headersEnd; i += 2)
            {
                AsciiString roName = headers[i];
                AsciiString roValue = headers[i + 1];
                if (roName.GetHashCode() == nameHash && roValue.GetHashCode() == valueHash &&
                    roName.ContentEqualsIgnoreCase(name) && hashingStrategy.Equals(roValue, value))
                {
                    return true;
                }
            }
            return false;
        }

        public bool TryGet(ICharSequence name, out ICharSequence value)
        {
            value = this.Get0(name);
            return value != null;
        }

        public ICharSequence Get(ICharSequence name, ICharSequence defaultValue)
        {
            var value = this.Get0(name);
            return value ?? defaultValue;
        }

        public bool TryGetAndRemove(ICharSequence name, out ICharSequence value)
        {
            throw new NotSupportedException("read only");
        }

        public ICharSequence GetAndRemove(ICharSequence name, ICharSequence defaultValue)
        {
            throw new NotSupportedException("read only");
        }

        public IList<ICharSequence> GetAll(ICharSequence name)
        {
            var nameHash = AsciiString.GetHashCode(name);
            var values = new List<ICharSequence>();

            var pseudoHeadersEnd = pseudoHeaders.Length - 1;
            for (int i = 0; i < pseudoHeadersEnd; i += 2)
            {
                AsciiString roName = pseudoHeaders[i];
                if (roName.GetHashCode() == nameHash && roName.ContentEqualsIgnoreCase(name))
                {
                    values.Add(pseudoHeaders[i + 1]);
                }
            }

            var otherHeadersEnd = otherHeaders.Length - 1;
            for (int i = 0; i < otherHeadersEnd; i += 2)
            {
                AsciiString roName = otherHeaders[i];
                if (roName.GetHashCode() == nameHash && roName.ContentEqualsIgnoreCase(name))
                {
                    values.Add(otherHeaders[i + 1]);
                }
            }

            return values;
        }

        public IList<ICharSequence> GetAllAndRemove(ICharSequence name)
        {
            throw new NotSupportedException("read only");
        }

        public bool TryGetBoolean(ICharSequence name, out bool value)
        {
            AsciiString rawValue = this.Get0(name);

            if (rawValue != null)
            {
                value = CharSequenceValueConverter.Default.ConvertToBoolean(rawValue);
                return true;
            }
            value = default;
            return false;
        }

        public bool GetBoolean(ICharSequence name, bool defaultValue) => this.TryGetBoolean(name, out var v) ? v : defaultValue;

        public bool TryGetByte(ICharSequence name, out byte value)
        {
            AsciiString rawValue = this.Get0(name);

            if (rawValue != null)
            {
                value = CharSequenceValueConverter.Default.ConvertToByte(rawValue);
                return true;
            }
            value = default;
            return false;
        }

        public byte GetByte(ICharSequence name, byte defaultValue) => this.TryGetByte(name, out var v) ? v : defaultValue;

        public bool TryGetChar(ICharSequence name, out char value)
        {
            AsciiString rawValue = this.Get0(name);

            if (rawValue != null)
            {
                value = CharSequenceValueConverter.Default.ConvertToChar(rawValue);
                return true;
            }
            value = default;
            return false;
        }

        public char GetChar(ICharSequence name, char defaultValue) => this.TryGetChar(name, out var v) ? v : defaultValue;

        public bool TryGetShort(ICharSequence name, out short value)
        {
            AsciiString rawValue = this.Get0(name);

            if (rawValue != null)
            {
                value = CharSequenceValueConverter.Default.ConvertToShort(rawValue);
                return true;
            }
            value = default;
            return false;
        }

        public short GetShort(ICharSequence name, short defaultValue) => this.TryGetShort(name, out var v) ? v : defaultValue;

        public bool TryGetInt(ICharSequence name, out int value)
        {
            AsciiString rawValue = this.Get0(name);

            if (rawValue != null)
            {
                value = CharSequenceValueConverter.Default.ConvertToInt(rawValue);
                return true;
            }
            value = default;
            return false;
        }

        public int GetInt(ICharSequence name, int defaultValue) => this.TryGetInt(name, out var v) ? v : defaultValue;

        public bool TryGetLong(ICharSequence name, out long value)
        {
            AsciiString rawValue = this.Get0(name);

            if (rawValue != null)
            {
                value = CharSequenceValueConverter.Default.ConvertToLong(rawValue);
                return true;
            }
            value = default;
            return false;
        }

        public long GetLong(ICharSequence name, long defaultValue) => this.TryGetLong(name, out var v) ? v : defaultValue;

        public bool TryGetFloat(ICharSequence name, out float value)
        {
            AsciiString rawValue = this.Get0(name);

            if (rawValue != null)
            {
                value = CharSequenceValueConverter.Default.ConvertToFloat(rawValue);
                return true;
            }
            value = default;
            return false;
        }

        public float GetFloat(ICharSequence name, float defaultValue) => this.TryGetFloat(name, out var v) ? v : defaultValue;

        public bool TryGetDouble(ICharSequence name, out double value)
        {
            AsciiString rawValue = this.Get0(name);

            if (rawValue != null)
            {
                value = CharSequenceValueConverter.Default.ConvertToDouble(rawValue);
                return true;
            }
            value = default;
            return false;
        }

        public double GetDouble(ICharSequence name, double defaultValue) => this.TryGetDouble(name, out var v) ? v : defaultValue;

        public bool TryGetTimeMillis(ICharSequence name, out long value)
        {
            AsciiString rawValue = this.Get0(name);

            if (rawValue != null)
            {
                value = CharSequenceValueConverter.Default.ConvertToTimeMillis(rawValue);
                return true;
            }
            value = default;
            return false;
        }

        public long GetTimeMillis(ICharSequence name, long defaultValue) => this.TryGetTimeMillis(name, out var v) ? v : defaultValue;

        public bool TryGetBooleanAndRemove(ICharSequence name, out bool value)
        {
            throw new NotSupportedException("read only");
        }

        public bool GetBooleanAndRemove(ICharSequence name, bool defaultValue)
        {
            throw new NotSupportedException("read only");
        }

        public bool TryGetByteAndRemove(ICharSequence name, out byte value)
        {
            throw new NotSupportedException("read only");
        }

        public byte GetByteAndRemove(ICharSequence name, byte defaultValue)
        {
            throw new NotSupportedException("read only");
        }

        public bool TryGetCharAndRemove(ICharSequence name, out char value)
        {
            throw new NotSupportedException("read only");
        }

        public char GetCharAndRemove(ICharSequence name, char defaultValue)
        {
            throw new NotSupportedException("read only");
        }

        public bool TryGetShortAndRemove(ICharSequence name, out short value)
        {
            throw new NotSupportedException("read only");
        }

        public short GetShortAndRemove(ICharSequence name, short defaultValue)
        {
            throw new NotSupportedException("read only");
        }

        public bool TryGetIntAndRemove(ICharSequence name, out int value)
        {
            throw new NotSupportedException("read only");
        }

        public int GetIntAndRemove(ICharSequence name, int defaultValue)
        {
            throw new NotSupportedException("read only");
        }

        public bool TryGetLongAndRemove(ICharSequence name, out long value)
        {
            throw new NotSupportedException("read only");
        }

        public long GetLongAndRemove(ICharSequence name, long defaultValue)
        {
            throw new NotSupportedException("read only");
        }

        public bool TryGetFloatAndRemove(ICharSequence name, out float value)
        {
            throw new NotSupportedException("read only");
        }

        public float GetFloatAndRemove(ICharSequence name, float defaultValue)
        {
            throw new NotSupportedException("read only");
        }

        public bool TryGetDoubleAndRemove(ICharSequence name, out double value)
        {
            throw new NotSupportedException("read only");
        }

        public double GetDoubleAndRemove(ICharSequence name, double defaultValue)
        {
            throw new NotSupportedException("read only");
        }

        public bool TryGetTimeMillisAndRemove(ICharSequence name, out long value)
        {
            throw new NotSupportedException("read only");
        }

        public long GetTimeMillisAndRemove(ICharSequence name, long defaultValue)
        {
            throw new NotSupportedException("read only");
        }

        public bool Contains(ICharSequence name) => this.Get0(name) != null;

        public bool Contains(ICharSequence name, ICharSequence value) => this.Contains(name, value, false);

        public bool ContainsObject(ICharSequence name, object value) => this.Contains(name, CharSequenceValueConverter.Default.ConvertObject(value));

        public bool ContainsBoolean(ICharSequence name, bool value) => this.Contains(name, CharSequenceValueConverter.Default.ConvertBoolean(value));

        public bool ContainsByte(ICharSequence name, byte value) => this.Contains(name, CharSequenceValueConverter.Default.ConvertByte(value));

        public bool ContainsChar(ICharSequence name, char value) => this.Contains(name, CharSequenceValueConverter.Default.ConvertChar(value));

        public bool ContainsShort(ICharSequence name, short value) => this.Contains(name, CharSequenceValueConverter.Default.ConvertShort(value));

        public bool ContainsInt(ICharSequence name, int value) => this.Contains(name, CharSequenceValueConverter.Default.ConvertInt(value));

        public bool ContainsLong(ICharSequence name, long value) => this.Contains(name, CharSequenceValueConverter.Default.ConvertLong(value));

        public bool ContainsFloat(ICharSequence name, float value) => this.Contains(name, CharSequenceValueConverter.Default.ConvertFloat(value));

        public bool ContainsDouble(ICharSequence name, double value) => this.Contains(name, CharSequenceValueConverter.Default.ConvertDouble(value));

        public bool ContainsTimeMillis(ICharSequence name, long value) => this.Contains(name, CharSequenceValueConverter.Default.ConvertTimeMillis(value));

        public ISet<ICharSequence> Names()
        {
            if (this.IsEmpty) { return new HashSet<ICharSequence>(); }

            var names = new HashSet<ICharSequence>();
            var pseudoHeadersEnd = pseudoHeaders.Length - 1;
            for (int i = 0; i < pseudoHeadersEnd; i += 2)
            {
                names.Add(pseudoHeaders[i]);
            }

            var otherHeadersEnd = otherHeaders.Length - 1;
            for (int i = 0; i < otherHeadersEnd; i += 2)
            {
                names.Add(otherHeaders[i]);
            }
            return names;
        }

        public IHeaders<ICharSequence, ICharSequence> Add(ICharSequence name, ICharSequence value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> Add(ICharSequence name, IEnumerable<ICharSequence> values)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> AddObject(ICharSequence name, object value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> AddObject(ICharSequence name, IEnumerable<object> values)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> AddBoolean(ICharSequence name, bool value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> AddByte(ICharSequence name, byte value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> AddChar(ICharSequence name, char value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> AddShort(ICharSequence name, short value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> AddInt(ICharSequence name, int value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> AddLong(ICharSequence name, long value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> AddFloat(ICharSequence name, float value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> AddDouble(ICharSequence name, double value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> AddTimeMillis(ICharSequence name, long value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> Add(IHeaders<ICharSequence, ICharSequence> headers)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> Set(ICharSequence name, ICharSequence value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> Set(ICharSequence name, IEnumerable<ICharSequence> values)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> SetObject(ICharSequence name, object value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> SetObject(ICharSequence name, IEnumerable<object> values)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> SetBoolean(ICharSequence name, bool value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> SetByte(ICharSequence name, byte value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> SetChar(ICharSequence name, char value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> SetShort(ICharSequence name, short value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> SetInt(ICharSequence name, int value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> SetLong(ICharSequence name, long value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> SetFloat(ICharSequence name, float value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> SetDouble(ICharSequence name, double value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> SetTimeMillis(ICharSequence name, long value)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> Set(IHeaders<ICharSequence, ICharSequence> headers)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> SetAll(IHeaders<ICharSequence, ICharSequence> headers)
        {
            throw new NotSupportedException("read only");
        }

        public bool Remove(ICharSequence name)
        {
            throw new NotSupportedException("read only");
        }

        public IHeaders<ICharSequence, ICharSequence> Clear()
        {
            throw new NotSupportedException("read only");
        }

        public IEnumerator<HeaderEntry<ICharSequence, ICharSequence>> GetEnumerator()
        {
            var pseudoHeadersEnd = pseudoHeaders.Length - 1;
            for (int i = 0; i < pseudoHeadersEnd; i += 2)
            {
                yield return new HeaderEntry<ICharSequence, ICharSequence>(pseudoHeaders[i], pseudoHeaders[i + 1], true);
            }

            var otherHeadersEnd = otherHeaders.Length - 1;
            for (int i = 0; i < otherHeadersEnd; i += 2)
            {
                yield return new HeaderEntry<ICharSequence, ICharSequence>(otherHeaders[i], otherHeaders[i + 1], true);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public override string ToString()
        {
            var builder = new StringBuilder(StringUtil.SimpleClassName(this)).Append('[');
            string separator = "";
            foreach (var entry in this)
            {
                builder.Append(separator);
                builder.Append(entry.Key).Append(": ").Append(entry.Value);
                separator = ", ";
            }
            return builder.Append(']').ToString();
        }
    }
}
