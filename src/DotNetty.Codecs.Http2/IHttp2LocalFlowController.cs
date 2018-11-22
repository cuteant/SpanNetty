// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Buffers;

    /// <summary>
    /// A <see cref="IHttp2FlowController"/> for controlling the inbound flow of <c>DATA</c> frames from the remote endpoint.
    /// </summary>
    public interface IHttp2LocalFlowController : IHttp2FlowController
    {
        /// <summary>
        /// Sets the writer to be use for sending <c>WINDOW_UPDATE</c> frames. This must be called before any flow
        /// controlled data is received.
        /// </summary>
        /// <param name="frameWriter">the HTTP/2 frame writer.</param>
        /// <returns></returns>
        IHttp2LocalFlowController FrameWriter(IHttp2FrameWriter frameWriter);

        /// <summary>
        /// Receives an inbound <c>DATA</c> frame from the remote endpoint and applies flow control policies to it for both
        /// the <paramref name="stream"/> as well as the connection. If any flow control policies have been violated, an exception is
        /// raised immediately, otherwise the frame is considered to have "passed" flow control.
        /// <para>If <paramref name="stream"/> is <c>null</c> or closed, flow control should only be applied to the connection window and the
        /// bytes are immediately consumed.</para>
        /// </summary>
        /// <param name="stream">the subject stream for the received frame. The connection stream object must not be used. If
        /// <paramref name="stream"/> is <c>null</c> or closed, flow control should only be applied to the connection window
        /// and the bytes are immediately consumed.</param>
        /// <param name="data">payload buffer for the frame.</param>
        /// <param name="padding">additional bytes that should be added to obscure the true content size. Must be between 0 and
        /// 256 (inclusive).</param>
        /// <param name="endOfStream">Indicates whether this is the last frame to be sent from the remote endpoint for this stream.</param>
        /// <exception cref="Http2Exception">if any flow control errors are encountered.</exception>
        void ReceiveFlowControlledFrame(IHttp2Stream stream, IByteBuffer data, int padding, bool endOfStream);

        /// <summary>
        /// Indicates that the application has consumed a number of bytes for the given stream and is therefore ready to
        /// receive more data from the remote endpoint. The application must consume any bytes that it receives or the flow
        /// control window will collapse. Consuming bytes enables the flow controller to send <c>WINDOW_UPDATE</c> to
        /// restore a portion of the flow control window for the stream.
        /// </summary>
        /// <param name="stream">the stream for which window space should be freed. The connection stream object must not be used.
        /// If <paramref name="stream"/> is <c>null</c> or closed (i.e. <see cref="IHttp2Stream.State"/> method returns 
        /// <see cref="Http2StreamState.Closed"/>), calling this method has no effect.</param>
        /// <param name="numBytes">the number of bytes to be returned to the flow control window.</param>
        /// <exception cref="Http2Exception">if the number of bytes returned exceeds the <see cref="UnconsumedBytes(IHttp2Stream)"/> for the stream.</exception>
        /// <returns><c>true</c> if a <see cref="Http2FrameTypes.WindowUpdate"/> was sent, <c>false</c> otherwise.</returns>
        bool ConsumeBytes(IHttp2Stream stream, int numBytes);

        /// <summary>
        /// The number of bytes for the given stream that have been received but not yet consumed by the application.
        /// </summary>
        /// <param name="stream">the stream for which window space should be freed.</param>
        /// <returns>the number of unconsumed bytes for the stream.</returns>
        int UnconsumedBytes(IHttp2Stream stream);

        /// <summary>
        /// Get the initial flow control window size for the given stream. This quantity is measured in number of bytes. Note
        /// the unavailable window portion can be calculated by <see cref="IHttp2FlowController.InitialWindowSize"/> - 
        /// <see cref="IHttp2FlowController.GetWindowSize(IHttp2Stream)"/>.
        /// </summary>
        /// <param name="stream"></param>
        int GetInitialWindowSize(IHttp2Stream stream);
    }
}
