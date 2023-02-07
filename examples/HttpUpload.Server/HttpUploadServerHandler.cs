namespace HttpUpload.Server
{
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http.Cookies;
    using DotNetty.Codecs.Http.Multipart;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;

    public class HttpUploadServerHandler : SimpleChannelInboundHandler2<IHttpObject>
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<HttpUploadServerHandler>();
        static readonly IHttpDataFactory s_factory =
            new DefaultHttpDataFactory(DefaultHttpDataFactory.MinSize); // Disk if size exceed

        IHttpRequest _request;
        IHttpData _partialContent;
        readonly StringBuilder _responseContent = new StringBuilder();
        HttpPostRequestDecoder _decoder;

        static HttpUploadServerHandler()
        {
            DiskFileUpload.DeleteOnExitTemporaryFile = true; // should delete file
                                                             // on exit (in normal
                                                             // exit)
                                                             //DiskFileUpload.FileBaseDirectory = @"e:\temp"; // system temp directory
            DiskAttribute.DeleteOnExitTemporaryFile = true; // should delete file on
                                                            // exit (in normal exit)
                                                            //DiskAttribute.DiskBaseDirectory = @"e:\temp"; // system temp directory
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            if (_decoder != null)
            {
                _decoder.CleanFiles();
            }
        }

        protected override void ChannelRead0(IChannelHandlerContext context, IHttpObject message)
        {
            if (message is IHttpRequest request)
            {
                s_logger.LogTrace("=========The Request Header========");
                s_logger.LogDebug(request.ToString());
                s_logger.LogTrace("===================================");
                _request = request;
                var uriPath = GetPath(request.Uri);
                if (!uriPath.StartsWith("/form"))
                {
                    // Write Menu
                    WriteMenu(context);
                    return;
                }
                _responseContent.Clear();
                _responseContent.Append("WELCOME TO THE WILD WILD WEB SERVER\r\n");
                _responseContent.Append("===================================\r\n");

                _responseContent.Append($"VERSION: {request.ProtocolVersion.Text}\r\n");

                _responseContent.Append($"REQUEST_URI: {request.Uri}\r\n\r\n");
                _responseContent.Append("\r\n\r\n");

                // new getMethod
                foreach (var entry in request.Headers)
                {
                    _responseContent.Append($"HEADER: {entry.Key}={entry.Value}\r\n");
                }
                _responseContent.Append("\r\n\r\n");

                // new getMethod
                ISet<ICookie> cookies;
                string value = request.Headers.GetAsString(HttpHeaderNames.Cookie);
                if (value == null)
                {
                    cookies = new HashSet<ICookie>();
                }
                else
                {
                    cookies = ServerCookieDecoder.StrictDecoder.Decode(value);
                }
                foreach (var cookie in cookies)
                {
                    _responseContent.Append($"COOKIE: {cookie}\r\n");
                }
                _responseContent.Append("\r\n\r\n");

                QueryStringDecoder decoderQuery = new QueryStringDecoder(request.Uri);
                var uriAttributes = decoderQuery.Parameters;
                foreach (var attr in uriAttributes)
                {
                    foreach (var attrVal in attr.Value)
                    {
                        _responseContent.Append($"URI: {attr.Key}={attrVal}\r\n");
                    }
                }
                _responseContent.Append("\r\n\r\n");

                // if GET Method: should not try to create an HttpPostRequestDecoder
                if (HttpMethod.Get.Equals(request.Method))
                {
                    // GET Method: should not try to create an HttpPostRequestDecoder
                    // So stop here
                    _responseContent.Append("\r\n\r\nEND OF GET CONTENT\r\n");
                    // Not now: LastHttpContent will be sent writeResponse(ctx.channel());
                    return;
                }
                try
                {
                    _decoder = new HttpPostRequestDecoder(s_factory, request);
                }
                catch (ErrorDataDecoderException e1)
                {
                    s_logger.LogError(e1.ToString());
                    _responseContent.Append(e1.Message);
                    WriteResponseAsync(context.Channel, true);
                    return;
                }

                var readingChunks = HttpUtil.IsTransferEncodingChunked(request);
                _responseContent.Append($"Is Chunked: {readingChunks}\r\n");
                _responseContent.Append($"IsMultipart: {_decoder.IsMultipart}\r\n");
                if (readingChunks)
                {
                    // Chunk version
                    _responseContent.Append("Chunks: ");
                }
            }

            // check if the decoder was constructed before
            // if not it handles the form get
            if (_decoder != null)
            {
                if (message is IHttpContent chunk) // New chunk is received
                {
                    try
                    {
                        _decoder.Offer(chunk);
                    }
                    catch (ErrorDataDecoderException e1)
                    {
                        s_logger.LogError(e1.ToString());
                        _responseContent.Append(e1.Message);
                        WriteResponseAsync(context.Channel, true);
                        return;
                    }
                    _responseContent.Append('o');
                    // example of reading chunk by chunk (minimize memory usage due to
                    // Factory)
                    ReadHttpDataChunkByChunk();
                    // example of reading only if at the end
                    if (chunk is ILastHttpContent)
                    {
                        WriteResponseAsync(context.Channel);

                        Reset();
                    }
                }
            }
            else
            {
                WriteResponseAsync(context.Channel);
            }
        }

        private void Reset()
        {
            _request = null;

            // destroy the decoder to release all resources
            _decoder.Destroy();
            _decoder = null;
        }

        /**
         * Example of reading request by chunk and getting values from chunk to chunk
         */
        private void ReadHttpDataChunkByChunk()
        {
            try
            {
                IInterfaceHttpData data = null;
                while (_decoder.HasNext)
                {
                    data = _decoder.Next();
                    if (data != null)
                    {
                        // check if current HttpData is a FileUpload and previously set as partial
                        if (_partialContent == data)
                        {
                            s_logger.LogInformation(" 100% (FinalSize: " + _partialContent.Length + ")");
                            _partialContent = null;
                        }
                        // new value
                        WriteHttpData(data);
                    }
                }
                // Check partial decoding for a FileUpload
                data = _decoder.CurrentPartialHttpData;
                if (data != null)
                {
                    StringBuilder builder = new StringBuilder();
                    if (_partialContent == null)
                    {
                        _partialContent = (IHttpData)data;
                        if (_partialContent is IFileUpload fileUpload)
                        {
                            builder.Append($"Start FileUpload: {fileUpload.FileName} ");
                        }
                        else
                        {
                            builder.Append($"Start Attribute: {_partialContent.Name} ");
                        }
                        builder.Append("(DefinedSize: ").Append(_partialContent.DefinedLength).Append(")");
                    }
                    if (_partialContent.DefinedLength > 0)
                    {
                        builder.Append(" ").Append(_partialContent.Length * 100 / _partialContent.DefinedLength)
                            .Append("% ");
                        s_logger.LogInformation(builder.ToString());
                    }
                    else
                    {
                        builder.Append(" ").Append(_partialContent.Length).Append(" ");
                        s_logger.LogInformation(builder.ToString());
                    }
                }
            }
            catch (EndOfDataDecoderException)
            {
                // end
                _responseContent.Append("\r\n\r\nEND OF CONTENT CHUNK BY CHUNK\r\n\r\n");
            }
        }

        private void WriteHttpData(IInterfaceHttpData data)
        {
            if (data.DataType == HttpDataType.Attribute)
            {
                var attribute = (IAttribute)data;
                string value;

                try
                {
                    value = attribute.Value;
                }
                catch (Exception e1)
                {
                    // Error while reading data from File, only print name and error
                    s_logger.LogError(e1.ToString());
                    _responseContent.Append("\r\nBODY Attribute: " + attribute.DataType + ": "
                            + attribute.Name + " Error while reading value: " + e1.Message + "\r\n");
                    return;
                }

                if (value.Length > 100)
                {
                    _responseContent.Append($"\r\nBODY Attribute: {attribute.DataType}: {attribute.Name} data too long\r\n");
                }
                else
                {
                    _responseContent.Append($"\r\nBODY Attribute: {attribute.DataType}: {attribute}\r\n");
                }
            }
            else
            {
                _responseContent.Append($"\r\nBODY FileUpload: {data.DataType}: {data}\r\n");
                if (data.DataType == HttpDataType.FileUpload)
                {
                    var fileUpload = (IFileUpload)data;
                    if (fileUpload.IsCompleted)
                    {
                        if (fileUpload.Length < 10000)
                        {
                            _responseContent.Append("\tContent of file\r\n");
                            try
                            {
                                _responseContent.Append(fileUpload.GetString(fileUpload.Charset));
                            }
                            catch (Exception e1)
                            {
                                // do nothing for the example
                                s_logger.LogError(e1.ToString());
                            }
                            _responseContent.Append("\r\n");
                        }
                        else
                        {
                            _responseContent.Append($"\tFile too long to be printed out:{fileUpload.Length}\r\n");
                        }
                        // fileUpload.isInMemory();// tells if the file is in Memory
                        // or on File
                        // fileUpload.renameTo(dest); // enable to move into another
                        // File dest
                        // decoder.removeFileUploadFromClean(fileUpload); //remove
                        // the File of to delete file
                    }
                    else
                    {
                        _responseContent.Append("\tFile to be continued but should not!\r\n");
                    }
                }
            }
        }

        private Task WriteResponseAsync(IChannel channel) => WriteResponseAsync(channel, false);

        private Task WriteResponseAsync(IChannel channel, bool forceClose)
        {
            // Convert the response content to a ChannelBuffer.
            var buf = Unpooled.CopiedBuffer(_responseContent.ToString(), Encoding.UTF8);
            _responseContent.Clear();

            // Decide whether to close the connection or not.
            var keepAlive = HttpUtil.IsKeepAlive(_request) && !forceClose;

            // Build the response object.
            var response = new DefaultFullHttpResponse(
                    HttpVersion.Http11, HttpResponseStatus.OK, buf);
            response.Headers.Set(HttpHeaderNames.ContentType, "text/plain; charset=UTF-8");

            response.Headers.SetInt(HttpHeaderNames.ContentLength, buf.ReadableBytes);

            if (!keepAlive)
            {
                response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Close);
            }
            else if (_request.ProtocolVersion.Equals(HttpVersion.Http10))
            {
                response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.KeepAlive);
            }

            ISet<ICookie> cookies;
            var value = _request.Headers.GetAsString(HttpHeaderNames.Cookie);
            if (value == null)
            {
                cookies = new HashSet<ICookie>();
            }
            else
            {
                cookies = ServerCookieDecoder.StrictDecoder.Decode(value);
            }
            if (cookies.Count > 0)
            {
                // Reset the cookies if necessary.
                foreach (var cookie in cookies)
                {
                    response.Headers.Add(HttpHeaderNames.SetCookie, ServerCookieEncoder.StrictEncoder.Encode(cookie));
                }
            }
            // Write the response.
            var future = channel.WriteAndFlushAsync(response);
            // Close the connection after the write operation is done if necessary.
            if (!keepAlive)
            {
                future.ContinueWith(t => channel.CloseAsync(), TaskContinuationOptions.ExecuteSynchronously);
            }
            return future;
        }

        private void WriteMenu(IChannelHandlerContext context)
        {
            // print several HTML forms
            // Convert the response content to a ChannelBuffer.
            _responseContent.Clear();

            // create Pseudo Menu
            _responseContent.AppendLine("<html>");
            _responseContent.AppendLine("<head>");
            _responseContent.AppendLine("<title>Netty Test Form</title>\r\n");
            _responseContent.AppendLine("</head>\r\n");
            _responseContent.AppendLine("<body bgcolor=white><style>td{font-size: 12pt;}</style>");

            _responseContent.AppendLine("<table border=\"0\">");
            _responseContent.AppendLine("<tr>");
            _responseContent.AppendLine("<td>");
            _responseContent.AppendLine("<h1>Netty Test Form</h1>");
            _responseContent.AppendLine("Choose one FORM");
            _responseContent.AppendLine("</td>");
            _responseContent.AppendLine("</tr>");
            _responseContent.AppendLine("</table>\r\n");

            // GET
            _responseContent.AppendLine("<CENTER>GET FORM<HR WIDTH=\"75%\" NOSHADE color=\"blue\"></CENTER>");
            _responseContent.AppendLine("<FORM ACTION=\"/formget\" METHOD=\"GET\">");
            _responseContent.AppendLine("<input type=hidden name=getform value=\"GET\">");
            _responseContent.AppendLine("<table border=\"0\">");
            _responseContent.AppendLine("<tr><td>Fill with value: <br> <input type=text name=\"info\" size=10></td></tr>");
            _responseContent.AppendLine("<tr><td>Fill with value: <br> <input type=text name=\"secondinfo\" size=20>");
            _responseContent
                    .AppendLine("<tr><td>Fill with value: <br> <textarea name=\"thirdinfo\" cols=40 rows=10></textarea>");
            _responseContent.AppendLine("</td></tr>");
            _responseContent.AppendLine("<tr><td><INPUT TYPE=\"submit\" NAME=\"Send\" VALUE=\"Send\"></INPUT></td>");
            _responseContent.AppendLine("<td><INPUT TYPE=\"reset\" NAME=\"Clear\" VALUE=\"Clear\" ></INPUT></td></tr>");
            _responseContent.AppendLine("</table></FORM>\r\n");
            _responseContent.AppendLine("<CENTER><HR WIDTH=\"75%\" NOSHADE color=\"blue\"></CENTER>");

            // POST
            _responseContent.AppendLine("<CENTER>POST FORM<HR WIDTH=\"75%\" NOSHADE color=\"blue\"></CENTER>");
            _responseContent.AppendLine("<FORM ACTION=\"/formpost\" METHOD=\"POST\">");
            _responseContent.AppendLine("<input type=hidden name=getform value=\"POST\">");
            _responseContent.AppendLine("<table border=\"0\">");
            _responseContent.AppendLine("<tr><td>Fill with value: <br> <input type=text name=\"info\" size=10></td></tr>");
            _responseContent.AppendLine("<tr><td>Fill with value: <br> <input type=text name=\"secondinfo\" size=20>");
            _responseContent
                    .AppendLine("<tr><td>Fill with value: <br> <textarea name=\"thirdinfo\" cols=40 rows=10></textarea>");
            _responseContent.AppendLine("<tr><td>Fill with file (only file name will be transmitted): <br> "
                    + "<input type=file name=\"myfile\">");
            _responseContent.AppendLine("</td></tr>");
            _responseContent.AppendLine("<tr><td><INPUT TYPE=\"submit\" NAME=\"Send\" VALUE=\"Send\"></INPUT></td>");
            _responseContent.AppendLine("<td><INPUT TYPE=\"reset\" NAME=\"Clear\" VALUE=\"Clear\" ></INPUT></td></tr>");
            _responseContent.AppendLine("</table></FORM>\r\n");
            _responseContent.AppendLine("<CENTER><HR WIDTH=\"75%\" NOSHADE color=\"blue\"></CENTER>");

            // POST with enctype="multipart/form-data"
            _responseContent.AppendLine("<CENTER>POST MULTIPART FORM<HR WIDTH=\"75%\" NOSHADE color=\"blue\"></CENTER>");
            _responseContent.AppendLine("<FORM ACTION=\"/formpostmultipart\" ENCTYPE=\"multipart/form-data\" METHOD=\"POST\">");
            _responseContent.AppendLine("<input type=hidden name=getform value=\"POST\">");
            _responseContent.AppendLine("<table border=\"0\">");
            _responseContent.AppendLine("<tr><td>Fill with value: <br> <input type=text name=\"info\" size=10></td></tr>");
            _responseContent.AppendLine("<tr><td>Fill with value: <br> <input type=text name=\"secondinfo\" size=20>");
            _responseContent
                    .AppendLine("<tr><td>Fill with value: <br> <textarea name=\"thirdinfo\" cols=40 rows=10></textarea>");
            _responseContent.AppendLine("<tr><td>Fill with file: <br> <input type=file name=\"myfile\">");
            _responseContent.AppendLine("</td></tr>");
            _responseContent.AppendLine("<tr><td><INPUT TYPE=\"submit\" NAME=\"Send\" VALUE=\"Send\"></INPUT></td>");
            _responseContent.AppendLine("<td><INPUT TYPE=\"reset\" NAME=\"Clear\" VALUE=\"Clear\" ></INPUT></td></tr>");
            _responseContent.AppendLine("</table></FORM>\r\n");
            _responseContent.AppendLine("<CENTER><HR WIDTH=\"75%\" NOSHADE color=\"blue\"></CENTER>");

            _responseContent.AppendLine("</body>");
            _responseContent.AppendLine("</html>");

            IByteBuffer buf = Unpooled.CopiedBuffer(_responseContent.ToString(), Encoding.UTF8);
            // Build the response object.
            var response = new DefaultFullHttpResponse(
                    HttpVersion.Http11, HttpResponseStatus.OK, buf);

            response.Headers.Set(HttpHeaderNames.ContentType, "text/html; charset=UTF-8");
            response.Headers.SetInt(HttpHeaderNames.ContentLength, buf.ReadableBytes);

            // Decide whether to close the connection or not.
            var keepAlive = HttpUtil.IsKeepAlive(_request);
            if (!keepAlive)
            {
                response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Close);
            }
            else if (_request.ProtocolVersion.Equals(HttpVersion.Http10))
            {
                response.Headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.KeepAlive);
            }

            // Write the response.
            var future = context.Channel.WriteAndFlushAsync(response);
            // Close the connection after the write operation is done if necessary.
            if (!keepAlive)
            {
                future.CloseOnComplete(context.Channel);
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            s_logger.LogError(exception, _responseContent.ToString());
            context.CloseAsync();
        }

        private static string GetPath(string uriString)
        {
            Debug.Assert(uriString != null, "uriString must not be null");
            Debug.Assert(uriString.Length > 0, "uriString must not be empty");

            int pathStartIndex = 0;

            // Perf. improvement: nearly all strings are relative Uris. So just look if the
            // string starts with '/'. If so, we have a relative Uri and the path starts at position 0.
            // (http.sys already trimmed leading whitespaces)
            if (uriString[0] != '/')
            {
                // We can't check against cookedUriScheme, since http.sys allows for request http://myserver/ to
                // use a request line 'GET https://myserver/' (note http vs. https). Therefore check if the
                // Uri starts with either http:// or https://.
                int authorityStartIndex = 0;
                if (uriString.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    authorityStartIndex = 7;
                }
                else if (uriString.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    authorityStartIndex = 8;
                }

                if (authorityStartIndex > 0)
                {
                    // we have an absolute Uri. Find out where the authority ends and the path begins.
                    // Note that Uris like "http://server?query=value/1/2" are invalid according to RFC2616
                    // and http.sys behavior: If the Uri contains a query, there must be at least one '/'
                    // between the authority and the '?' character: It's safe to just look for the first
                    // '/' after the authority to determine the beginning of the path.
                    pathStartIndex = uriString.IndexOf('/', authorityStartIndex);
                    if (pathStartIndex == -1)
                    {
                        // e.g. for request lines like: 'GET http://myserver' (no final '/')
                        pathStartIndex = uriString.Length;
                    }
                }
                else
                {
                    // RFC2616: Request-URI = "*" | absoluteURI | abs_path | authority
                    // 'authority' can only be used with CONNECT which is never received by HttpListener.
                    // I.e. if we don't have an absolute path (must start with '/') and we don't have
                    // an absolute Uri (must start with http:// or https://), then 'uriString' must be '*'.
                    Debug.Assert((uriString.Length == 1) && (uriString[0] == '*'), "Unknown request Uri string format",
                        "Request Uri string is not an absolute Uri, absolute path, or '*': {0}", uriString);

                    // Should we ever get here, be consistent with 2.0/3.5 behavior: just add an initial
                    // slash to the string and treat it as a path:
                    uriString = "/" + uriString;
                }
            }

            // Find end of path: The path is terminated by
            // - the first '?' character
            // - the first '#' character: This is never the case here, since http.sys won't accept 
            //   Uris containing fragments. Also, RFC2616 doesn't allow fragments in request Uris.
            // - end of Uri string
            int queryIndex = uriString.IndexOf('?');
            if (queryIndex == -1)
            {
                queryIndex = uriString.Length;
            }

            // will always return a != null string.
            return AddSlashToAsteriskOnlyPath(uriString.Substring(pathStartIndex, queryIndex - pathStartIndex));
        }

        private static string AddSlashToAsteriskOnlyPath(string path)
        {
            Debug.Assert(path != null, "'path' must not be null");

            // If a request like "OPTIONS * HTTP/1.1" is sent to the listener, then the request Uri
            // should be "http[s]://server[:port]/*" to be compatible with pre-4.0 behavior.
            if ((path.Length == 1) && (path[0] == '*'))
            {
                return "/*";
            }

            return path;
        }
    }
}
