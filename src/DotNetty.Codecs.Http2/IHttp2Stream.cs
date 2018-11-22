// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// A single stream within an HTTP2 connection. Streams are compared to each other by priority.
    /// </summary>
    public interface IHttp2Stream
    {
        /// <summary>
        /// Gets the unique identifier for this stream within the connection.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets the state of this stream.
        /// </summary>
        Http2StreamState State { get; }

        /// <summary>
        /// Opens this stream, making it available via <see cref="IHttp2Connection.ForEachActiveStream(IHttp2StreamVisitor)"/> and
        /// transition state to:
        /// <para><see cref="Http2StreamState.Open"/> if <see cref="State"/> is <see cref="Http2StreamState.Idle"/> and <paramref name="halfClosed"/> is <c>false</c>.</para>
        /// <para><see cref="Http2StreamState.HalfClosedLocal"/> if <see cref="State"/> is <see cref="Http2StreamState.Idle"/> and <paramref name="halfClosed"/>
        /// is <c>true</c> and the stream is local. In this state, <see cref="IsHeadersSent"/> is <c>true</c></para>
        /// <para><see cref="Http2StreamState.HalfClosedRemote"/> if <see cref="State"/> is <see cref="Http2StreamState.Idle"/> and <paramref name="halfClosed"/>
        /// is <c>true</c> and the stream is remote. In this state, <see cref="IsHeadersReceived"/> is <c>true</c></para>
        /// <para><see cref="Http2StreamState.ReservedLocal"/> if <see cref="State"/> is <see cref="Http2StreamState.HalfClosedRemote"/>.</para>
        /// <para><see cref="Http2StreamState.ReservedRemote"/> if <see cref="State"/> is <see cref="Http2StreamState.HalfClosedLocal"/>.</para>
        /// </summary>
        /// <param name="halfClosed"></param>
        /// <returns></returns>
        IHttp2Stream Open(bool halfClosed);

        /// <summary>
        /// Closes the stream.
        /// </summary>
        /// <returns></returns>
        IHttp2Stream Close();

        /// <summary>
        /// Closes the local side of this stream. If this makes the stream closed, the child is closed as well.
        /// </summary>
        /// <returns></returns>
        IHttp2Stream CloseLocalSide();

        /// <summary>
        /// Closes the remote side of this stream. If this makes the stream closed, the child is closed as well.
        /// </summary>
        /// <returns></returns>
        IHttp2Stream CloseRemoteSide();

        /// <summary>
        /// Indicates whether a <c>RST_STREAM</c> frame has been sent from the local endpoint for this stream.
        /// </summary>
        bool IsResetSent { get; }

        /// <summary>
        /// Sets the flag indicating that a <c>RST_STREAM</c> frame has been sent from the local endpoint
        /// for this stream. This does not affect the stream state.
        /// </summary>
        IHttp2Stream ResetSent();

        /// <summary>
        /// Associates the application-defined data with this stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>The value that was previously associated with <paramref name="key"/>, or <c>null</c> if there was none.</returns>
        T SetProperty<T>(IHttp2ConnectionPropertyKey key, object value);
        object SetProperty(IHttp2ConnectionPropertyKey key, object value);

        /// <summary>
        /// Returns application-defined data if any was associated with this stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        T GetProperty<T>(IHttp2ConnectionPropertyKey key);

        /// <summary>
        /// Returns and removes application-defined data if any was associated with this stream.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        T RemoveProperty<T>(IHttp2ConnectionPropertyKey key);
        /// <summary>
        /// Returns and removes application-defined data if any was associated with this stream.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        object RemoveProperty(IHttp2ConnectionPropertyKey key);

        /// <summary>
        /// Indicates that headers have been sent to the remote endpoint on this stream. The first call to this method would
        /// be for the initial headers (see <see cref="IsHeadersSent"/> and the second call would indicate the trailers
        /// (see <see cref="IsTrailersSent"/>.
        /// </summary>
        /// <param name="isInformational"><c>true</c> if the headers contain an informational status code (for responses only).</param>
        /// <returns></returns>
        IHttp2Stream HeadersSent(bool isInformational);

        /// <summary>
        /// Indicates whether or not headers were sent to the remote endpoint.
        /// </summary>
        bool IsHeadersSent { get; }

        /// <summary>
        /// Indicates whether or not trailers were sent to the remote endpoint.
        /// </summary>
        bool IsTrailersSent { get; }

        /// <summary>
        /// Indicates that headers have been received. The first call to this method would be for the initial headers
        /// (see <see cref="IsHeadersReceived"/> and the second call would indicate the trailers
        /// (see <see cref="IsTrailersReceived"/>.
        /// </summary>
        /// <param name="isInformational"><c>true</c> if the headers contain an informational status code (for responses only).</param>
        /// <returns></returns>
        IHttp2Stream HeadersReceived(bool isInformational);

        /// <summary>
        /// Indicates whether or not the initial headers have been received.
        /// </summary>
        bool IsHeadersReceived { get; }

        /// <summary>
        /// Indicates whether or not the trailers have been received.
        /// </summary>
        bool IsTrailersReceived { get; }

        /// <summary>
        /// Indicates that a push promise was sent to the remote endpoint.
        /// </summary>
        IHttp2Stream PushPromiseSent();

        /// <summary>
        /// Indicates whether or not a push promise was sent to the remote endpoint.
        /// </summary>
        bool IsPushPromiseSent { get; }
    }
}