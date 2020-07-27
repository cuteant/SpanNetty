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
    /// A visitor that allows iteration over a collection of streams.
    /// </summary>
    public interface IHttp2StreamVisitor
    {
        /// <summary>
        /// allows iteration over a collection of streams.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns><c>true</c> if the visitor wants to continue the loop and handle the entry.
        /// <c>false</c> if the visitor wants to stop handling headers and abort the loop.</returns>
        bool Visit(IHttp2Stream stream);
    }

    public sealed class FuncHttp2StreamVisitor : IHttp2StreamVisitor
    {
        private readonly Func<IHttp2Stream, bool> _visitFunc;

        public FuncHttp2StreamVisitor(Func<IHttp2Stream, bool> visitFunc)
        {
            if (visitFunc is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.visitFunc); }
            _visitFunc = visitFunc;
        }

        public bool Visit(IHttp2Stream stream) => _visitFunc(stream);
    }
}
