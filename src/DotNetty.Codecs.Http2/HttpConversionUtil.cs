// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using CuteAnt.Pool;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;
    using DotNettyHttpVersion = DotNetty.Codecs.Http.HttpVersion;

    /// <summary>
    /// Provides utility methods and constants for the HTTP/2 to HTTP conversion
    /// </summary>
    public static class HttpConversionUtil
    {
        /// <summary>
        /// The set of headers that should not be directly copied when converting headers from HTTP to HTTP/2.
        /// </summary>
        static readonly CharSequenceMap<AsciiString> HttpToHttp2HeaderBlacklist;

        /// <summary>
        /// This will be the method used for <see cref="IHttpRequest"/> objects generated out of the HTTP message flow defined in <a
        /// href="http://tools.ietf.org/html/draft-ietf-httpbis-http2-16#section-8.1.">HTTP/2 Spec Message Flow</a>
        /// </summary>
        public static readonly HttpMethod OutOfMessageSequenceMethod = HttpMethod.Options;

        /// <summary>
        /// This will be the path used for <see cref="IHttpRequest"/> objects generated out of the HTTP message flow defined in <a
        /// href="http://tools.ietf.org/html/draft-ietf-httpbis-http2-16#section-8.1.">HTTP/2 Spec Message Flow</a>
        /// </summary>
        public static readonly string OutOfMessageSequencePath = "";

        /// <summary>
        /// This will be the status code used for <see cref="IHttpRequest"/> objects generated out of the HTTP message flow defined
        /// in <a href="http://tools.ietf.org/html/draft-ietf-httpbis-http2-16#section-8.1.">HTTP/2 Spec Message Flow</a>
        /// </summary>
        public static readonly HttpResponseStatus OutOfMessageSequenceReturnCode = HttpResponseStatus.OK;

        /// <summary>
        /// <a href="https://tools.ietf.org/html/rfc7540#section-8.1.2.3">rfc7540, 8.1.2.3</a> states the path must not
        /// be empty, and instead should be <c>"/"</c>.
        /// </summary>
        private static readonly AsciiString EmptyRequestPath = AsciiString.Cached(@"/");

        /// <summary>
        /// Provides the HTTP header extensions used to carry HTTP/2 information in HTTP objects
        /// </summary>
        public static class ExtensionHeaderNames
        {
            /// <summary>
            /// HTTP extension header which will identify the stream id from the HTTP/2 event(s) responsible for generating a
            /// <c>HttpObject</c>
            /// <para><c>"x-http2-stream-id"</c></para>
            /// </summary>
            public static readonly AsciiString StreamId = AsciiString.Cached("x-http2-stream-id");

            /// <summary>
            /// HTTP extension header which will identify the scheme pseudo header from the HTTP/2 event(s) responsible for
            /// generating a <c>HttpObject</c>
            /// <para><c>"x-http2-scheme"</c></para>
            /// </summary>
            public static readonly AsciiString Scheme = AsciiString.Cached("x-http2-scheme");

            /// <summary>
            /// HTTP extension header which will identify the path pseudo header from the HTTP/2 event(s) responsible for
            /// generating a <c>HttpObject</c>
            /// <para><c>"x-http2-path"</c></para>
            /// </summary>
            public static readonly AsciiString Path = AsciiString.Cached("x-http2-path");

            /// <summary>
            /// HTTP extension header which will identify the stream id used to create this stream in a HTTP/2 push promise
            /// frame
            /// <para><c>"x-http2-stream-promise-id"</c></para>
            /// </summary>
            public static readonly AsciiString StreamPromiseId = AsciiString.Cached("x-http2-stream-promise-id");

            /// <summary>
            /// HTTP extension header which will identify the stream id which this stream is dependent on. This stream will
            /// be a child node of the stream id associated with this header value.
            /// <para><c>"x-http2-stream-dependency-id"</c></para>
            /// </summary>
            public static readonly AsciiString StreamDependencyId = AsciiString.Cached("x-http2-stream-dependency-id");

            /// <summary>
            /// HTTP extension header which will identify the weight (if non-default and the priority is not on the default
            /// stream) of the associated HTTP/2 stream responsible responsible for generating a <c>HttpObject</c>
            /// <para><c>"x-http2-stream-weight"</c></para>
            /// </summary>
            public static readonly AsciiString StreamWeight = AsciiString.Cached("x-http2-stream-weight");
        }

        static HttpConversionUtil()
        {
            HttpToHttp2HeaderBlacklist = new CharSequenceMap<AsciiString>
            {
                { HttpHeaderNames.Connection, AsciiString.Empty },
                { HttpHeaderNames.KeepAlive, AsciiString.Empty },
                { HttpHeaderNames.ProxyConnection, AsciiString.Empty },
                { HttpHeaderNames.TransferEncoding, AsciiString.Empty },
                { HttpHeaderNames.Host, AsciiString.Empty },
                { HttpHeaderNames.Upgrade, AsciiString.Empty },
                { ExtensionHeaderNames.StreamId, AsciiString.Empty },
                { ExtensionHeaderNames.Scheme, AsciiString.Empty },
                { ExtensionHeaderNames.Path, AsciiString.Empty }
            };
        }

        /// <summary>
        /// Apply HTTP/2 rules while translating status code to <see cref="HttpResponseStatus"/>
        /// </summary>
        /// <param name="status">The status from an HTTP/2 frame</param>
        /// <returns>The HTTP/1.x status</returns>
        /// <exception cref="Http2Exception">If there is a problem translating from HTTP/2 to HTTP/1.x</exception>
        public static HttpResponseStatus ParseStatus(ICharSequence status)
        {
            HttpResponseStatus result = null;
            try
            {
                result = HttpResponseStatus.ParseLine(status);
                if (result == HttpResponseStatus.SwitchingProtocols)
                {
                    ThrowHelper.ThrowConnectionError_InvalidHttp2StatusCode(result.Code);
                }
            }
            catch (Http2Exception e)
            {
                throw e;
            }
            catch (Exception t)
            {
                ThrowHelper.ThrowConnectionError_UnrecognizedHttpStatusCode(t, status);
            }
            return result;
        }

        /// <summary>
        /// Create a new object to contain the response data
        /// </summary>
        /// <param name="streamId">The stream associated with the response</param>
        /// <param name="http2Headers">The initial set of HTTP/2 headers to create the response with</param>
        /// <param name="alloc">The <see cref="IByteBufferAllocator"/> to use to generate the content of the message</param>
        /// <param name="validateHttpHeaders"><c>true</c> to validate HTTP headers in the http-codec
        /// <para><c>false</c> not to validate HTTP headers in the http-codec</para></param>
        /// <returns>A new response object which represents headers/data</returns>
        /// <exception cref="Http2Exception">see <see cref="AddHttp2ToHttpHeaders(int, IHttp2Headers, IFullHttpMessage, bool)"/></exception>
        public static IFullHttpResponse ToFullHttpResponse(int streamId, IHttp2Headers http2Headers, IByteBufferAllocator alloc,
            bool validateHttpHeaders)
        {
            HttpResponseStatus status = ParseStatus(http2Headers.Status);
            // HTTP/2 does not define a way to carry the version or reason phrase that is included in an
            // HTTP/1.1 status line.
            IFullHttpResponse msg = new DefaultFullHttpResponse(DotNettyHttpVersion.Http11, status, alloc.Buffer(),
                                                                validateHttpHeaders);
            try
            {
                AddHttp2ToHttpHeaders(streamId, http2Headers, msg, false);
            }
            catch (Http2Exception e)
            {
                msg.Release();
                throw e;
            }
            catch (Exception t)
            {
                msg.Release();
                ThrowHelper.ThrowStreamError_Http2ToHttp1HeadersConversionError(streamId, t);
            }
            return msg;
        }

        /// <summary>
        /// Create a new object to contain the request data
        /// </summary>
        /// <param name="streamId">The stream associated with the request</param>
        /// <param name="http2Headers">The initial set of HTTP/2 headers to create the request with</param>
        /// <param name="alloc">The <see cref="IByteBufferAllocator"/> to use to generate the content of the message</param>
        /// <param name="validateHttpHeaders"><c>true</c> to validate HTTP headers in the http-codec
        /// <para><c>false</c> not to validate HTTP headers in the http-codec</para></param>
        /// <returns>A new request object which represents headers/data</returns>
        /// <exception cref="Http2Exception">see <see cref="AddHttp2ToHttpHeaders(int, IHttp2Headers, IFullHttpMessage, bool)"/></exception>
        public static IFullHttpRequest ToFullHttpRequest(int streamId, IHttp2Headers http2Headers, IByteBufferAllocator alloc,
            bool validateHttpHeaders)
        {
            // HTTP/2 does not define a way to carry the version identifier that is included in the HTTP/1.1 request line.
            var method = http2Headers.Method;
            if (method is null) { ThrowHelper.ThrowArgumentNullException_MethodHeader(); }
            var path = http2Headers.Path;
            if (path is null) { ThrowHelper.ThrowArgumentNullException_PathHeader(); }
            var msg = new DefaultFullHttpRequest(DotNettyHttpVersion.Http11, HttpMethod.ValueOf(AsciiString.Of(method)),
                path.ToString(), alloc.Buffer(), validateHttpHeaders);
            try
            {
                AddHttp2ToHttpHeaders(streamId, http2Headers, msg, false);
            }
            catch (Http2Exception e)
            {
                msg.Release();
                throw e;
            }
            catch (Exception t)
            {
                msg.Release();
                ThrowHelper.ThrowStreamError_Http2ToHttp1HeadersConversionError(streamId, t);
            }
            return msg;
        }

        /// <summary>
        /// Create a new object to contain the request data.
        /// </summary>
        /// <param name="streamId">The stream associated with the request</param>
        /// <param name="http2Headers">The initial set of HTTP/2 headers to create the request with</param>
        /// <param name="validateHttpHeaders"><c>true</c> to validate HTTP headers in the http-codec
        /// <para><c>false</c> not to validate HTTP headers in the http-codec</para></param>
        /// <returns>A new request object which represents headers for a chunked request</returns>
        /// <exception cref="Http2Exception">See <see cref="AddHttp2ToHttpHeaders(int, IHttp2Headers, HttpHeaders, DotNettyHttpVersion, bool, bool)"/></exception>
        public static IHttpRequest ToHttpRequest(int streamId, IHttp2Headers http2Headers, bool validateHttpHeaders)
        {
            // HTTP/2 does not define a way to carry the version identifier that is included in the HTTP/1.1 request line.
            var method = http2Headers.Method;
            if (method is null) { ThrowHelper.ThrowArgumentNullException_MethodHeader(); }
            var path = http2Headers.Path;
            if (path is null) { ThrowHelper.ThrowArgumentNullException_PathHeader(); }

            var msg = new DefaultHttpRequest(DotNettyHttpVersion.Http11, HttpMethod.ValueOf(AsciiString.Of(method)),
                    path.ToString(), validateHttpHeaders);
            try
            {
                AddHttp2ToHttpHeaders(streamId, http2Headers, msg.Headers, msg.ProtocolVersion, false, true);
            }
            catch (Http2Exception e)
            {
                throw e;
            }
            catch (Exception t)
            {
                ThrowHelper.ThrowStreamError_Http2ToHttp1HeadersConversionError(streamId, t);
            }
            return msg;
        }

        /// <summary>
        /// Create a new object to contain the response data.
        /// </summary>
        /// <param name="streamId">The stream associated with the response</param>
        /// <param name="http2Headers">The initial set of HTTP/2 headers to create the response with</param>
        /// <param name="validateHttpHeaders"><c>true</c> to validate HTTP headers in the http-codec
        /// <para><c>false</c> not to validate HTTP headers in the http-codec</para></param>
        /// <returns>A new response object which represents headers for a chunked response</returns>
        /// <exception cref="Http2Exception">See <see cref="AddHttp2ToHttpHeaders(int, IHttp2Headers, HttpHeaders, DotNettyHttpVersion, bool, bool)"/></exception>
        public static IHttpResponse ToHttpResponse(int streamId, IHttp2Headers http2Headers, bool validateHttpHeaders)
        {
            var status = ParseStatus(http2Headers.Status);
            // HTTP/2 does not define a way to carry the version or reason phrase that is included in an
            // HTTP/1.1 status line.
            var msg = new DefaultHttpResponse(DotNettyHttpVersion.Http11, status, validateHttpHeaders);
            try
            {
                AddHttp2ToHttpHeaders(streamId, http2Headers, msg.Headers, msg.ProtocolVersion, false, true);
            }
            catch (Http2Exception e)
            {
                throw e;
            }
            catch (Exception t)
            {
                ThrowHelper.ThrowStreamError_Http2ToHttp1HeadersConversionError(streamId, t);
            }
            return msg;
        }

        /// <summary>
        /// Translate and add HTTP/2 headers to HTTP/1.x headers.
        /// </summary>
        /// <param name="streamId">The stream associated with <paramref name="sourceHeaders"/>.</param>
        /// <param name="sourceHeaders">The HTTP/2 headers to convert.</param>
        /// <param name="destinationMessage">The object which will contain the resulting HTTP/1.x headers.</param>
        /// <param name="addToTrailer"><c>true</c> to add to trailing headers. <c>false</c> to add to initial headers.</param>
        /// <exception cref="Http2Exception">If not all HTTP/2 headers can be translated to HTTP/1.x.</exception>
        public static void AddHttp2ToHttpHeaders(int streamId, IHttp2Headers sourceHeaders,
            IFullHttpMessage destinationMessage, bool addToTrailer)
        {
            AddHttp2ToHttpHeaders(streamId, sourceHeaders,
                    addToTrailer ? destinationMessage.TrailingHeaders : destinationMessage.Headers,
                    destinationMessage.ProtocolVersion, addToTrailer, destinationMessage is IHttpRequest);
        }

        /// <summary>
        /// Translate and add HTTP/2 headers to HTTP/1.x headers.
        /// </summary>
        /// <param name="streamId">The stream associated with <paramref name="outputHeaders"/>.</param>
        /// <param name="inputHeaders">The HTTP/2 headers to convert.</param>
        /// <param name="outputHeaders">The object which will contain the resulting HTTP/1.x headers..</param>
        /// <param name="httpVersion">What HTTP/1.x version <paramref name="outputHeaders"/> should be treated as when doing the conversion.</param>
        /// <param name="isTrailer"><c>true</c> if <paramref name="outputHeaders"/> should be treated as trailing headers.
        /// <c>false</c> otherwise.</param>
        /// <param name="isRequest"><c>true</c> if the <paramref name="outputHeaders"/> will be used in a request message.
        /// <c>false</c> for response message.</param>
        /// <exception cref="Http2Exception">If not all HTTP/2 headers can be translated to HTTP/1.x.</exception>
        public static void AddHttp2ToHttpHeaders(int streamId, IHttp2Headers inputHeaders, HttpHeaders outputHeaders,
            DotNettyHttpVersion httpVersion, bool isTrailer, bool isRequest)
        {
            Http2ToHttpHeaderTranslator translator = new Http2ToHttpHeaderTranslator(streamId, outputHeaders, isRequest);
            try
            {
                foreach (var entry in inputHeaders)
                {
                    translator.Translate(entry);
                }
            }
            catch (Http2Exception ex)
            {
                throw ex;
            }
            catch (Exception t)
            {
                ThrowHelper.ThrowStreamError_Http2ToHttp1HeadersConversionError(streamId, t);
            }

            outputHeaders.Remove(HttpHeaderNames.TransferEncoding);
            outputHeaders.Remove(HttpHeaderNames.Trailer);
            if (!isTrailer)
            {
                outputHeaders.SetInt(ExtensionHeaderNames.StreamId, streamId);
                HttpUtil.SetKeepAlive(outputHeaders, httpVersion, true);
            }
        }

        /// <summary>
        /// Converts the given HTTP/1.x headers into HTTP/2 headers.
        /// The following headers are only used if they can not be found in from the <c>HOST</c> header or the
        /// <c>Request-Line</c> as defined by <a href="https://tools.ietf.org/html/rfc7230">rfc7230</a>
        /// <para><see cref="ExtensionHeaderNames.Scheme"/></para>
        /// <see cref="ExtensionHeaderNames.Path"/> is ignored and instead extracted from the <c>Request-Line</c>.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="validateHeaders"></param>
        /// <returns></returns>
        public static IHttp2Headers ToHttp2Headers(IHttpMessage input, bool validateHeaders)
        {
            HttpHeaders inHeaders = input.Headers;
            IHttp2Headers output = new DefaultHttp2Headers(validateHeaders, inHeaders.Size);
            if (input is IHttpRequest request)
            {
                output.Method = request.Method.AsciiName;
                // TODO java.net.URI isOriginForm  isAsteriskForm
                if (Uri.TryCreate(request.Uri, UriKind.Absolute, out var requestTargetUri) && !requestTargetUri.IsFile) // .NETCore 非Windows系统，会把 '/' 打头的相对 url 默认为 UnixFileUri
                {
                    output.Path = ToHttp2Path(requestTargetUri);
                    SetHttp2Scheme(inHeaders, requestTargetUri, output);

                    var authority = requestTargetUri.Authority;
                    // Attempt to take from HOST header before taking from the request-line
                    var host = inHeaders.GetAsString(HttpHeaderNames.Host);
                    SetHttp2Authority(string.IsNullOrEmpty(host) ? requestTargetUri.Authority : host, output);
                }
                else
                {
                    output.Path = new AsciiString(request.Uri);
                    // Consume the Scheme extension header if present
                    if (inHeaders.TryGet(ExtensionHeaderNames.Scheme, out var cValue))
                    {
                        output.Scheme = AsciiString.Of(cValue);
                    }
                    if (output.Scheme is null)
                    {
                        ThrowHelper.ThrowArgumentException_SchemeMustBeSpecified();
                    }
                    var host = inHeaders.GetAsString(HttpHeaderNames.Host);
                    SetHttp2Authority(host, output);
                }
            }
            else if (input is IHttpResponse response)
            {
                output.Status = response.Status.CodeAsText;
            }

            // Add the HTTP headers which have not been consumed above
            ToHttp2Headers(inHeaders, output);
            return output;
        }

        public static IHttp2Headers ToHttp2Headers(HttpHeaders inHeaders, bool validateHeaders)
        {
            if (inHeaders.IsEmpty) { return EmptyHttp2Headers.Instance; }

            var output = new DefaultHttp2Headers(validateHeaders, inHeaders.Size);
            ToHttp2Headers(inHeaders, output);
            return output;
        }

        private static CharSequenceMap<AsciiString> ToLowercaseMap(IEnumerable<ICharSequence> values, int arraySizeHint)
        {
            var valueConverter = UnsupportedValueConverter<AsciiString>.Instance;
            var result = new CharSequenceMap<AsciiString>(true, valueConverter, arraySizeHint);

            foreach (var item in values)
            {
                AsciiString lowerCased = AsciiString.Of(item).ToLowerCase();
                try
                {
                    int index = lowerCased.ForEachByte(ByteProcessor.FindComma);
                    if (index != -1)
                    {
                        int start = 0;
                        do
                        {
                            result.Add(lowerCased.SubSequence(start, index, false).Trim(), AsciiString.Empty);
                            start = index + 1;
                        } while (start < lowerCased.Count &&
                                (index = lowerCased.ForEachByte(start, lowerCased.Count - start, ByteProcessor.FindComma)) != -1);
                        result.Add(lowerCased.SubSequence(start, lowerCased.Count, false).Trim(), AsciiString.Empty);
                    }
                    else
                    {
                        result.Add(lowerCased.Trim(), AsciiString.Empty);
                    }
                }
                catch (Exception)
                {
                    // This is not expect to happen because FIND_COMMA never throws but must be caught
                    // because of the ByteProcessor interface.
                    ThrowHelper.ThrowInvalidOperationException();
                }
            }

            return result;
        }

        /// <summary>
        /// Filter the <see cref="HttpHeaderNames.Te"/> header according to the
        /// <a href="https://tools.ietf.org/html/rfc7540#section-8.1.2.2">special rules in the HTTP/2 RFC</a>.
        /// </summary>
        /// <param name="entry">An entry whose name is <see cref="HttpHeaderNames.Te"/>.</param>
        /// <param name="output">the resulting HTTP/2 headers.</param>
        private static void ToHttp2HeadersFilterTE(HeaderEntry<AsciiString, ICharSequence> entry, IHttp2Headers output)
        {
            if (AsciiString.IndexOf(entry.Value, ',', 0) == -1)
            {
                if (AsciiString.ContentEqualsIgnoreCase(AsciiString.Trim(entry.Value), HttpHeaderValues.Trailers))
                {
                    output.Add(HttpHeaderNames.Te, HttpHeaderValues.Trailers);
                }
            }
            else
            {
                var teValues = StringUtil.UnescapeCsvFields(entry.Value);
                foreach (var teValue in teValues)
                {
                    if (AsciiString.ContentEqualsIgnoreCase(AsciiString.Trim(teValue), HttpHeaderValues.Trailers))
                    {
                        output.Add(HttpHeaderNames.Te, HttpHeaderValues.Trailers);
                        break;
                    }
                }
            }
        }

        public static void ToHttp2Headers(HttpHeaders inHeaders, IHttp2Headers output)
        {
            // Choose 8 as a default size because it is unlikely we will see more than 4 Connection headers values, but
            // still allowing for "enough" space in the map to reduce the chance of hash code collision.
            var connectionBlacklist = ToLowercaseMap(inHeaders.GetAll(HttpHeaderNames.Connection), 8);

            foreach (var entry in inHeaders)
            {
                AsciiString aName = entry.Key.ToLowerCase();
                if (!HttpToHttp2HeaderBlacklist.Contains(aName) && !connectionBlacklist.Contains(aName))
                {
                    // https://tools.ietf.org/html/rfc7540#section-8.1.2.2 makes a special exception for TE
                    if (aName.ContentEqualsIgnoreCase(HttpHeaderNames.Te))
                    {
                        ToHttp2HeadersFilterTE(entry, output);
                    }
                    else if (aName.ContentEqualsIgnoreCase(HttpHeaderNames.Cookie))
                    {
                        AsciiString value = AsciiString.Of(entry.Value);
                        uint uValueCount = (uint)value.Count;
                        // split up cookies to allow for better compression
                        // https://tools.ietf.org/html/rfc7540#section-8.1.2.5
                        try
                        {
                            int index = value.ForEachByte(ByteProcessor.FindSemicolon);
                            if (uValueCount > (uint)index) // != -1
                            {
                                int start = 0;
                                do
                                {
                                    output.Add(HttpHeaderNames.Cookie, value.SubSequence(start, index, false));
                                    // skip 2 characters "; " (see https://tools.ietf.org/html/rfc6265#section-4.2.1)
                                    start = index + 2;
                                } while ((uint)start < uValueCount &&
                                        uValueCount > (uint)(index = value.ForEachByte(start, value.Count - start, ByteProcessor.FindSemicolon))); // != -1
                                if ((uint)start >= uValueCount)
                                {
                                    ThrowHelper.ThrowArgumentException_CookieValueIsOfUnexpectedFormat(value);
                                }
                                output.Add(HttpHeaderNames.Cookie, value.SubSequence(start, value.Count, false));
                            }
                            else
                            {
                                output.Add(HttpHeaderNames.Cookie, value);
                            }
                        }
                        catch (Exception ex)
                        {
                            var str = ex.ToString();
                            // This is not expect to happen because FIND_SEMI_COLON never throws but must be caught
                            // because of the ByteProcessor interface.
                            ThrowHelper.ThrowInvalidOperationException();
                        }
                    }
                    else
                    {
                        output.Add(aName, entry.Value);
                    }
                }
            }
        }



        /// <summary>
        /// Generate a HTTP/2 <c> :path</c> from a URI in accordance with
        /// <a href="https://tools.ietf.org/html/rfc7230#section-5.3">rfc7230, 5.3</a>.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static AsciiString ToHttp2Path(Uri uri)
        {
            var pathBuilder = StringBuilderManager.Allocate();
            var pathAndQuery = uri.PathAndQuery;
            if (!string.IsNullOrEmpty(pathAndQuery))
            {
                pathBuilder.Append(pathAndQuery);
            }
            var fragment = uri.Fragment;
            if (!string.IsNullOrEmpty(fragment))
            {
                pathBuilder.Append(fragment);
            }
            var path = StringBuilderManager.ReturnAndFree(pathBuilder);
            return string.IsNullOrEmpty(path) ? EmptyRequestPath : new AsciiString(path);
        }

        // package-private for testing only
        internal static void SetHttp2Authority(string authority, IHttp2Headers output)
        {
            // The authority MUST NOT include the deprecated "userinfo" subcomponent
            if (authority is object)
            {
                if (StringUtil.IsEmpty(authority))
                {
                    output.Authority = AsciiString.Empty;
                }
                else
                {
                    int start = authority.IndexOf('@') + 1;
                    int length = authority.Length - start;
                    if (0u >= (uint)length)
                    {
                        ThrowHelper.ThrowArgumentException_Http2AuthorityIsEmpty(authority);
                    }
                    output.Authority = new AsciiString(authority, start, length);
                }
            }
        }

        private static void SetHttp2Scheme(HttpHeaders input, Uri uri, IHttp2Headers output)
        {
            var value = uri.Scheme;
            if (value is object)
            {
                output.Scheme = new AsciiString(value);
                return;
            }

            // Consume the Scheme extension header if present
            if (input.TryGet(ExtensionHeaderNames.Scheme, out var cValue))
            {
                output.Scheme = AsciiString.Of(cValue);
                return;
            }

            if (uri.Port == HttpScheme.Https.Port)
            {
                output.Scheme = HttpScheme.Https.Name;
            }
            else if (uri.Port == HttpScheme.Http.Port)
            {
                output.Scheme = HttpScheme.Http.Name;
            }

            ThrowHelper.ThrowArgumentException_SchemeMustBeSpecified();
        }

        sealed class Http2ToHttpHeaderTranslator
        {
            /// <summary>
            /// Translations from HTTP/2 header name to the HTTP/1.x equivalent.
            /// </summary>
            private static readonly CharSequenceMap<AsciiString> RequestHeaderTranslations;
            private static readonly CharSequenceMap<AsciiString> ResponseHeaderTranslations;

            static Http2ToHttpHeaderTranslator()
            {
                RequestHeaderTranslations = new CharSequenceMap<AsciiString>();
                ResponseHeaderTranslations = new CharSequenceMap<AsciiString>();

                ResponseHeaderTranslations.Add(PseudoHeaderName.Authority.Value, HttpHeaderNames.Host);
                ResponseHeaderTranslations.Add(PseudoHeaderName.Scheme.Value, ExtensionHeaderNames.Scheme);
                RequestHeaderTranslations.Add(ResponseHeaderTranslations);
                ResponseHeaderTranslations.Add(PseudoHeaderName.Path.Value, ExtensionHeaderNames.Path);
            }


            private readonly int streamId;
            private readonly HttpHeaders output;
            private readonly CharSequenceMap<AsciiString> translations;

            public Http2ToHttpHeaderTranslator(int streamId, HttpHeaders output, bool request)
            {
                this.streamId = streamId;
                this.output = output;
                this.translations = request ? RequestHeaderTranslations : ResponseHeaderTranslations;
            }

            public void Translate(HeaderEntry<ICharSequence, ICharSequence> entry)
            {
                var name = entry.Key;
                var value = entry.Value;

                if (this.translations.TryGet(name, out var translatedName))
                {
                    output.Add(translatedName, AsciiString.Of(value));
                }
                else if (!PseudoHeaderName.IsPseudoHeader(name))
                {
                    // https://tools.ietf.org/html/rfc7540#section-8.1.2.3
                    // All headers that start with ':' are only valid in HTTP/2 context
                    if (0u >= (uint)name.Count || name[0] == ':')
                    {
                        ThrowHelper.ThrowStreamError_InvalidHttp2HeaderEncounteredInTranslationToHttp1(streamId, name);
                    }
                    var cookie = HttpHeaderNames.Cookie;
                    if (cookie.Equals(name))
                    {
                        // combine the cookie values into 1 header entry.
                        // https://tools.ietf.org/html/rfc7540#section-8.1.2.5
                        var existingCookie = output.Get(cookie, null);
                        output.Set(cookie,
                                   (existingCookie is object) ? new StringCharSequence(existingCookie + "; " + value) : value);
                    }
                    else
                    {
                        output.Add(AsciiString.Of(name), value);
                    }
                }
            }
        }
    }
}
