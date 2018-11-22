// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// A visitor that allows to iterate over a collection of <see cref="IHttp2FrameStream"/>s.
    /// </summary>
    public interface IHttp2FrameStreamVisitor
    {
        /// <summary>
        /// This method is called once for each stream of the collection.
        /// If an <see cref="System.Exception"/> is thrown, the loop is stopped.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns><c>true</c> if the visitor wants to continue the loop and handle the stream.
        /// <c>false</c> if the visitor wants to stop handling the stream and abort the loop.</returns>
        bool Visit(IHttp2FrameStream stream);
    }
}
