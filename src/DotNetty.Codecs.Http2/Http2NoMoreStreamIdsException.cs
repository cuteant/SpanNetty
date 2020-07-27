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
    using System;

    /// <summary>
    /// This exception is thrown when there are no more stream IDs available for the current connection
    /// </summary>
    public class Http2NoMoreStreamIdsException : Http2Exception
    {
        private const string ErrorMessage = "No more streams can be created on this connection";

        public Http2NoMoreStreamIdsException()
            : base(Http2Error.ProtocolError, ErrorMessage, ShutdownHint.GracefulShutdown)
        {
        }

        public Http2NoMoreStreamIdsException(Exception cause)
            : base(Http2Error.ProtocolError, ErrorMessage, cause, ShutdownHint.GracefulShutdown)
        {
        }
    }
}
