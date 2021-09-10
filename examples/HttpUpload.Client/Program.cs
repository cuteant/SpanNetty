// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace HttpUpload.Client
{
    using DotNetty.Codecs;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http.Cookies;
    using DotNetty.Codecs.Http.Multipart;
    using DotNetty.Codecs.Http.Utilities;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Streams;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv;
    using Examples.Common;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    partial class Program
    {
        static async Task Main(string[] args)
        {
            var builder = new UriBuilder
            {
                Scheme = ClientSettings.IsSsl ? "https" : "http",
                Host = ClientSettings.Host.ToString(),
                Port = ClientSettings.Port,
            };

            var baseUri = builder.Uri.ToString();
            ExampleHelper.SetConsoleLogger();

            string postSimple, postFile, get;
            if (baseUri.EndsWith("/"))
            {
                postSimple = baseUri + "formpost";
                postFile = baseUri + "formpostmultipart";
                get = baseUri + "formget";
            }
            else
            {
                postSimple = baseUri + "/formpost";
                postFile = baseUri + "/formpostmultipart";
                get = baseUri + "/formget";
            }
            var uriSimple = new Uri(postSimple);
            var uriFile = new Uri(postFile);

            bool useLibuv = ClientSettings.UseLibuv;
            Console.WriteLine($"Transport type : {(useLibuv ? "Libuv" : "Socket")}");

            IEventLoopGroup group;
            if (useLibuv)
            {
                group = new EventLoopGroup();
            }
            else
            {
                group = new MultithreadEventLoopGroup();
            }

            X509Certificate2 cert = null;
            string targetHost = null;
            if (ClientSettings.IsSsl)
            {
                cert = new X509Certificate2(Path.Combine(ExampleHelper.ProcessDirectory, "dotnetty.com.pfx"), "password");
                targetHost = cert.GetNameInfo(X509NameType.DnsName, false);
            }

            try
            {
                var bootstrap = new Bootstrap();
                bootstrap
                    .Group(group)
                    .Option(ChannelOption.TcpNodelay, true);

                if (useLibuv)
                {
                    bootstrap.Channel<TcpChannel>();
                }
                else
                {
                    bootstrap.Channel<TcpSocketChannel>();
                }

                bootstrap.Handler(new ActionChannelInitializer<IChannel>(channel =>
                {
                    IChannelPipeline pipeline = channel.Pipeline;
                    if (cert != null)
                    {
                        pipeline.AddLast("tls", new TlsHandler(stream => new SslStream(stream, true, (sender, certificate, chain, errors) => true), new ClientTlsSettings(targetHost)));
                    }

                    //pipeline.AddLast(new LoggingHandler("CONN"));

                    pipeline.AddLast("codec", new HttpClientCodec());

                    // Remove the following line if you don't want automatic content decompression.
                    pipeline.AddLast("inflater", new HttpContentDecompressor());

                    // to be used since huge file transfer
                    pipeline.AddLast("chunkedWriter", new ChunkedWriteHandler<IHttpContent>());

                    pipeline.AddLast("handler", new HttpUploadClientHandler());
                }));


                // setup the factory: here using a mixed memory/disk based on size threshold
                var factory = new DefaultHttpDataFactory(DefaultHttpDataFactory.MinSize); // Disk if MINSIZE exceed

                DiskFileUpload.DeleteOnExitTemporaryFile = true; // should delete file on exit (in normal exit)
                DiskFileUpload.FileBaseDirectory = null; // system temp directory
                DiskAttribute.DeleteOnExitTemporaryFile = true; // should delete file on exit (in normal exit)
                DiskAttribute.DiskBaseDirectory = null; // system temp directory

                // Simple Get form: no factory used (not usable)
                var headers = await FormgetAsync(bootstrap, get, uriSimple);
                if (headers == null)
                {
                    factory.CleanAllHttpData();
                    return;
                }

                using (var file = new FileStream("upload.txt", FileMode.Open, FileAccess.Read))
                {
                    // Simple Post form: factory used for big attributes
                    var bodylist = await FormpostAsync(bootstrap, uriSimple, file, factory, headers);
                    if (bodylist == null)
                    {
                        factory.CleanAllHttpData();
                        return;
                    }

                    // Multipart Post form: factory used
                    await FormpostmultipartAsync(bootstrap, uriFile, factory, headers, bodylist);
                }

                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
            finally
            {
                await group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));
            }
        }

        /**
         * Standard usage of HTTP API in Netty without file Upload (get is not able to achieve File upload
         * due to limitation on request size).
         *
         * @return the list of headers that will be used in every example after
         **/
        private static async Task<IList<HeaderEntry<AsciiString, ICharSequence>>> FormgetAsync(
            Bootstrap bootstrap, string get, Uri uriSimple)
        {
            // XXX /formget
            // No use of HttpPostRequestEncoder since not a POST
            IChannel channel = await bootstrap.ConnectAsync(new IPEndPoint(ClientSettings.Host, ClientSettings.Port));

            // Prepare the HTTP request.

            //QueryStringEncoder encoder = new QueryStringEncoder(get);
            //// add Form attribute
            //encoder.AddParam("getform", "GET");
            //encoder.AddParam("info", "first value");
            //encoder.AddParam("secondinfo", "secondvalue ���&");
            //// not the big one since it is not compatible with GET size
            //// encoder.AddParam("thirdinfo", textArea);
            //encoder.AddParam("thirdinfo", "third value\r\ntest second line\r\n\r\nnew line\r\n");
            //encoder.AddParam("Send", "Send");
            //var uriGet = new Uri(encoder.ToString());
            var queryParams = new Dictionary<string, string>()
            {
                { "getform", "GET" },
                { "info", "first value" },
                { "secondinfo", "secondvalue ���&" },
                { "thirdinfo", "third value\r\ntest second line\r\n\r\nnew line\r\n" },
                { "Send", "Send" },
            };
            var request = new DefaultHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Get, QueryHelpers.AddQueryString(get, queryParams));
            var headers = request.Headers;
            headers.Set(HttpHeaderNames.Host, ClientSettings.Host);
            headers.Set(HttpHeaderNames.Connection, HttpHeaderValues.Close);
            headers.Set(HttpHeaderNames.AcceptEncoding, HttpHeaderValues.Gzip + "," + HttpHeaderValues.Deflate);

            headers.Set(HttpHeaderNames.AcceptCharset, "ISO-8859-1,utf-8;q=0.7,*;q=0.7");
            headers.Set(HttpHeaderNames.AcceptLanguage, "fr");
            headers.Set(HttpHeaderNames.Referer, uriSimple.ToString());
            headers.Set(HttpHeaderNames.UserAgent, "Netty Simple Http Client side");
            headers.Set(HttpHeaderNames.Accept, "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            //connection will not close but needed
            // headers.Set("Connection","keep-alive");
            // headers.Set("Keep-Alive","300");

            headers.Set(
                    HttpHeaderNames.Cookie, ClientCookieEncoder.StrictEncoder.Encode(
                            new DefaultCookie("my-cookie", "foo"),
                            new DefaultCookie("another-cookie", "bar"))
            );

            // send request
            await channel.WriteAndFlushAsync(request);

            // Wait for the server to close the connection.
            await channel.CloseCompletion;

            // convert headers to list
            return headers.Entries();
        }

        /**
         * Standard post without multipart but already support on Factory (memory management)
         *
         * @return the list of HttpData object (attribute and file) to be reused on next post
         */
        private static async Task<List<IInterfaceHttpData>> FormpostAsync(Bootstrap bootstrap,
            Uri uriSimple, FileStream file, IHttpDataFactory factory,
            IList<HeaderEntry<AsciiString, ICharSequence>> headers)
        {
            // XXX /formpost
            // Start the connection attempt.
            IChannel channel = await bootstrap.ConnectAsync(new IPEndPoint(ClientSettings.Host, ClientSettings.Port));

            // Prepare the HTTP request.
            IHttpRequest request = new DefaultHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Post, uriSimple.ToString());

            // Use the PostBody encoder
            HttpPostRequestEncoder bodyRequestEncoder =
                    new HttpPostRequestEncoder(factory, request, false);  // false => not multipart

            // it is legal to add directly header or cookie into the request until finalize
            foreach (var entry in headers)
            {
                request.Headers.Set(entry.Key, entry.Value);
            }

            // add Form attribute
            bodyRequestEncoder.AddBodyAttribute("getform", "POST");
            bodyRequestEncoder.AddBodyAttribute("info", "first value");
            bodyRequestEncoder.AddBodyAttribute("secondinfo", "secondvalue ���&");
            bodyRequestEncoder.AddBodyAttribute("thirdinfo", TextArea);
            bodyRequestEncoder.AddBodyAttribute("fourthinfo", TextAreaLong);
            bodyRequestEncoder.AddBodyFileUpload("myfile", file, "application/x-zip-compressed", false);

            // finalize request
            request = bodyRequestEncoder.FinalizeRequest();

            // Create the bodylist to be reused on the last version with Multipart support
            var bodylist = bodyRequestEncoder.GetBodyListAttributes();

            var list = new List<object>();
            // send request
            list.Add(request);

            // test if request was chunked and if so, finish the write
            if (bodyRequestEncoder.IsChunked)
            { // could do either request.isChunked()
              // either do it through ChunkedWriteHandler
                list.Add(bodyRequestEncoder);
            }
            await channel.WriteAndFlushManyAsync(list);

            // Do not clear here since we will reuse the InterfaceHttpData on the next request
            // for the example (limit action on client side). Take this as a broadcast of the same
            // request on both Post actions.
            //
            // On standard program, it is clearly recommended to clean all files after each request
            // bodyRequestEncoder.cleanFiles();

            // Wait for the server to close the connection.
            await channel.CloseCompletion;
            return bodylist;
        }

        /**
         * Multipart example
         */
        private static async Task FormpostmultipartAsync(
            Bootstrap bootstrap, Uri uriFile, IHttpDataFactory factory,
            IList<HeaderEntry<AsciiString, ICharSequence>> headers, List<IInterfaceHttpData> bodylist)
        {
            // XXX /formpostmultipart
            // Start the connection attempt.
            IChannel channel = await bootstrap.ConnectAsync(new IPEndPoint(ClientSettings.Host, ClientSettings.Port));

            // Prepare the HTTP request.
            var request = new DefaultHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Post, uriFile.ToString());

            // Use the PostBody encoder
            HttpPostRequestEncoder bodyRequestEncoder =
                    new HttpPostRequestEncoder(factory, request, true); // true => multipart

            // it is legal to add directly header or cookie into the request until finalize
            foreach (var entry in headers)
            {
                request.Headers.Set(entry.Key, entry.Value);
            }

            // add Form attribute from previous request in formpost()
            bodyRequestEncoder.SetBodyHttpDatas(bodylist);

            // finalize request
            bodyRequestEncoder.FinalizeRequest();

            var list = new List<object>();
            // send request
            list.Add(request);

            // test if request was chunked and if so, finish the write
            if (bodyRequestEncoder.IsChunked)
            {
                list.Add(bodyRequestEncoder);
            }
            await channel.WriteAndFlushManyAsync(list);

            // Now no more use of file representation (and list of HttpData)
            bodyRequestEncoder.CleanFiles();

            // Wait for the server to close the connection.
            await channel.CloseCompletion;
        }
    }
}
