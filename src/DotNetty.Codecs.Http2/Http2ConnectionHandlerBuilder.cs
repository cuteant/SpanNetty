// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Builder which builds <see cref="Http2ConnectionHandler"/> objects.
    /// </summary>
    public sealed class Http2ConnectionHandlerBuilder : AbstractHttp2ConnectionHandlerBuilder<Http2ConnectionHandler, Http2ConnectionHandlerBuilder>
    {
        protected override Http2ConnectionHandler Build(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings)
        {
            return new Http2ConnectionHandler(decoder, encoder, initialSettings);
        }
    }
}
