// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using System;
    using System.Text;

    partial class HttpVersion : IEquatable<HttpVersion>
    {
        static readonly byte[] Http11Bytes = Encoding.ASCII.GetBytes("HTTP/1.1");
        const byte OneByte = (byte)'1';
        const byte ZeroByte = (byte)'0';

        public bool Equals(HttpVersion other)
        {
            if (ReferenceEquals(this, other)) { return true; }

            return other != null
                && this.minorVersion == other.minorVersion
                && this.majorVersion == other.majorVersion
                && string.Equals(this.protocolName, other.protocolName
#if NETCOREAPP_3_0_GREATER || NETSTANDARD_2_0_GREATER
                    );
#else
                    , StringComparison.Ordinal);
#endif
        }
    }
}
