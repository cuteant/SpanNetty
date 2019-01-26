// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    /// <summary>
    /// An object (used by remote flow control) that is responsible for distributing the bytes to be
    /// written across the streams in the connection.
    /// </summary>
    public interface IStreamByteDistributor
    {
        /// <summary>
        /// Called when the streamable bytes for a stream has changed. Until this
        /// method is called for the first time for a give stream, the stream is assumed to have no
        /// streamable bytes.
        /// </summary>
        /// <param name="state"></param>
        void UpdateStreamableBytes(IStreamByteDistributorStreamState state);

        /// <summary>
        /// Explicitly update the dependency tree. This method is called independently of stream state changes.
        /// </summary>
        /// <param name="childStreamId">The stream identifier associated with the child stream.</param>
        /// <param name="parentStreamId">The stream identifier associated with the parent stream. May be <c>0</c>,
        /// to make <paramref name="childStreamId"/> and immediate child of the connection.</param>
        /// <param name="weight">The weight which is used relative to other child streams for <paramref name="parentStreamId"/>. This value
        /// must be between 1 and 256 (inclusive).</param>
        /// <param name="exclusive">If <paramref name="childStreamId"/> should be the exclusive dependency of <paramref name="parentStreamId"/>.</param>
        void UpdateDependencyTree(int childStreamId, int parentStreamId, short weight, bool exclusive);

        /// <summary>
        /// Distributes up to <paramref name="maxBytes"/> to those streams containing streamable bytes and
        /// iterates across those streams to write the appropriate bytes. Criteria for
        /// traversing streams is undefined and it is up to the implementation to determine when to stop
        /// at a given stream.
        /// <para>The streamable bytes are not automatically updated by calling this method. It is up to the
        /// caller to indicate the number of bytes streamable after the write by calling
        /// <see cref="UpdateStreamableBytes(IStreamByteDistributorStreamState)"/>.</para>
        /// </summary>
        /// <param name="maxBytes">the maximum number of bytes to write.</param>
        /// <param name="writer"></param>
        /// <returns><c>true</c> if there are still streamable bytes that have not yet been written,
        /// otherwise <c>false</c>.</returns>
        /// <exception cref="Http2Exception">If an internal exception occurs and internal connection state would otherwise be
        /// corrupted.</exception>
        bool Distribute(int maxBytes, IStreamByteDistributorWriter writer);
        bool Distribute(int maxBytes, Action<IHttp2Stream, int> writer);
    }
}
