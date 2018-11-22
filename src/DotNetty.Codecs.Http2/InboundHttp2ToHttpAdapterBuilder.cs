// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Builds an <see cref="InboundHttp2ToHttpAdapter"/>.
    /// </summary>
    public sealed class InboundHttp2ToHttpAdapterBuilder : AbstractInboundHttp2ToHttpAdapterBuilder<InboundHttp2ToHttpAdapter, InboundHttp2ToHttpAdapterBuilder>
    {
        /// <summary>
        /// Creates a new <see cref="InboundHttp2ToHttpAdapter"/> builder for the specified <see cref="IHttp2Connection"/>.
        /// </summary>
        /// <param name="connection">the object which will provide connection notification events
        /// for the current connection.</param>
        public InboundHttp2ToHttpAdapterBuilder(IHttp2Connection connection) : base(connection) { }

        protected override InboundHttp2ToHttpAdapter Build(IHttp2Connection connection, int maxContentLength, bool validateHttpHeaders, bool propagateSettings)
        {
            return new InboundHttp2ToHttpAdapter(connection, maxContentLength, validateHttpHeaders, propagateSettings);
        }
    }
}
