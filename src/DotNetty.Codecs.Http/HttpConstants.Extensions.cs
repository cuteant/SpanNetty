// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System.Text;
    using DotNetty.Buffers;

    partial class HttpConstants
    {
        /// <summary>不提供 Unicode 字节顺序标记，检测到无效的编码时不引发异常</summary>
        public static readonly UTF8Encoding UTF8NoBOM = new UTF8Encoding(false);

        /// <summary>不提供 Unicode 字节顺序标记，检测到无效的编码时引发异常</summary>
        public static readonly UTF8Encoding SecureUTF8NoBOM = new UTF8Encoding(false, true);

        /// <summary>提供 Unicode 字节顺序标记，检测到无效的编码时引发异常</summary>
        public static readonly UTF8Encoding SecureUTF8 = new UTF8Encoding(true, true);

        /// <summary>'\''</summary>
        public const byte Quote = (byte)'\'';

        /// <summary>' '</summary>
        public const byte Space = (byte)' ';

        /// <summary>'('</summary>
        public const byte StartComment = (byte)'(';

        /// <summary>')'</summary>
        public const byte EndComment = (byte)')';

        /// <summary>'\\'</summary>
        public const byte BackSlash = (byte)'\\';

        /// <summary>'/'</summary>
        public const byte ForwardSlash = (byte)'/';

        /// <summary>'@'</summary>
        public const byte At = (byte)'@';

        /// <summary>'>'</summary>
        public const byte EndAngleBracket = (byte)'>';

        /// <summary>小于</summary>
        public const byte StartAngleBracket = (byte)'<';

        /// <summary>'['</summary>
        public const byte StartSquareBracket = (byte)'[';

        /// <summary>']'</summary>
        public const byte EndSquareBracket = (byte)']';

        /// <summary>'.'</summary>
        public const byte Dot = (byte)'.';

        /// <summary>'?'</summary>
        public const byte QuestionMark = (byte)'?';

        /// <summary>'!'</summary>
        public const byte ExclamationMark = (byte)'!';

        /// <summary>'*'</summary>
        public const byte Star = (byte)'*';

        /// <summary>'+'</summary>
        public const byte PlusSign = (byte)'+';

        /// <summary>'-'</summary>
        public const byte MinusSign = (byte)'-';

        /// <summary>'_'</summary>
        public const byte Underline = (byte)'_';

        /// <summary>'%'</summary>
        public const byte Percent = (byte)'%';

        /// <summary>'&amp;'</summary>
        public const byte Ampersand = (byte)'&';

        /// <summary>'#'</summary>
        public const byte NumberSign = (byte)'#';

        /// <summary>'0'</summary>
        public const byte Zero = (byte)'0';

        /// <summary>'9'</summary>
        public const byte Nine = (byte)'9';


        /// <summary>Horizontal tab</summary>
        public const char HorizontalTabChar = '\t';

        /// <summary>Carriage return</summary>
        public const char CarriageReturnChar = '\r';

        /// <summary>Equals '='</summary>
        public const char EqualsSignChar = '=';

        /// <summary>Line feed character</summary>
        public const char LineFeedChar = '\n';

        /// <summary>Colon ':'</summary>
        public const char ColonChar = ':';

        /// <summary>Semicolon ';'</summary>
        public const char SemicolonChar = ';';

        /// <summary>Comma ','</summary>
        public const char CommaChar = ',';

        /// <summary>Double quote '"'</summary>
        public const char DoubleQuoteChar = '\"';

        /// <summary>'\''</summary>
        public const char QuoteChar = '\'';

        /// <summary>' '</summary>
        public const char SpaceChar = ' ';

        /// <summary>'('</summary>
        public const char StartCommentChar = '(';

        /// <summary>')'</summary>
        public const char EndCommentChar = ')';

        /// <summary>'\\'</summary>
        public const char BackSlashChar = '\\';

        /// <summary>'/'</summary>
        public const char ForwardSlashChar = '/';

        /// <summary>'@'</summary>
        public const char AtChar = '@';

        /// <summary>'>'</summary>
        public const char EndAngleBracketChar = '>';

        /// <summary>小于</summary>
        public const char StartAngleBracketChar = '<';

        /// <summary>'['</summary>
        public const char StartSquareBracketChar = '[';

        /// <summary>']'</summary>
        public const char EndSquareBracketChar = ']';

        /// <summary>'.'</summary>
        public const char DotChar = '.';

        /// <summary>'?'</summary>
        public const char QuestionMarkChar = '?';

        /// <summary>'!'</summary>
        public const char ExclamationMarkChar = '!';

        /// <summary>'*'</summary>
        public const char StarChar = '*';

        /// <summary>'+'</summary>
        public const char PlusSignChar = '+';

        /// <summary>'-'</summary>
        public const char MinusSignChar = '-';

        /// <summary>'_'</summary>
        public const char UnderlineChar = '_';

        /// <summary>'%'</summary>
        public const char PercentChar = '%';

        /// <summary>'&amp;'</summary>
        public const char AmpersandChar = '&';

        /// <summary>'#'</summary>
        public const char NumberSignChar = '#';

        /// <summary>'0'</summary>
        public const char ZeroChar = '0';

        /// <summary>'9'</summary>
        public const char NineChar = '9';
    }
}
