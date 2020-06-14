// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System.Text;
    using DotNetty.Common.Utilities;

    static class HttpMessageUtil
    {
        internal static StringBuilder AppendRequest(StringBuilder buf, IHttpRequest req)
        {
            AppendCommon(buf, req);
            AppendInitialLine(buf, req);
            AppendHeaders(buf, req.Headers);
            RemoveLastNewLine(buf);
            return buf;
        }

        internal static StringBuilder AppendResponse(StringBuilder buf, IHttpResponse res)
        {
            AppendCommon(buf, res);
            AppendInitialLine(buf, res);
            AppendHeaders(buf, res.Headers);
            RemoveLastNewLine(buf);
            return buf;
        }

        static void AppendCommon(StringBuilder buf, IHttpMessage msg)
        {
            _ = buf.Append($"{StringUtil.SimpleClassName(msg)}");
            _ = buf.Append("(decodeResult: ");
            _ = buf.Append(msg.Result);
            _ = buf.Append(", version: ");
            _ = buf.Append(msg.ProtocolVersion);
            _ = buf.Append($"){StringUtil.Newline}");
        }

        internal static StringBuilder AppendFullRequest(StringBuilder buf, IFullHttpRequest req)
        {
            AppendFullCommon(buf, req);
            AppendInitialLine(buf, req);
            AppendHeaders(buf, req.Headers);
            AppendHeaders(buf, req.TrailingHeaders);
            RemoveLastNewLine(buf);
            return buf;
        }

        internal static StringBuilder AppendFullResponse(StringBuilder buf, IFullHttpResponse res)
        {
            AppendFullCommon(buf, res);
            AppendInitialLine(buf, res);
            AppendHeaders(buf, res.Headers);
            AppendHeaders(buf, res.TrailingHeaders);
            RemoveLastNewLine(buf);
            return buf;
        }

        static void AppendFullCommon(StringBuilder buf, IFullHttpMessage msg)
        {
            _ = buf.Append(StringUtil.SimpleClassName(msg));
            _ = buf.Append("(decodeResult: ");
            _ = buf.Append(msg.Result);
            _ = buf.Append(", version: ");
            _ = buf.Append(msg.ProtocolVersion);
            _ = buf.Append(", content: ");
            _ = buf.Append(msg.Content);
            _ = buf.Append(')');
            _ = buf.Append(StringUtil.Newline);
        }

        static void AppendInitialLine(StringBuilder buf, IHttpRequest req) => 
            buf.Append($"{req.Method} {req.Uri} {req.ProtocolVersion}{StringUtil.Newline}");

        static void AppendInitialLine(StringBuilder buf, IHttpResponse res) => 
            buf.Append($"{res.ProtocolVersion} {res.Status}{StringUtil.Newline}");

        static void AppendHeaders(StringBuilder buf, HttpHeaders headers)
        {
            foreach(HeaderEntry<AsciiString, ICharSequence> e in headers)
            {
                _ = buf.Append($"{e.Key}:{e.Value}{StringUtil.Newline}");
            }
        }

        static void RemoveLastNewLine(StringBuilder buf) => buf.Length = buf.Length - StringUtil.Newline.Length;
    }
}
