// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Builder which builds <see cref="HttpToHttp2ConnectionHandler"/> objects.
    /// </summary>
    public sealed class HttpToHttp2ConnectionHandlerBuilder : AbstractHttp2ConnectionHandlerBuilder<HttpToHttp2ConnectionHandler, HttpToHttp2ConnectionHandlerBuilder>
    {
        protected override HttpToHttp2ConnectionHandler Build(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings)
        {
            return new HttpToHttp2ConnectionHandler(decoder, encoder, initialSettings, IsValidateHeaders, DecoupleCloseAndGoAway);
        }
    }
}
