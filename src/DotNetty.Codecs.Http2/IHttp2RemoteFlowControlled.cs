// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Implementations of this interface are used to progressively write chunks of the underlying
    /// payload to the stream. A payload is considered to be fully written if <see cref="Write(Transport.Channels.IChannelHandlerContext, int)"/> has
    /// been called at least once and it's <see cref="Size()"/> is now zero.
    /// </summary>
    public interface IHttp2RemoteFlowControlled
    {
        /// <summary>
        /// The size of the payload in terms of bytes applied to the flow-control window.
        /// Some payloads like <c>HEADER</c> frames have no cost against flow control and would
        /// return 0 for this value even though they produce a non-zero number of bytes on
        /// the wire. Other frames like <c>DATA</c> frames have both their payload and padding count
        /// against flow-control.
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Called to indicate that an error occurred before this object could be completely written.
        /// <para>The <see cref="IHttp2RemoteFlowController"/> will make exactly one call to either
        /// this method or <see cref="WriteComplete()"/>.</para>
        /// </summary>
        /// <param name="ctx">The context to use if any communication needs to occur as a result of the error.
        /// This may be <c>null</c> if an exception occurs when the connection has not been established yet.</param>
        /// <param name="cause">cause of the error.</param>
        void Error(IChannelHandlerContext ctx, Exception cause);

        /// <summary>
        /// Called after this object has been successfully written.
        /// <para>The <see cref="IHttp2RemoteFlowController"/> will make exactly one call to either
        /// this method or <see cref="Error(IChannelHandlerContext, Exception)"/>.</para>
        /// </summary>
        void WriteComplete();

        /// <summary>
        /// Writes up to <paramref name="allowedBytes"/> of the encapsulated payload to the stream. Note that
        /// a value of 0 may be passed which will allow payloads with flow-control size == 0 to be
        /// written. The flow-controller may call this method multiple times with different values until
        /// the payload is fully written, i.e it's size after the write is 0.
        /// <para>When an exception is thrown the <see cref="IHttp2RemoteFlowController"/> will make a call to
        /// <see cref="Error(IChannelHandlerContext, Exception)"/>.</para>
        /// </summary>
        /// <param name="ctx">The context to use for writing.</param>
        /// <param name="allowedBytes">an upper bound on the number of bytes the payload can write at this time.</param>
        void Write(IChannelHandlerContext ctx, int allowedBytes);

        /// <summary>
        /// Merge the contents of the <paramref name="next"/> message into this message so they can be written out as one unit.
        /// This allows many small messages to be written as a single DATA frame.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="next"></param>
        /// <returns><c>true</c> if <paramref name="next"/> was successfully merged and does not need to be enqueued, <c>false</c> otherwise.</returns>
        bool Merge(IChannelHandlerContext ctx, IHttp2RemoteFlowControlled next);
    }
}
