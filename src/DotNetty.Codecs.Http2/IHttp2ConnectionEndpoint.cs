// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.ComponentModel;

    /// <summary>
    /// A view of the connection from one endpoint (local or remote).
    /// </summary>
    public interface IHttp2ConnectionEndpoint<T> : IHttp2ConnectionEndpoint
        where T : IHttp2FlowController
    {
        /// <summary>
        /// Gets or sets the flow controller for this endpoint.
        /// </summary>
        T FlowController { get; set; }
    }

    /// <summary>
    /// A view of the connection from one endpoint (local or remote).
    /// </summary>
    public interface IHttp2ConnectionEndpoint
    {
        /// <summary>
        /// Increment and get the next generated stream id this endpoint. If negative, the stream IDs are
        /// exhausted for this endpoint an no further streams may be created.
        /// </summary>
        int IncrementAndGetNextStreamId { get; }

        /// <summary>
        /// Indicates whether the given streamId is from the set of IDs used by this endpoint to
        /// create new streams.
        /// </summary>
        /// <param name="streamId"></param>
        /// <returns></returns>
        bool IsValidStreamId(int streamId);

        /// <summary>
        /// Indicates whether or not this endpoint may have created the given stream. This is <c>true</c> if
        /// <see cref="IsValidStreamId(int)"/> and <paramref name="streamId"/> &lt;= <see cref="LastStreamCreated"/>
        /// </summary>
        /// <param name="streamId"></param>
        /// <returns></returns>
        bool MayHaveCreatedStream(int streamId);

        /// <summary>
        /// Indicates whether or not this endpoint created the given stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        bool Created(IHttp2Stream stream);

        /// <summary>
        /// Indicates whether or a stream created by this endpoint can be opened without violating
        /// <see cref="MaxActiveStreams"/>
        /// </summary>
        /// <returns></returns>
        bool CanOpenStream { get; }

        /// <summary>
        /// Creates a stream initiated by this endpoint. This could fail for the following reasons:
        /// <para>The requested stream ID is not the next sequential ID for this endpoint.</para>
        /// <para>The stream already exists.</para>
        /// <para><see cref="CanOpenStream"/> is <c>false</c>.</para>
        /// <para>The connection is marked as going away.</para>
        /// 
        /// <para>The initial state of the stream will be immediately set before notifying <see cref="IHttp2ConnectionListener"/>s.
        /// The state transition is sensitive to <c>halfClosed</c> and is defined by <see cref="IHttp2Stream.Open(bool)"/>.</para>
        /// </summary>
        /// <param name="streamId">The ID of the stream</param>
        /// <param name="halfClosed">see <see cref="IHttp2Stream.Open(bool)"/>.</param>
        /// <returns></returns>
        IHttp2Stream CreateStream(int streamId, bool halfClosed);

        /// <summary>
        /// Creates a push stream in the reserved state for this endpoint and notifies all listeners.
        /// This could fail for the following reasons:
        /// <para>Server push is not allowed to the opposite endpoint.</para>
        /// <para>The requested stream ID is not the next sequential stream ID for this endpoint.</para>
        /// <para>The number of concurrent streams is above the allowed threshold for this endpoint.</para>
        /// <para>The connection is marked as going away.</para>
        /// <para>The parent stream ID does not exist or is not <c>OPEN</c> from the side sending the push
        /// promise.</para>
        /// <para>Could not set a valid priority for the new stream.</para>
        /// </summary>
        /// <param name="streamId">the ID of the push stream</param>
        /// <param name="parent">the parent stream used to initiate the push stream.</param>
        /// <returns></returns>
        IHttp2Stream ReservePushStream(int streamId, IHttp2Stream parent);

        /// <summary>
        /// Indicates whether or not this endpoint is the server-side of the connection.
        /// </summary>
        /// <returns></returns>
        bool IsServer { get; }

        /// <summary>
        /// This is the <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_ENABLE_PUSH</a> value sent
        /// from the opposite endpoint. This method should only be called by Netty (not users) as a result of a
        /// receiving a <c>SETTINGS</c> frame.
        /// </summary>
        /// <param name="allow"></param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        void AllowPushTo(bool allow);

        /// <summary>
        /// This is the <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_ENABLE_PUSH</a> value sent
        /// from the opposite endpoint. The initial value must be <c>true</c> for the client endpoint and always false
        /// for a server endpoint.
        /// </summary>
        bool AllowPushTo();

        /// <summary>
        /// Gets the number of active streams (i.e. <c>OPEN</c> or <c>HALF CLOSED</c>) that were created by this
        /// endpoint.
        /// </summary>
        int NumActiveStreams { get; }

        /// <summary>
        /// Gets the maximum number of streams (created by this endpoint) that are allowed to be active at
        /// the same time. This is the
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_CONCURRENT_STREAMS</a>
        /// value sent from the opposite endpoint to restrict stream creation by this endpoint.
        /// <para>The default value returned by this method must be "unlimited".</para>
        /// </summary>
        int MaxActiveStreams { get; }

        /// <summary>
        /// Sets the limit for <c>SETTINGS_MAX_CONCURRENT_STREAMS</c>.
        /// </summary>
        /// <param name="maxActiveStreams">The maximum number of streams (created by this endpoint) that are allowed to be
        /// active at once. This is the
        /// <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_CONCURRENT_STREAMS</a> value sent
        /// from the opposite endpoint to restrict stream creation by this endpoint.</param>
        void SetMaxActiveStreams(int maxActiveStreams);

        /// <summary>
        /// Gets the ID of the stream last successfully created by this endpoint.
        /// </summary>
        int LastStreamCreated { get; }

        /// <summary>
        /// If a GOAWAY was received for this endpoint, this will be the last stream ID from the
        /// GOAWAY frame. Otherwise, this will be <c> -1</c>.
        /// </summary>
        int LastStreamKnownByPeer();

        /// <summary>
        /// Gets the <see cref="IHttp2ConnectionEndpoint"/> opposite this one.
        /// </summary>
        IHttp2ConnectionEndpoint Opposite { get; }
    }
}
