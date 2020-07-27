/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

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
