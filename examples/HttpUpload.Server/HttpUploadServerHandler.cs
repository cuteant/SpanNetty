namespace HttpUpload.Server
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http.Cookies;
    using DotNetty.Codecs.Http.Multipart;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;

    public class HttpUploadServerHandler : SimpleChannelInboundHandler2<IHttpObject>
    {
        static readonly ILogger s_logger = InternalLoggerFactory.DefaultFactory.CreateLogger<HttpUploadServerHandler>();
        static readonly IHttpDataFactory factory =
            new DefaultHttpDataFactory(DefaultHttpDataFactory.MinSize); // Disk if size exceed

        IHttpRequest request;

        bool readingChunks;

        IHttpData partialContent;

        readonly StringBuilder responseContent = new StringBuilder();

        HttpPostRequestDecoder decoder;

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
            if (this.decoder != null)
            {
                this.decoder.CleanFiles();
            }
            base.ChannelActive(context);
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, IHttpObject msg)
        {
            if (msg is IHttpRequest request)
            {
                s_logger.LogTrace("=========The Request Header========");
                s_logger.LogDebug(request.ToString());
                s_logger.LogTrace("===================================");
                this.request = request;
                var uriPath = GetPath(request.Uri);
                if (!uriPath.StartsWith("/form"))
                {
                    // Write Menu
                    WriteMenuAsync(ctx);
                    return;
                }
                this.responseContent.Clear();
                this.responseContent.Append("WELCOME TO THE WILD WILD WEB SERVER\r\n");
                this.responseContent.Append("===================================\r\n");

                this.responseContent.Append("VERSION: " + request.ProtocolVersion.Text + "\r\n");

                this.responseContent.Append("REQUEST_URI: " + request.Uri + "\r\n\r\n");
                this.responseContent.Append("\r\n\r\n");

                // new getMethod
                foreach (var entry in request.Headers)
                {
                    this.responseContent.Append("HEADER: " + entry.Key + '=' + entry.Value + "\r\n");
                }
                this.responseContent.Append("\r\n\r\n");

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
                    this.responseContent.Append("COOKIE: " + cookie + "\r\n");
                }
                this.responseContent.Append("\r\n\r\n");

                QueryStringDecoder decoderQuery = new QueryStringDecoder(request.Uri);
                var uriAttributes = decoderQuery.Parameters;
                foreach (var attr in uriAttributes)
                {
                    foreach (var attrVal in attr.Value)
                    {
                        this.responseContent.Append("URI: " + attr.Key + '=' + attrVal + "\r\n");
                    }
                }
                this.responseContent.Append("\r\n\r\n");

                // if GET Method: should not try to create a HttpPostRequestDecoder
                if (request.Method.Equals(HttpMethod.Get))
                {
                    // GET Method: should not try to create a HttpPostRequestDecoder
                    // So stop here
                    this.responseContent.Append("\r\n\r\nEND OF GET CONTENT\r\n");
                    // Not now: LastHttpContent will be sent writeResponse(ctx.channel());
                    return;
                }
                try
                {
                    this.decoder = new HttpPostRequestDecoder(factory, request);
                }
                catch (ErrorDataDecoderException e1)
                {
                    s_logger.LogError(e1.ToString());
                    this.responseContent.Append(e1.Message);
                    WriteResponseAsync(ctx.Channel).ContinueWith(t => ctx.Channel.CloseAsync(), TaskContinuationOptions.ExecuteSynchronously);
                    return;
                }

                readingChunks = HttpUtil.IsTransferEncodingChunked(request);
                this.responseContent.Append("Is Chunked: " + readingChunks + "\r\n");
                this.responseContent.Append("IsMultipart: " + this.decoder.IsMultipart + "\r\n");
                if (readingChunks)
                {
                    // Chunk version
                    this.responseContent.Append("Chunks: ");
                    readingChunks = true;
                }
            }

            // check if the decoder was constructed before
            // if not it handles the form get
            if (this.decoder != null)
            {
                if (msg is IHttpContent chunk) // New chunk is received
                {
                    try
                    {
                        this.decoder.Offer(chunk);
                    }
                    catch (ErrorDataDecoderException e1)
                    {
                        s_logger.LogError(e1.ToString());
                        this.responseContent.Append(e1.Message);
                        WriteResponseAsync(ctx.Channel).ContinueWith(t => ctx.Channel.CloseAsync(), TaskContinuationOptions.ExecuteSynchronously);
                        return;
                    }
                    this.responseContent.Append('o');
                    // example of reading chunk by chunk (minimize memory usage due to
                    // Factory)
                    ReadHttpDataChunkByChunk();
                    // example of reading only if at the end
                    if (chunk is ILastHttpContent)
                    {
                        WriteResponseAsync(ctx.Channel);
                        readingChunks = false;

                        Reset();
                    }
                }
            }
            else
            {
                WriteResponseAsync(ctx.Channel);
            }
        }

        private void Reset()
        {
            this.request = null;

            // destroy the decoder to release all resources
            this.decoder.Destroy();
            this.decoder = null;
        }

        /**
         * Example of reading request by chunk and getting values from chunk to chunk
         */
        private void ReadHttpDataChunkByChunk()
        {
            try
            {
                IInterfaceHttpData data = null;
                while (this.decoder.HasNext)
                {
                    data = this.decoder.Next();
                    if (data != null)
                    {
                        // check if current HttpData is a FileUpload and previously set as partial
                        if (this.partialContent == data)
                        {
                            s_logger.LogInformation(" 100% (FinalSize: " + this.partialContent.Length + ")");
                            this.partialContent = null;
                        }
                        try
                        {
                            // new value
                            WriteHttpData(data);
                        }
                        finally
                        {
                            data.Release();
                        }
                    }
                }
                // Check partial decoding for a FileUpload
                data = this.decoder.CurrentPartialHttpData;
                if (data != null)
                {
                    StringBuilder builder = new StringBuilder();
                    if (this.partialContent == null)
                    {
                        this.partialContent = (IHttpData)data;
                        if (this.partialContent is IFileUpload fileUpload)
                        {
                            builder.Append("Start FileUpload: ")
                                .Append(fileUpload.FileName).Append(" ");
                        }
                        else
                        {
                            builder.Append("Start Attribute: ")
                                .Append(this.partialContent.Name).Append(" ");
                        }
                        builder.Append("(DefinedSize: ").Append(this.partialContent.DefinedLength).Append(")");
                    }
                    if (this.partialContent.DefinedLength > 0)
                    {
                        builder.Append(" ").Append(this.partialContent.Length * 100 / this.partialContent.DefinedLength)
                            .Append("% ");
                        s_logger.LogInformation(builder.ToString());
                    }
                    else
                    {
                        builder.Append(" ").Append(this.partialContent.Length).Append(" ");
                        s_logger.LogInformation(builder.ToString());
                    }
                }
            }
            catch (EndOfDataDecoderException)
            {
                // end
                this.responseContent.Append("\r\n\r\nEND OF CONTENT CHUNK BY CHUNK\r\n\r\n");
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
                    this.responseContent.Append("\r\nBODY Attribute: " + attribute.DataType + ": "
                            + attribute.Name + " Error while reading value: " + e1.Message + "\r\n");
                    return;
                }
                if (value.Length > 100)
                {
                    this.responseContent.Append("\r\nBODY Attribute: " + attribute.DataType + ": "
                            + attribute.Name + " data too long\r\n");
                }
                else
                {
                    this.responseContent.Append("\r\nBODY Attribute: " + attribute.DataType + ": "
                            + attribute + "\r\n");
                }
            }
            else
            {
                this.responseContent.Append("\r\nBODY FileUpload: " + data.DataType + ": " + data
                        + "\r\n");
                if (data.DataType == HttpDataType.FileUpload)
                {
                    var fileUpload = (IFileUpload)data;
                    if (fileUpload.IsCompleted)
                    {
                        if (fileUpload.Length < 10000)
                        {
                            this.responseContent.Append("\tContent of file\r\n");
                            try
                            {
                                this.responseContent.Append(fileUpload.GetString(fileUpload.Charset));
                            }
                            catch (Exception e1)
                            {
                                // do nothing for the example
                                s_logger.LogError(e1.ToString());
                            }
                            this.responseContent.Append("\r\n");
                        }
                        else
                        {
                            this.responseContent.Append("\tFile too long to be printed out:" + fileUpload.Length + "\r\n");
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
                        this.responseContent.Append("\tFile to be continued but should not!\r\n");
                    }
                }
            }
        }

        private Task WriteResponseAsync(IChannel channel)
        {
            // Convert the response content to a ChannelBuffer.
            var buf = Unpooled.CopiedBuffer(this.responseContent.ToString(), Encoding.UTF8);
            this.responseContent.Clear();

            // Decide whether to close the connection or not.
            var close = this.request.Headers.Contains(HttpHeaderNames.Connection, HttpHeaderValues.Close, true)
                    || this.request.ProtocolVersion.Equals(HttpVersion.Http10)
                    && !this.request.Headers.Contains(HttpHeaderNames.Connection, HttpHeaderValues.KeepAlive, true);

            // Build the response object.
            var response = new DefaultFullHttpResponse(
                    HttpVersion.Http11, HttpResponseStatus.OK, buf);
            response.Headers.Set(HttpHeaderNames.ContentType, "text/plain; charset=UTF-8");

            if (!close)
            {
                // There's no need to add 'Content-Length' header
                // if this is the last response.
                response.Headers.SetInt(HttpHeaderNames.ContentLength, buf.ReadableBytes);
            }

            ISet<ICookie> cookies;
            var value = this.request.Headers.GetAsString(HttpHeaderNames.Cookie);
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
            if (close)
            {
                future.ContinueWith(t => channel.CloseAsync(), TaskContinuationOptions.ExecuteSynchronously);
            }
            return future;
        }

        private Task WriteMenuAsync(IChannelHandlerContext ctx)
        {
            // print several HTML forms
            // Convert the response content to a ChannelBuffer.
            this.responseContent.Clear();

            // create Pseudo Menu
            this.responseContent.AppendLine("<html>");
            this.responseContent.AppendLine("<head>");
            this.responseContent.AppendLine("<title>Netty Test Form</title>\r\n");
            this.responseContent.AppendLine("</head>\r\n");
            this.responseContent.AppendLine("<body bgcolor=white><style>td{font-size: 12pt;}</style>");

            this.responseContent.AppendLine("<table border=\"0\">");
            this.responseContent.AppendLine("<tr>");
            this.responseContent.AppendLine("<td>");
            this.responseContent.AppendLine("<h1>Netty Test Form</h1>");
            this.responseContent.AppendLine("Choose one FORM");
            this.responseContent.AppendLine("</td>");
            this.responseContent.AppendLine("</tr>");
            this.responseContent.AppendLine("</table>\r\n");

            // GET
            this.responseContent.AppendLine("<CENTER>GET FORM<HR WIDTH=\"75%\" NOSHADE color=\"blue\"></CENTER>");
            this.responseContent.AppendLine("<FORM ACTION=\"/formget\" METHOD=\"GET\">");
            this.responseContent.AppendLine("<input type=hidden name=getform value=\"GET\">");
            this.responseContent.AppendLine("<table border=\"0\">");
            this.responseContent.AppendLine("<tr><td>Fill with value: <br> <input type=text name=\"info\" size=10></td></tr>");
            this.responseContent.AppendLine("<tr><td>Fill with value: <br> <input type=text name=\"secondinfo\" size=20>");
            this.responseContent
                    .AppendLine("<tr><td>Fill with value: <br> <textarea name=\"thirdinfo\" cols=40 rows=10></textarea>");
            this.responseContent.AppendLine("</td></tr>");
            this.responseContent.AppendLine("<tr><td><INPUT TYPE=\"submit\" NAME=\"Send\" VALUE=\"Send\"></INPUT></td>");
            this.responseContent.AppendLine("<td><INPUT TYPE=\"reset\" NAME=\"Clear\" VALUE=\"Clear\" ></INPUT></td></tr>");
            this.responseContent.AppendLine("</table></FORM>\r\n");
            this.responseContent.AppendLine("<CENTER><HR WIDTH=\"75%\" NOSHADE color=\"blue\"></CENTER>");

            // POST
            this.responseContent.AppendLine("<CENTER>POST FORM<HR WIDTH=\"75%\" NOSHADE color=\"blue\"></CENTER>");
            this.responseContent.AppendLine("<FORM ACTION=\"/formpost\" METHOD=\"POST\">");
            this.responseContent.AppendLine("<input type=hidden name=getform value=\"POST\">");
            this.responseContent.AppendLine("<table border=\"0\">");
            this.responseContent.AppendLine("<tr><td>Fill with value: <br> <input type=text name=\"info\" size=10></td></tr>");
            this.responseContent.AppendLine("<tr><td>Fill with value: <br> <input type=text name=\"secondinfo\" size=20>");
            this.responseContent
                    .AppendLine("<tr><td>Fill with value: <br> <textarea name=\"thirdinfo\" cols=40 rows=10></textarea>");
            this.responseContent.AppendLine("<tr><td>Fill with file (only file name will be transmitted): <br> "
                    + "<input type=file name=\"myfile\">");
            this.responseContent.AppendLine("</td></tr>");
            this.responseContent.AppendLine("<tr><td><INPUT TYPE=\"submit\" NAME=\"Send\" VALUE=\"Send\"></INPUT></td>");
            this.responseContent.AppendLine("<td><INPUT TYPE=\"reset\" NAME=\"Clear\" VALUE=\"Clear\" ></INPUT></td></tr>");
            this.responseContent.AppendLine("</table></FORM>\r\n");
            this.responseContent.AppendLine("<CENTER><HR WIDTH=\"75%\" NOSHADE color=\"blue\"></CENTER>");

            // POST with enctype="multipart/form-data"
            this.responseContent.AppendLine("<CENTER>POST MULTIPART FORM<HR WIDTH=\"75%\" NOSHADE color=\"blue\"></CENTER>");
            this.responseContent.AppendLine("<FORM ACTION=\"/formpostmultipart\" ENCTYPE=\"multipart/form-data\" METHOD=\"POST\">");
            this.responseContent.AppendLine("<input type=hidden name=getform value=\"POST\">");
            this.responseContent.AppendLine("<table border=\"0\">");
            this.responseContent.AppendLine("<tr><td>Fill with value: <br> <input type=text name=\"info\" size=10></td></tr>");
            this.responseContent.AppendLine("<tr><td>Fill with value: <br> <input type=text name=\"secondinfo\" size=20>");
            this.responseContent
                    .AppendLine("<tr><td>Fill with value: <br> <textarea name=\"thirdinfo\" cols=40 rows=10></textarea>");
            this.responseContent.AppendLine("<tr><td>Fill with file: <br> <input type=file name=\"myfile\">");
            this.responseContent.AppendLine("</td></tr>");
            this.responseContent.AppendLine("<tr><td><INPUT TYPE=\"submit\" NAME=\"Send\" VALUE=\"Send\"></INPUT></td>");
            this.responseContent.AppendLine("<td><INPUT TYPE=\"reset\" NAME=\"Clear\" VALUE=\"Clear\" ></INPUT></td></tr>");
            this.responseContent.AppendLine("</table></FORM>\r\n");
            this.responseContent.AppendLine("<CENTER><HR WIDTH=\"75%\" NOSHADE color=\"blue\"></CENTER>");

            this.responseContent.AppendLine("</body>");
            this.responseContent.AppendLine("</html>");

            IByteBuffer buf = Unpooled.CopiedBuffer(this.responseContent.ToString(), Encoding.UTF8);
            // Build the response object.
            var response = new DefaultFullHttpResponse(
                    HttpVersion.Http11, HttpResponseStatus.OK, buf);

            response.Headers.Set(HttpHeaderNames.ContentType, "text/html; charset=UTF-8");
            response.Headers.SetInt(HttpHeaderNames.ContentLength, buf.ReadableBytes);

            // Write the response.
            return ctx.Channel.WriteAndFlushAsync(response);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            s_logger.LogError(exception, this.responseContent.ToString());
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
