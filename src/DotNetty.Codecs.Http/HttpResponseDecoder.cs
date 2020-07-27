// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using DotNetty.Common.Utilities;

    public class HttpResponseDecoder : HttpObjectDecoder
    {
        static readonly HttpResponseStatus UnknownStatus = new HttpResponseStatus(999, new AsciiString("Unknown"));

        public HttpResponseDecoder()
        {
        }

        public HttpResponseDecoder(int maxInitialLineLength, int maxHeaderSize, int maxChunkSize)
            : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, DefaultChunkedSupported)
        {
        }

        public HttpResponseDecoder(int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders)
            : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, DefaultChunkedSupported, validateHeaders)
        {
        }

        public HttpResponseDecoder(int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders, int initialBufferSize)
            : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, DefaultChunkedSupported, validateHeaders, initialBufferSize)
        {
        }

        public HttpResponseDecoder(
                int maxInitialLineLength, int maxHeaderSize, int maxChunkSize, bool validateHeaders,
                int initialBufferSize, bool allowDuplicateContentLengths)
            : base(maxInitialLineLength, maxHeaderSize, maxChunkSize, DefaultChunkedSupported, validateHeaders,
                  initialBufferSize, allowDuplicateContentLengths)
        {
        }

        protected sealed override IHttpMessage CreateMessage(AsciiString[] initialLine) =>
             new DefaultHttpResponse(
                HttpVersion.ValueOf(initialLine[0]),
                HttpResponseStatus.ValueOf(initialLine[1].ParseInt(), initialLine[2]), this.ValidateHeaders);

        protected override IHttpMessage CreateInvalidMessage() => new DefaultFullHttpResponse(HttpVersion.Http10, UnknownStatus, this.ValidateHeaders);

        protected override bool IsDecodingRequest() => false;
    }
}
