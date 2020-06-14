// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// HTTP/2 pseudo-headers names.
    /// </summary>
    public sealed class PseudoHeaderName : IEquatable<PseudoHeaderName>
    {
        /// <summary>
        /// <c>:method</c>
        /// </summary>
        public static readonly PseudoHeaderName Method;

        /// <summary>
        /// <c>:scheme</c>
        /// </summary>
        public static readonly PseudoHeaderName Scheme;

        /// <summary>
        /// <c>:authority</c>
        /// </summary>
        public static readonly PseudoHeaderName Authority;

        /// <summary>
        /// <c>:path</c>
        /// </summary>
        public static readonly PseudoHeaderName Path;

        /// <summary>
        /// <c>:status</c>
        /// </summary>
        public static readonly PseudoHeaderName Status;

        public static readonly PseudoHeaderName[] All;

        private const char PseudoHeaderPrefix = ':';
        private const byte PseudoHeaderPrefixByte = (byte)PseudoHeaderPrefix;

        private readonly AsciiString _value;
        public readonly bool _requestOnly;

        private static readonly CharSequenceMap<PseudoHeaderName> PseudoHeaders;

        static PseudoHeaderName()
        {
            Method = new PseudoHeaderName(":method", true);
            Scheme = new PseudoHeaderName(":scheme", true);
            Authority = new PseudoHeaderName(":authority", true);
            Path = new PseudoHeaderName(":path", true);
            Status = new PseudoHeaderName(":status", false);
            All = new PseudoHeaderName[] { Method, Scheme, Authority, Path, Status };

            PseudoHeaders = new CharSequenceMap<PseudoHeaderName>();
            foreach (PseudoHeaderName pseudoHeader in All)
            {
                _ = PseudoHeaders.Add(pseudoHeader._value, pseudoHeader);
            }
        }

        PseudoHeaderName(string value, bool requestOnly)
        {
            _value = AsciiString.Cached(value);
            _requestOnly = requestOnly;
        }

        /// <summary>
        /// Return a slice so that the buffer gets its own reader index.
        /// </summary>
        /// <returns></returns>
        public AsciiString Value => _value;

        /// <summary>
        /// Indicates whether the pseudo-header is to be used in a request context.
        /// @return <c>true</c> if the pseudo-header is to be used in a request context
        /// </summary>
        public bool IsRequestOnly => _requestOnly;

        public static bool HasPseudoHeaderFormat(ICharSequence headerName)
        {
            if (headerName is AsciiString asciiHeaderName)
            {
                return (uint)asciiHeaderName.Count > 0u && asciiHeaderName.ByteAt(0) == PseudoHeaderPrefixByte;
            }
            else
            {
                return (uint)headerName.Count > 0u && headerName[0] == PseudoHeaderPrefix;
            }
        }

        /// <summary>
        /// Indicates whether the given header name is a valid HTTP/2 pseudo header.
        /// </summary>
        /// <param name="header"></param>
        /// <returns></returns>
        public static bool IsPseudoHeader(ICharSequence header)
        {
            return PseudoHeaders.Contains(header);
        }

        /// <summary>
        /// Returns the <see cref="PseudoHeaderName"/> corresponding to the specified header name.
        /// return corresponding <see cref="PseudoHeaderName"/> if any, <c>null</c> otherwise.
        /// </summary>
        /// <param name="header"></param>
        public static PseudoHeaderName GetPseudoHeader(ICharSequence header)
        {
            return PseudoHeaders.TryGet(header, out var name) ? name : null;
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj);
        }

        public bool Equals(PseudoHeaderName other)
        {
            return ReferenceEquals(this, other);
        }

        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(this);
        }
    }
}