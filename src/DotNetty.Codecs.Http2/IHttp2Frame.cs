// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// An HTTP/2 frame.
    /// </summary>
    public interface IHttp2Frame
    {
        /// <summary>
        /// Returns the name of the HTTP/2 frame e.g. DATA, GOAWAY, etc.
        /// </summary>
        string Name { get; }
    }
}
