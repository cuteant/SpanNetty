// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System.Text;
    using DotNetty.Buffers;

    public static partial class HttpConstants
    {
        /// <summary>Horizontal space</summary>
        public const byte HorizontalSpace = 32;

        /// <summary>Horizontal tab</summary>
        public const byte HorizontalTab = 9;

        /// <summary>Carriage return</summary>
        public const byte CarriageReturn = 13;
        public const uint NCarriageReturn = 13u;

        /// <summary>Equals '='</summary>
        public const byte EqualsSign = 61;

        /// <summary>Line feed character</summary>
        public const byte LineFeed = 10;
        public const uint NLineFeed = 10u;

        /// <summary>Colon ':'</summary>
        public const byte Colon = 58;

        /// <summary>Semicolon ';'</summary>
        public const byte Semicolon = 59;

        /// <summary>Comma ','</summary>
        public const byte Comma = 44;

        /// <summary>Double quote '"'</summary>
        public const byte DoubleQuote = (byte)'"';

         // Default character set (UTF-8)
        public static readonly Encoding DefaultEncoding = Encoding.UTF8;

        // Horizontal space in char
        public const char HorizontalSpaceChar = (char)HorizontalSpace;

        // For HttpObjectEncoder
        internal const int CrlfShort = (CarriageReturn << 8) | LineFeed;

        internal const int ZeroCrlfMedium = ('0' << 16) | CrlfShort;

        internal static readonly byte[] ZeroCrlfCrlf = { (byte)'0', CarriageReturn, LineFeed, CarriageReturn, LineFeed };

        internal static readonly IByteBuffer CrlfBuf = Unpooled.UnreleasableBuffer(Unpooled.WrappedBuffer(new[] { CarriageReturn, LineFeed }));

        internal static readonly IByteBuffer ZeroCrlfCrlfBuf = Unpooled.UnreleasableBuffer(Unpooled.WrappedBuffer(ZeroCrlfCrlf));
    }
}
