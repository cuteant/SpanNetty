// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Listener for life-cycle events for streams in this connection.
    /// </summary>
    public interface IHttp2ConnectionListener
    {
        /// <summary>
        /// Notifies the listener that the given stream was added to the connection. This stream may
        /// not yet be active (i.e. <c>OPEN</c> or <c>HALF CLOSED</c>).
        /// </summary>
        /// <param name="stream"></param>
        /// <exception cref="Http2RuntimeException">thrown it will be logged and <c>not propagated</c>.
        /// Throwing from this method is not supported and is considered a programming error.</exception>
        void OnStreamAdded(IHttp2Stream stream);

        /// <summary>
        /// Notifies the listener that the given stream was made active (i.e. <c>OPEN</c> or <c>HALF CLOSED</c>).
        /// </summary>
        /// <param name="stream"></param>
        /// <exception cref="Http2RuntimeException">thrown it will be logged and <c>not propagated</c>.
        /// Throwing from this method is not supported and is considered a programming error.</exception>
        void OnStreamActive(IHttp2Stream stream);

        /// <summary>
        /// Notifies the listener that the given stream has transitioned from <c>OPEN</c> to <c>HALF CLOSED</c>.
        /// This method will <strong>not</strong> be called until a state transition occurs from when
        /// <see cref="OnStreamActive(IHttp2Stream)"/> was called.
        /// The stream can be inspected to determine which side is <c>HALF CLOSED</c>.
        /// </summary>
        /// <param name="stream"></param>
        /// <exception cref="Http2RuntimeException">thrown it will be logged and <c>not propagated</c>.
        /// Throwing from this method is not supported and is considered a programming error.</exception>
        void OnStreamHalfClosed(IHttp2Stream stream);

        /// <summary>
        /// Notifies the listener that the given stream is now <c>CLOSED</c> in both directions and will no longer
        /// be accessible via <see cref="IHttp2Connection.ForEachActiveStream(IHttp2StreamVisitor)"/>.
        /// </summary>
        /// <param name="stream"></param>
        /// <exception cref="Http2RuntimeException">thrown it will be logged and <c>not propagated</c>.
        /// Throwing from this method is not supported and is considered a programming error.</exception>
        void OnStreamClosed(IHttp2Stream stream);

        /// <summary>
        /// Notifies the listener that the given stream has now been removed from the connection and
        /// will no longer be returned via <see cref="IHttp2Connection.Stream(int)"/>. The connection may
        /// maintain inactive streams for some time before removing them.
        /// </summary>
        /// <param name="stream"></param>
        /// <exception cref="Http2RuntimeException">thrown it will be logged and <c>not propagated</c>.
        /// Throwing from this method is not supported and is considered a programming error.</exception>
        void OnStreamRemoved(IHttp2Stream stream);

        /// <summary>
        /// Called when a <c>GOAWAY</c> frame was sent for the connection.
        /// </summary>
        /// <param name="lastStreamId">the last known stream of the remote endpoint.</param>
        /// <param name="errorCode">the error code, if abnormal closure.</param>
        /// <param name="debugData">application-defined debug data.</param>
        /// <exception cref="Http2RuntimeException">thrown it will be logged and <c>not propagated</c>.
        /// Throwing from this method is not supported and is considered a programming error.</exception>
        void OnGoAwaySent(int lastStreamId, Http2Error errorCode, IByteBuffer debugData);

        /// <summary>
        /// Called when a <c>GOAWAY</c> was received from the remote endpoint. This event handler duplicates
        /// <see cref="IHttp2FrameListener.OnGoAwayRead(IChannelHandlerContext, int, Http2Error, IByteBuffer)"/>
        /// but is added here in order to simplify application logic for handling <c>GOAWAY</c> in a uniform way. An
        /// application should generally not handle both events, but if it does this method is called second, after
        /// notifying the <see cref="IHttp2FrameListener"/>.
        /// </summary>
        /// <param name="lastStreamId">the last known stream of the remote endpoint.</param>
        /// <param name="errorCode">the error code, if abnormal closure.</param>
        /// <param name="debugData">application-defined debug data.</param>
        /// <exception cref="Http2RuntimeException">thrown it will be logged and <c>not propagated</c>.
        /// Throwing from this method is not supported and is considered a programming error.</exception>
        void OnGoAwayReceived(int lastStreamId, Http2Error errorCode, IByteBuffer debugData);
    }
}
