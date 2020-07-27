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
    /// An HTTP/2 exception for a specific <see cref="IHttp2FrameStream"/>.
    /// </summary>
    public sealed class Http2FrameStreamException : Exception
    {
        public Http2FrameStreamException(IHttp2FrameStream stream, Http2Error error, Exception cause)
            : base(cause.Message, cause)
        {
            if (stream is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.stream); }

            Stream = stream;
            Error = error;
        }

        public Http2Error Error { get; }

        public IHttp2FrameStream Stream { get; }
    }
}
