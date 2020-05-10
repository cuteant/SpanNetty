// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

    public readonly struct FuncHttp2StreamVisitor : IHttp2StreamVisitor
    {
        private readonly Func<IHttp2Stream, bool> visitFunc;

        public FuncHttp2StreamVisitor(Func<IHttp2Stream, bool> visitFunc)
        {
            if (visitFunc is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.visitFunc); }
            this.visitFunc = visitFunc;
        }

        public bool Visit(IHttp2Stream stream) => this.visitFunc(stream);
    }
}
