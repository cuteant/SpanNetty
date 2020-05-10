// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoProperty
namespace DotNetty.Codecs.Http
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Text;
    using CuteAnt.Pool;
    using DotNetty.Common.Utilities;

    public class QueryStringDecoder
    {
        const int DefaultMaxParams = 1024;

        readonly Encoding charset;
        readonly string uri;
        readonly int maxParams;
        int pathEndIdx;
        string path;
        IDictionary<string, List<string>> parameters;

        public QueryStringDecoder(string uri) : this(uri, HttpConstants.DefaultEncoding)
        {
        }

        public QueryStringDecoder(string uri, bool hasPath) : this(uri, HttpConstants.DefaultEncoding, hasPath)
        {
        }

        public QueryStringDecoder(string uri, Encoding charset) : this(uri, charset, true)
        {
        }

        public QueryStringDecoder(string uri, Encoding charset, bool hasPath) : this(uri, charset, hasPath, DefaultMaxParams)
        {
        }

        public QueryStringDecoder(string uri, Encoding charset, bool hasPath, int maxParams)
        {
            if (uri is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.uri); }
            if (charset is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.charset); }
            if (maxParams <= 0) { ThrowHelper.ThrowArgumentException_Positive(maxParams, ExceptionArgument.maxParams); }

            this.uri = uri;
            this.charset = charset;
            this.maxParams = maxParams;

            // -1 means that path end index will be initialized lazily
            this.pathEndIdx = hasPath ? -1 : 0;
        }

        public QueryStringDecoder(Uri uri) : this(uri, HttpConstants.DefaultEncoding)
        {
        }

        public QueryStringDecoder(Uri uri, Encoding charset) : this(uri, charset, DefaultMaxParams)
        {
        }

        public QueryStringDecoder(Uri uri, Encoding charset, int maxParams)
        {
            if (uri is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.uri); }
            if (charset is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.charset); }
            if (maxParams <= 0) { ThrowHelper.ThrowArgumentException_Positive(maxParams, ExceptionArgument.maxParams); }

            string rawPath = uri.AbsolutePath;
            // Also take care of cut of things like "http://localhost"
            this.uri = uri.PathAndQuery;
            this.charset = charset;
            this.maxParams = maxParams;
            this.pathEndIdx = rawPath.Length;
        }

        public override string ToString() => this.uri;

        public string Path => this.path ?? 
            (this.path = DecodeComponent(this.uri, 0, this.PathEndIdx(), this.charset, true));

        public IDictionary<string, List<string>> Parameters => this.parameters ?? 
            (this.parameters = DecodeParams(this.uri, this.PathEndIdx(), this.charset, this.maxParams));

        public string RawPath() => this.uri.Substring(0, this.PathEndIdx());

        public string RawQuery() 
        {
            int start = this.pathEndIdx + 1;
            return start < this.uri.Length ? this.uri.Substring(start) : StringUtil.EmptyString;
        }

        int PathEndIdx()
        {
            if (this.pathEndIdx == -1)
            {
                this.pathEndIdx = FindPathEndIndex(this.uri);
            }
            return this.pathEndIdx;
        }

        static IDictionary<string, List<string>> DecodeParams(string s, int from, Encoding charset, int paramsLimit)
        {
            int len = s.Length;
            if ((uint)from >= (uint)len)
            {
                return ImmutableDictionary<string, List<string>>.Empty;
            }
            if (s[from] == HttpConstants.QuestionMarkChar)
            {
                from++;
            }
            var parameters = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            int nameStart = from;
            int valueStart = -1;
            int i;
            //loop:
            for (i = from; i < len; i++)
            {
                switch (s[i])
                {
                    case HttpConstants.EqualsSignChar:
                        if (nameStart == i)
                        {
                            nameStart = i + 1;
                        }
                        else if (valueStart < nameStart)
                        {
                            valueStart = i + 1;
                        }
                        break;
                    case HttpConstants.AmpersandChar:
                    case HttpConstants.SemicolonChar:
                        if (AddParam(s, nameStart, valueStart, i, parameters, charset))
                        {
                            paramsLimit--;
                            if (0u >= (uint)paramsLimit)
                            {
                                return parameters;
                            }
                        }
                        nameStart = i + 1;
                        break;
                    case HttpConstants.NumberSignChar:
                        goto loop;
                }
            }
            loop:
            AddParam(s, nameStart, valueStart, i, parameters, charset);
            return parameters;
        }

        static bool AddParam(string s, int nameStart, int valueStart, int valueEnd,
            Dictionary<string, List<string>> parameters, Encoding charset)
        {
            if (nameStart >= valueEnd)
            {
                return false;
            }
            if (valueStart <= nameStart)
            {
                valueStart = valueEnd + 1;
            }
            string name = DecodeComponent(s, nameStart, valueStart - 1, charset, false);
            string value = DecodeComponent(s, valueStart, valueEnd, charset, false);
            if (!parameters.TryGetValue(name, out List<string> values))
            {
                values = new List<string>(1);  // Often there's only 1 value.
                parameters.Add(name, values);
            }
            values.Add(value);
            return true;
        }

        public static string DecodeComponent(string s) => DecodeComponent(s, HttpConstants.DefaultEncoding);

        public static string DecodeComponent(string s, Encoding charset) => s is null 
            ? StringUtil.EmptyString : DecodeComponent(s, 0, s.Length, charset, false);

        static string DecodeComponent(string s, int from, int toExcluded, Encoding charset, bool isPath)
        {
            int len = toExcluded - from;
            if (len <= 0)
            {
                return StringUtil.EmptyString;
            }
            int firstEscaped = -1;
            for (int i = from; i < toExcluded; i++)
            {
                char c = s[i];
                if (c == HttpConstants.PercentChar || c == HttpConstants.PlusSignChar && !isPath)
                {
                    firstEscaped = i;
                    break;
                }
            }
            if (firstEscaped == -1)
            {
                return s.Substring(from, len);
            }

            // Each encoded byte takes 3 characters (e.g. "%20")
            int decodedCapacity = (toExcluded - firstEscaped) / 3;
            var byteBuf = new byte[decodedCapacity];
            int idx;
            var strBuf = StringBuilderManager.Allocate(len);
            strBuf.Append(s, from, firstEscaped - from);

            for (int i = firstEscaped; i < toExcluded; i++)
            {
                char c = s[i];
                if (c != HttpConstants.PercentChar)
                {
                    strBuf.Append(c != HttpConstants.PlusSignChar || isPath ? c : StringUtil.Space);
                    continue;
                }

                idx = 0;
                do
                {
                    if (i + 3 > toExcluded)
                    {
                        StringBuilderManager.Free(strBuf); ThrowHelper.ThrowArgumentException_UnterminatedEscapeSeq(i, s);
                    }
                    byteBuf[idx++] = StringUtil.DecodeHexByte(s, i + 1);
                    i += 3;
                }
                while (i < toExcluded && s[i] == HttpConstants.PercentChar);
                i--;

                strBuf.Append(charset.GetString(byteBuf, 0, idx));
            }

            return StringBuilderManager.ReturnAndFree(strBuf);
        }

        static int FindPathEndIndex(string uri)
        {
            int len = uri.Length;
            for (int i = 0; i < len; i++)
            {
                char c = uri[i];
                switch (c)
                {
                    case HttpConstants.QuestionMarkChar:
                    case HttpConstants.NumberSignChar:
                        return i;
                }
            }
            return len;
        }
    }
}
