// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal;
    using DotNetty.Transport.Channels;

    public class HttpServerCodec : CombinedChannelDuplexHandler<HttpRequestDecoder, HttpResponseEncoder>,
        HttpServerUpgradeHandler.ISourceCodec
    {
        /// <summary>
        /// A queue that is used for correlating a request and a response.
        /// </summary>
        readonly Deque<HttpMethod> queue = new Deque<HttpMethod>();

        /// <summary>
        /// Creates a new instance with the default decoder options
        /// ({@code maxInitialLineLength (4096}}, {@code maxHeaderSize (8192)}, and
        /// {@code maxChunkSize (8192)}).
        /// </summary>
        public HttpServerCodec() : this(4096, 8192, 8192)
        {
        }

        /// <summary>
        /// Creates a new instance with the specified decoder options.
        /// </summary>
        /// <param name="maxInitialLineLength"></param>
        /// <param name="maxHeaderSize"></param>
        /// <param name="maxChunkSize"></param>
        public HttpServerCodec(int maxInitialLineLength, int maxHeaderSize, int maxChunkSize)
        {
            this.Init(new HttpServerRequestDecoder(this, maxInitialLineLength, maxHeaderSize, maxChunkSize),
                new HttpServerResponseEncoder(this));
        }

        /// <summary>
        /// Creates a new instance with the specified decoder options.
        /// </summary>
        /// <param name="maxInitialLineLength"></param>
        /// <param name="maxHeaderSize"></param>
        /// <param name="maxChunkSize"></param>
        /// <param name="validateHeaders"></param>
        public HttpServerCodec(int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders)
        {
            this.Init(new HttpServerRequestDecoder(this, maxInitialLineLength, maxHeaderSize, maxChunkSize, validateHeaders),
                new HttpServerResponseEncoder(this));
        }

        /// <summary>
        /// Creates a new instance with the specified decoder options.
        /// </summary>
        /// <param name="maxInitialLineLength"></param>
        /// <param name="maxHeaderSize"></param>
        /// <param name="maxChunkSize"></param>
        /// <param name="validateHeaders"></param>
        /// <param name="initialBufferSize"></param>
        public HttpServerCodec(int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders, int initialBufferSize)
        {
            this.Init(new HttpServerRequestDecoder(this, maxInitialLineLength, maxHeaderSize, maxChunkSize, validateHeaders, initialBufferSize),
                new HttpServerResponseEncoder(this));
        }

        /// <summary>
        /// Upgrades to another protocol from HTTP. Removes the <see cref="HttpRequestDecoder"/> and
        /// <see cref="HttpResponseEncoder"/> from the pipeline.
        /// </summary>
        /// <param name="ctx"></param>
        public void UpgradeFrom(IChannelHandlerContext ctx) => ctx.Pipeline.Remove(this);

        sealed class HttpServerRequestDecoder : HttpRequestDecoder
        {
            readonly HttpServerCodec serverCodec;

            public HttpServerRequestDecoder(HttpServerCodec serverCodec, int maxInitialLineLength, int maxHeaderSize, int maxChunkSize)
                : base(maxInitialLineLength, maxHeaderSize, maxChunkSize)
            {
                this.serverCodec = serverCodec;
            }

            public HttpServerRequestDecoder(HttpServerCodec serverCodec, int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders)
                : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, validateHeaders)
            {
                this.serverCodec = serverCodec;
            }

            public HttpServerRequestDecoder(HttpServerCodec serverCodec,
                int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders, int initialBufferSize)
                : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, validateHeaders, initialBufferSize)
            {
                this.serverCodec = serverCodec;
            }

            protected override void Decode(IChannelHandlerContext context, IByteBuffer buffer, List<object> output)
            {
                int oldSize = output.Count;
                base.Decode(context, buffer, output);
                int size = output.Count;
                for (int i = oldSize; i < size; i++)
                {
                    if (output[i] is IHttpRequest request)
                    {
                        this.serverCodec.queue.AddToBack(request.Method);
                    }
                }
            }
        }

        sealed class HttpServerResponseEncoder : HttpResponseEncoder
        {
            readonly HttpServerCodec serverCodec;
            HttpMethod method;

            public HttpServerResponseEncoder(HttpServerCodec serverCodec)
            {
                this.serverCodec = serverCodec;
            }

            protected override void SanitizeHeadersBeforeEncode(IHttpResponse msg, bool isAlwaysEmpty)
            {
                if (!isAlwaysEmpty && ReferenceEquals(this.method, HttpMethod.Connect) &&
                    msg.Status.CodeClass == HttpStatusClass.Success)
                {
                    // Stripping Transfer-Encoding:
                    // See https://tools.ietf.org/html/rfc7230#section-3.3.1
                    _ = msg.Headers.Remove(HttpHeaderNames.TransferEncoding);
                    return;
                }

                base.SanitizeHeadersBeforeEncode(msg, isAlwaysEmpty);
            }

            protected override bool IsContentAlwaysEmpty(IHttpResponse msg)
            {
                _ = this.serverCodec.queue.TryRemoveFromFront(out this.method);
                return HttpMethod.Head.Equals(this.method) || base.IsContentAlwaysEmpty(msg);
            }
        }
    }
}
