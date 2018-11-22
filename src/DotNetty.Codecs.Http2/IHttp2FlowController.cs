// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.ComponentModel;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Base interface for all HTTP/2 flow controllers.
    /// </summary>
    public interface IHttp2FlowController
    {
        /// <summary>
        /// Set the <see cref="IChannelHandlerContext"/> for which to apply flow control on.
        /// <para>This <strong>must</strong> be called to properly initialize the <see cref="IHttp2FlowController"/>.
        /// Not calling this is considered a programming error.</para>
        /// </summary>
        /// <param name="ctx">The <see cref="IChannelHandlerContext"/> for which to apply flow control on.</param>
        /// <exception cref="Http2Exception">if any protocol-related error occurred.</exception>
        void SetChannelHandlerContext(IChannelHandlerContext ctx);

        /// <summary>
        /// Sets the connection-wide initial flow control window and updates all stream windows (but not the connection
        /// stream window) by the delta.
        /// 
        /// <para>Represents the value for
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_INITIAL_WINDOW_SIZE</a>. This method should
        /// only be called by Netty (not users) as a result of a receiving a <c>SETTINGS</c> frame.</para>
        /// </summary>
        /// <param name="newWindowSize">the new initial window size.</param>
        /// <exception cref="Http2Exception">if any protocol-related error occurred.</exception>
        [EditorBrowsable(EditorBrowsableState.Never)]
        void SetInitialWindowSize(int newWindowSize);

        /// <summary>
        /// Gets the connection-wide initial flow control window size that is used as the basis for new stream flow
        /// control windows.
        /// <para>Represents the value for
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_INITIAL_WINDOW_SIZE</a>. The initial value
        /// returned by this method must be <see cref="Http2CodecUtil.DefaultWindowSize"/>.</para>
        /// </summary>
        int InitialWindowSize { get; }

        /// <summary>
        /// Get the portion of the flow control window for the given stream that is currently available for sending/receiving
        /// frames which are subject to flow control. This quantity is measured in number of bytes.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        int GetWindowSize(IHttp2Stream stream);

        /// <summary>
        /// Increments the size of the stream's flow control window by the given delta.
        /// 
        /// <para>In the case of a <see cref="IHttp2RemoteFlowController"/> this is called upon receipt of a
        /// <c>WINDOW_UPDATE</c> frame from the remote endpoint to mirror the changes to the window size.</para>
        /// 
        /// <para>For a <see cref="IHttp2LocalFlowController"/> this can be called to request the expansion of the
        /// window size published by this endpoint. It is up to the implementation, however, as to when a
        /// <c>WINDOW_UPDATE</c> is actually sent.</para>
        /// </summary>
        /// <param name="stream">The subject stream. Use <see cref="IHttp2Connection.ConnectionStream"/> for
        /// requesting the size of the connection window.</param>
        /// <param name="delta">the change in size of the flow control window.</param>
        /// <exception cref="Http2Exception">thrown if a protocol-related error occurred.</exception>
        void IncrementWindowSize(IHttp2Stream stream, int delta);
    }
}
