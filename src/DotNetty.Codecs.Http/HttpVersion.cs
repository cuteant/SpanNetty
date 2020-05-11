// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    public partial class HttpVersion : IComparable<HttpVersion>, IComparable
    {
        static readonly Regex VersionPattern = new Regex("^(\\S+)/(\\d+)\\.(\\d+)$", RegexOptions.Compiled);

        internal static readonly AsciiString Http10String = new AsciiString("HTTP/1.0");
        internal static readonly AsciiString Http11String = new AsciiString("HTTP/1.1");

        public static readonly HttpVersion Http10 = new HttpVersion("HTTP", 1, 0, false, true);
        public static readonly HttpVersion Http11 = new HttpVersion("HTTP", 1, 1, true, true);

        [MethodImpl(InlineMethod.AggressiveInlining)]
        internal static HttpVersion ValueOf(AsciiString text)
        {
            if (text is null)
            {
                ThrowHelper.ThrowArgumentException_NullText();
            }

            // ReSharper disable once PossibleNullReferenceException
            HttpVersion version = ValueOfInline(text.Array);
            if (version is object)
            {
                return version;
            }

            // Fall back to slow path
            text = text.Trim();

            if (0u >= (uint)text.Count)
            {
                ThrowHelper.ThrowArgumentException_EmptyText();
            }

            // Try to match without convert to uppercase first as this is what 99% of all clients
            // will send anyway. Also there is a change to the RFC to make it clear that it is
            // expected to be case-sensitive
            //
            // See:
            // * http://trac.tools.ietf.org/wg/httpbis/trac/ticket/1
            // * http://trac.tools.ietf.org/wg/httpbis/trac/wiki
            //
            return Version0(text) ?? new HttpVersion(text.ToString(), true);
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        static HttpVersion ValueOfInline(byte[] bytes)
        {
            if (bytes.Length != 8) return null;

            if (bytes[0] != Http11Bytes[0]) return null;
            if (bytes[1] != Http11Bytes[1]) return null;
            if (bytes[2] != Http11Bytes[2]) return null;
            if (bytes[3] != Http11Bytes[3]) return null;
            if (bytes[4] != Http11Bytes[4]) return null;
            if (bytes[5] != Http11Bytes[5]) return null;
            if (bytes[6] != Http11Bytes[6]) return null;
            switch (bytes[7])
            {
                case OneByte:
                    return Http11;
                case ZeroByte:
                    return Http10;
                default:
                    return null;
            }
        }

        static HttpVersion Version0(AsciiString text)
        {
            if (Http11String.Equals(text))
            {
                return Http11;
            }
            if (Http10String.Equals(text))
            {
                return Http10;
            }

            return null;
        }

        readonly string protocolName;
        readonly int majorVersion;
        readonly int minorVersion;
        readonly AsciiString text;
        readonly bool keepAliveDefault;
        readonly byte[] bytes;

        public HttpVersion(string text, bool keepAliveDefault)
        {
            if (text is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.text); }

            text = text.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(text))
            {
                ThrowHelper.ThrowArgumentException_Empty(ExceptionArgument.text);
            }

            Match match = VersionPattern.Match(text);
            if (!match.Success)
            {
                ThrowHelper.ThrowArgumentException_InvalidVersion(text);
            }

            this.protocolName = match.Groups[1].Value;
            this.majorVersion = int.Parse(match.Groups[2].Value);
            this.minorVersion = int.Parse(match.Groups[3].Value);
            this.text = new AsciiString($"{this.ProtocolName}/{this.MajorVersion}.{this.MinorVersion}");
            this.keepAliveDefault = keepAliveDefault;
            this.bytes = null;
        }

        HttpVersion(string protocolName, int majorVersion, int minorVersion, bool keepAliveDefault, bool bytes)
        {
            if (protocolName is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.protocolName);
            }

            protocolName = protocolName.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(protocolName))
            {
                ThrowHelper.ThrowArgumentException_Empty(ExceptionArgument.protocolName);
            }

            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < protocolName.Length; i++)
            {
                char c = protocolName[i];
                if (CharUtil.IsISOControl(c) || char.IsWhiteSpace(c))
                {
                    ThrowHelper.ThrowArgumentException_InvalidProtocolName(c);
                }
            }

            if (majorVersion < 0)
            {
                ThrowHelper.ThrowArgumentException_NegativeVersion(ExceptionArgument.majorVersion);
            }
            if (minorVersion < 0)
            {
                ThrowHelper.ThrowArgumentException_NegativeVersion(ExceptionArgument.minorVersion);
            }

            this.protocolName = protocolName;
            this.majorVersion = majorVersion;
            this.minorVersion = minorVersion;
            this.text = new AsciiString(protocolName + '/' + majorVersion + '.' + minorVersion);
            this.keepAliveDefault = keepAliveDefault;

            this.bytes = bytes ? this.text.Array : null;
        }

        public string ProtocolName => this.protocolName;

        public int MajorVersion => this.majorVersion;

        public int MinorVersion => this.minorVersion;

        public AsciiString Text => this.text;

        public bool IsKeepAliveDefault => this.keepAliveDefault;

        public override string ToString() => this.text.ToString();

        public override int GetHashCode() => (this.protocolName.GetHashCode() * 31 + this.majorVersion) * 31 + this.minorVersion;

        public override bool Equals(object obj)
        {
            if (obj is HttpVersion that)
            {
                return this.minorVersion == that.minorVersion
                    && this.majorVersion == that.majorVersion
                    && string.Equals(this.protocolName, that.protocolName
#if NETCOREAPP_3_0_GREATER || NETSTANDARD_2_0_GREATER
                        );
#else
                        , StringComparison.Ordinal);
#endif
            }

            return false;
        }

        public int CompareTo(HttpVersion other)
        {
            int v = string.CompareOrdinal(this.protocolName, other.protocolName);
            if (v != 0)
            {
                return v;
            }

            v = this.majorVersion - other.majorVersion;
            if (v != 0)
            {
                return v;
            }

            return this.minorVersion - other.minorVersion;
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return 0;
            }

            if (obj is HttpVersion httpVersion)
            {
                return this.CompareTo(httpVersion);
            }

            return ThrowHelper.ThrowArgumentException_CompareToHttpVersion();
        }

        internal void Encode(IByteBuffer buf)
        {
            if (this.bytes is null)
            {
                buf.WriteCharSequence(this.text, Encoding.ASCII);
            }
            else
            {
                buf.WriteBytes(this.bytes);
            }
        }
    }
}
