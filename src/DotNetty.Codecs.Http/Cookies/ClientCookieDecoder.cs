// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Cookies
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using DotNetty.Common.Utilities;

    public sealed class ClientCookieDecoder : CookieDecoder
    {
        static readonly HashSet<char> _headerChars = new HashSet<char>(
            new char[] { '\t', '\n', (char)0x0b, '\f', '\r', ' ', ';' });

        // Strict encoder that validates that name and value chars are in the valid scope
        // defined in RFC6265
        public static readonly ClientCookieDecoder StrictDecoder = new ClientCookieDecoder(true);

        // Lax instance that doesn't validate name and value
        public static readonly ClientCookieDecoder LaxDecoder = new ClientCookieDecoder(false);

        ClientCookieDecoder(bool strict) : base(strict)
        {
        }

        public ICookie Decode(string header)
        {
            Contract.Requires(header != null);

            int headerLen = header.Length;
            if (headerLen == 0)
            {
                return null;
            }

            CookieBuilder cookieBuilder = null;
            //loop:
            for (int i = 0;;)
            {

                // Skip spaces and separators.
                while(true)
                {
                    if (i == headerLen)
                    {
                        goto loop;
                    }
                    char c = header[i];
                    if (c == ',')
                    {
                        // Having multiple cookies in a single Set-Cookie header is
                        // deprecated, modern browsers only parse the first one
                        goto loop;

                    }
                    else if (_headerChars.Contains(c))
                    {
                        i++;
                        continue;
                    }
                    break;
                }

                int nameBegin = i;
                int nameEnd = 0;
                int valueBegin = 0;
                int valueEnd = 0;

                while (true)
                {
                    char curChar = header[i];
                    switch (curChar)
                    {
                        case ';':
                            // NAME; (no value till ';')
                            nameEnd = i;
                            valueBegin = valueEnd = -1;
                            goto loop0;

                        case '=':
                            // NAME=VALUE
                            nameEnd = i;
                            i++;
                            if (i == headerLen)
                            {
                                // NAME= (empty value, i.e. nothing after '=')
                                valueBegin = valueEnd = 0;
                                goto loop0;
                            }

                            valueBegin = i;
                            // NAME=VALUE;
                            int semiPos = header.IndexOf(';', i);
                            valueEnd = i = semiPos > 0 ? semiPos : headerLen;
                            goto loop0;

                        default:
                            i++;
                            break;
                    }

                    if (i == headerLen)
                    {
                        // NAME (no value till the end of string)
                        nameEnd = headerLen;
                        valueBegin = valueEnd = -1;
                        break;
                    }
                }

                loop0:
                if (valueEnd > 0 && header[valueEnd - 1] == ',')
                {
                    // old multiple cookies separator, skipping it
                    valueEnd--;
                }

                if (cookieBuilder == null)
                {
                    // cookie name-value pair
                    DefaultCookie cookie = this.InitCookie(header, nameBegin, nameEnd, valueBegin, valueEnd);

                    if (cookie == null)
                    {
                        return null;
                    }

                    cookieBuilder = new CookieBuilder(cookie, header);
                }
                else
                {
                    // cookie attribute
                    cookieBuilder.AppendAttribute(nameBegin, nameEnd, valueBegin, valueEnd);
                }
            }

            loop:
            Debug.Assert(cookieBuilder != null);
            return cookieBuilder.Cookie();
        }

        sealed class CookieBuilder
        {
            readonly string header;
            readonly DefaultCookie cookie;
            string domain;
            string path;
            long maxAge = long.MinValue;
            int expiresStart;
            int expiresEnd;
            bool secure;
            bool httpOnly;

            internal CookieBuilder(DefaultCookie cookie, string header)
            {
                this.cookie = cookie;
                this.header = header;
            }

            long MergeMaxAgeAndExpires()
            {
                // max age has precedence over expires
                if (this.maxAge != long.MinValue)
                {
                    return this.maxAge;
                }
                else if (IsValueDefined(this.expiresStart, this.expiresEnd))
                {
                    DateTime? expiresDate = DateFormatter.ParseHttpDate(this.header, this.expiresStart, this.expiresEnd);
                    if (expiresDate != null)
                    {
                        return (expiresDate.Value.Ticks - DateTime.UtcNow.Ticks) / TimeSpan.TicksPerSecond;
                    }
                }
                return long.MinValue;
            }

            internal ICookie Cookie()
            {
                this.cookie.Domain = this.domain;
                this.cookie.Path = this.path;
                this.cookie.MaxAge = this.MergeMaxAgeAndExpires();
                this.cookie.IsSecure = this.secure;
                this.cookie.IsHttpOnly = this.httpOnly;

                return this.cookie;
            }

            public void AppendAttribute(int keyStart, int keyEnd, int valueStart, int valueEnd)
            {
                int length = keyEnd - keyStart;

                switch (length)
                {
                    case 4:
                        this.Parse4(keyStart, valueStart, valueEnd);
                        break;

                    case 6:
                        this.Parse6(keyStart, valueStart, valueEnd);
                        break;

                    case 7:
                        this.Parse7(keyStart, valueStart, valueEnd);
                        break;

                    case 8:
                        this.Parse8(keyStart);
                        break;
                }
            }

            void Parse4(int nameStart, int valueStart, int valueEnd)
            {
                if (CharUtil.RegionMatchesIgnoreCase(this.header, nameStart, CookieHeaderNames.Path, 0, 4))
                {
                    this.path = this.ComputeValue(valueStart, valueEnd);
                }
            }

            void Parse6(int nameStart, int valueStart, int valueEnd)
            {
                if (CharUtil.RegionMatchesIgnoreCase(this.header, nameStart, CookieHeaderNames.Domain, 0, 5))
                {
                    this.domain = this.ComputeValue(valueStart, valueEnd);
                }
                else if (CharUtil.RegionMatchesIgnoreCase(this.header, nameStart, CookieHeaderNames.Secure, 0, 5))
                {
                    this.secure = true;
                }
            }

            void SetMaxAge(string value)
            {
                if (long.TryParse(value, out long v))
                {
                    this.maxAge = Math.Max(v, 0);
                }
            }

            void Parse7(int nameStart, int valueStart, int valueEnd)
            {
                if (CharUtil.RegionMatchesIgnoreCase(this.header, nameStart, CookieHeaderNames.Expires, 0, 7))
                {
                    this.expiresStart = valueStart;
                    this.expiresEnd = valueEnd;
                }
                else if (CharUtil.RegionMatchesIgnoreCase(this.header, nameStart, CookieHeaderNames.MaxAge, 0, 7))
                {
                    this.SetMaxAge(this.ComputeValue(valueStart, valueEnd));
                }
            }

            void Parse8(int nameStart)
            {
                if (CharUtil.RegionMatchesIgnoreCase(this.header, nameStart, CookieHeaderNames.HttpOnly, 0, 8))
                {
                    this.httpOnly = true;
                }
            }

            static bool IsValueDefined(int valueStart, int valueEnd) => valueStart != -1 && valueStart != valueEnd;

            string ComputeValue(int valueStart, int valueEnd) =>  IsValueDefined(valueStart, valueEnd) 
                ? this.header.Substring(valueStart, valueEnd - valueStart) 
                : null;
        }
    }
}
