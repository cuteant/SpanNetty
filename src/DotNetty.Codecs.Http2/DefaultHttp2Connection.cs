// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// Simple implementation of <see cref="IHttp2Connection"/>.
    /// </summary>
    public class DefaultHttp2Connection : IHttp2Connection
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<DefaultHttp2Connection>();

        // Fields accessed by inner classes
        readonly Dictionary<int, DefaultHttp2Stream> streamMap = new Dictionary<int, DefaultHttp2Stream>();
        readonly PropertyKeyRegistry propertyKeyRegistry = new PropertyKeyRegistry();
        readonly Http2ConnectionStream connectionStream;
        readonly DefaultEndpoint<IHttp2LocalFlowController> localEndpoint;
        readonly DefaultEndpoint<IHttp2RemoteFlowController> remoteEndpoint;

        /// <summary>
        /// We chose a <see cref="List{T}"/> over a <see cref="ISet{T}"/> to avoid allocating an <see cref="IEnumerator{T}"/>
        /// objects when iterating over the listeners.
        /// <para>Initial size of 4 because the default configuration currently has 3 listeners
        /// (local/remote flow controller and <see cref="IStreamByteDistributor"/> and we leave room for 1 extra.
        /// We could be more aggressive but the ArrayList resize will double the size if we are too small.</para>
        /// </summary>
        readonly List<IHttp2ConnectionListener> listeners = new List<IHttp2ConnectionListener>(4);
        readonly ActiveStreams activeStreams;
        readonly IPromise closeFuture;

        IPromise closePromise;
        private IPromise InternalClosePromise
        {
            get => Volatile.Read(ref this.closePromise);
            set => Interlocked.Exchange(ref this.closePromise, value);
        }

        /// <summary>
        /// Creates a new connection with the given settings.
        /// </summary>
        /// <param name="server">whether or not this end-point is the server-side of the HTTP/2 connection.</param>
        public DefaultHttp2Connection(bool server)
            : this(server, Http2CodecUtil.DefaultMaxReservedStreams)
        {
        }

        /// <summary>
        /// Creates a new connection with the given settings.
        /// </summary>
        /// <param name="server">whether or not this end-point is the server-side of the HTTP/2 connection.</param>
        /// <param name="maxReservedStreams">The maximum amount of streams which can exist in the reserved state for each endpoint.</param>
        public DefaultHttp2Connection(bool server, int maxReservedStreams)
        {
            this.connectionStream = new Http2ConnectionStream(this);
            this.activeStreams = new ActiveStreams(this, this.listeners);
            // Reserved streams are excluded from the SETTINGS_MAX_CONCURRENT_STREAMS limit according to [1] and the RFC
            // doesn't define a way to communicate the limit on reserved streams. We rely upon the peer to send RST_STREAM
            // in response to any locally enforced limits being exceeded [2].
            // [1] https://tools.ietf.org/html/rfc7540#section-5.1.2
            // [2] https://tools.ietf.org/html/rfc7540#section-8.2.2
            this.localEndpoint = new DefaultEndpoint<IHttp2LocalFlowController>(this, server, server ? int.MaxValue : maxReservedStreams);
            this.remoteEndpoint = new DefaultEndpoint<IHttp2RemoteFlowController>(this, !server, maxReservedStreams);

            // Add the connection stream to the map.
            this.streamMap.Add(this.connectionStream.Id, this.connectionStream);
            this.closeFuture = new TaskCompletionSource();
        }

        /// <summary>
        /// Determine if <see cref="CloseAsync(IPromise)"/> has been called and no more streams are allowed to be created.
        /// </summary>
        /// <returns></returns>
        bool IsClosed()
        {
            return this.InternalClosePromise is object;
        }

        public Task CloseCompletion => this.closeFuture.Task;

        public Task CloseAsync(IPromise promise)
        {
            if (promise is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.promise); }

            // Since we allow this method to be called multiple times, we must make sure that all the promises are notified
            // when all streams are removed and the close operation completes.
            var prevClosePromise = this.InternalClosePromise;
            if (prevClosePromise is object)
            {
                if (ReferenceEquals(prevClosePromise, promise))
                {
                    // Do nothing
                }
                else if (prevClosePromise.IsVoid)
                {
                    this.InternalClosePromise = promise;
                }
            }
            else
            {
                this.InternalClosePromise = promise;
            }
            if (!promise.IsVoid)
            {
                this.closeFuture.Task.CascadeTo(promise, Logger);
            }

            if (IsStreamMapEmpty())
            {
                this.closeFuture.TryComplete();
                return this.closeFuture.Task;
            }

            //IEnumerator<KeyValuePair<int, IHttp2Stream>> itr = streamMap.GetEnumerator();
            //copying streams to array to be able to modify streamMap
            DefaultHttp2Stream[] streams = this.streamMap.Values.ToArray();

            // We must take care while iterating the streamMap as to not modify while iterating in case there are other code
            // paths iterating over the active streams.
            if (this.activeStreams.AllowModifications())
            {
                this.activeStreams.IncrementPendingIterations();
                try
                {
                    for (int i = 0; i < streams.Length; i++)
                    {
                        DefaultHttp2Stream stream = streams[i];
                        if (stream.Id != Http2CodecUtil.ConnectionStreamId)
                        {
                            // If modifications of the activeStream map is allowed, then a stream close operation will also
                            // modify the streamMap. Pass the iterator in so that remove will be called to prevent
                            // concurrent modification exceptions.
                            stream.Close(true);
                        }
                    }
                }
                finally
                {
                    this.activeStreams.DecrementPendingIterations();
                }
            }
            else
            {
                for (int i = 0; i < streams.Length; i++)
                {
                    DefaultHttp2Stream stream = streams[i];
                    if (stream.Id != Http2CodecUtil.ConnectionStreamId)
                    {
                        // We are not allowed to make modifications, so the close calls will be executed after this
                        // iteration completes.
                        stream.Close();
                    }
                }
            }

            return this.closeFuture.Task;
        }

        public void AddListener(IHttp2ConnectionListener listener)
        {
            this.listeners.Add(listener);
        }

        public void RemoveListener(IHttp2ConnectionListener listener)
        {
            this.listeners.Remove(listener);
        }

        public bool IsServer => this.localEndpoint.IsServer;

        public IHttp2Stream ConnectionStream => this.connectionStream;

        public IHttp2Stream Stream(int streamId)
        {
            return this.streamMap.TryGetValue(streamId, out DefaultHttp2Stream result) ? result : null;
        }

        public bool StreamMayHaveExisted(int streamId)
        {
            return this.remoteEndpoint.MayHaveCreatedStream(streamId) || this.localEndpoint.MayHaveCreatedStream(streamId);
        }

        public int NumActiveStreams => this.activeStreams.Size();

        public IHttp2Stream ForEachActiveStream(IHttp2StreamVisitor visitor)
        {
            return this.activeStreams.ForEachActiveStream(visitor);
        }

        public IHttp2Stream ForEachActiveStream(Func<IHttp2Stream, bool> visitor)
        {
            return this.activeStreams.ForEachActiveStream(visitor);
        }

        public IHttp2ConnectionEndpoint<IHttp2LocalFlowController> Local => this.localEndpoint;

        public IHttp2ConnectionEndpoint<IHttp2RemoteFlowController> Remote => this.remoteEndpoint;

        public bool GoAwayReceived()
        {
            return this.localEndpoint.lastStreamKnownByPeer >= 0;
        }

        public void GoAwayReceived(int lastKnownStream, Http2Error errorCode, IByteBuffer debugData)
        {
            var oldLastStreamKnownByPeer = this.localEndpoint.LastStreamKnownByPeer();
            if (oldLastStreamKnownByPeer >= 0 && oldLastStreamKnownByPeer < lastKnownStream)
            {
                ThrowHelper.ThrowConnectionError_LastStreamIdMustNotIncrease(oldLastStreamKnownByPeer, lastKnownStream);
            }
            this.localEndpoint.LastStreamKnownByPeer(lastKnownStream);
            for (int i = 0; i < this.listeners.Count; ++i)
            {
                try
                {
                    this.listeners[i].OnGoAwayReceived(lastKnownStream, errorCode, debugData);
                }
                catch (Exception cause)
                {
                    Logger.CaughtExceptionFromListenerOnGoAwayReceived(cause);
                }
            }

            this.CloseStreamsGreaterThanLastKnownStreamId(lastKnownStream, this.localEndpoint);
        }

        public bool GoAwaySent()
        {
            return this.remoteEndpoint.lastStreamKnownByPeer >= 0;
        }

        public bool GoAwaySent(int lastKnownStream, Http2Error errorCode, IByteBuffer debugData)
        {
            var oldLastStreamKnownByPeer = this.remoteEndpoint.LastStreamKnownByPeer();
            if (oldLastStreamKnownByPeer >= 0)
            {
                // Protect against re-entrancy. Could happen if writing the frame fails, and error handling
                // treating this is a connection handler and doing a graceful shutdown...
                if (lastKnownStream == oldLastStreamKnownByPeer) { return false; }
                if (lastKnownStream > oldLastStreamKnownByPeer)
                {
                    ThrowHelper.ThrowConnectionError_LastStreamIdentifierMustNotIncreaseBetween(oldLastStreamKnownByPeer, lastKnownStream);
                }
            }

            this.remoteEndpoint.LastStreamKnownByPeer(lastKnownStream);
            for (int i = 0; i < this.listeners.Count; ++i)
            {
                try
                {
                    this.listeners[i].OnGoAwaySent(lastKnownStream, errorCode, debugData);
                }
                catch (Exception cause)
                {
                    Logger.CaughtExceptionFromListenerOnGoAwaySent(cause);
                }
            }

            this.CloseStreamsGreaterThanLastKnownStreamId(lastKnownStream, this.remoteEndpoint);
            return true;
        }

        private void CloseStreamsGreaterThanLastKnownStreamId(int lastKnownStream, DefaultEndpoint endpoint)
        {
            this.ForEachActiveStream(localVisit);
            bool localVisit(IHttp2Stream stream)
            {
                if (stream.Id > lastKnownStream && endpoint.IsValidStreamId(stream.Id))
                {
                    stream.Close();
                }
                return true;
            }
        }

        /// <summary>
        /// Determine if <see cref="streamMap"/> only contains the connection stream.
        /// </summary>
        /// <returns></returns>
        private bool IsStreamMapEmpty() => this.streamMap.Count == 1;

        /// <summary>
        /// Remove a stream from the <see cref="streamMap"/>.
        /// </summary>
        /// <param name="stream">the stream to remove.</param>
        void RemoveStream(DefaultHttp2Stream stream)
        {
            bool removed = this.streamMap.Remove(stream.Id);

            if (removed)
            {
                for (int i = 0; i < this.listeners.Count; i++)
                {
                    try
                    {
                        this.listeners[i].OnStreamRemoved(stream);
                    }
                    catch (Exception cause)
                    {
                        Logger.CaughtExceptionFromListenerOnStreamRemoved(cause);
                    }
                }

                if (this.InternalClosePromise is object && IsStreamMapEmpty())
                {
                    this.closeFuture.TryComplete();
                }
            }
        }

        static Http2StreamState ActiveState(int streamId, Http2StreamState initialState, bool isLocal, bool halfClosed)
        {
            if (initialState == Http2StreamState.Idle)
            {
                return halfClosed ? isLocal ? Http2StreamState.HalfClosedLocal : Http2StreamState.HalfClosedRemote : Http2StreamState.Open;
            }
            else if (initialState == Http2StreamState.ReservedLocal)
            {
                return Http2StreamState.HalfClosedRemote;
            }
            else if (initialState == Http2StreamState.ReservedRemote)
            {
                return Http2StreamState.HalfClosedLocal;
            }

            return ThrowHelper.ThrowStreamError_AttemptingToOpenAStreamInAnInvalidState(streamId, initialState);
        }

        void NotifyHalfClosed(IHttp2Stream stream)
        {
            for (int i = 0; i < this.listeners.Count; i++)
            {
                try
                {
                    this.listeners[i].OnStreamHalfClosed(stream);
                }
                catch (Exception cause)
                {
                    Logger.CaughtExceptionFromListenerOnStreamHalfClosed(cause);
                }
            }
        }

        void NotifyClosed(IHttp2Stream stream)
        {
            for (int i = 0; i < this.listeners.Count; i++)
            {
                try
                {
                    this.listeners[i].OnStreamClosed(stream);
                }
                catch (Exception cause)
                {
                    Logger.CaughtExceptionFromListenerOnStreamClosed(cause);
                }
            }
        }

        public IHttp2ConnectionPropertyKey NewKey()
        {
            return this.propertyKeyRegistry.NewKey(this);
        }

        /// <summary>
        /// Verifies that the key is valid and returns it as the internal <see cref="DefaultPropertyKey"/> type.
        /// </summary>
        /// <param name="key"></param>
        /// <exception cref="ArgumentNullException">if the key is <c>null</c> or not of type <see cref="DefaultPropertyKey"/>.</exception>
        /// <exception cref="ArgumentException">if the key was not created by this connection.</exception>
        /// <returns></returns>
        DefaultPropertyKey VerifyKey(IHttp2ConnectionPropertyKey key)
        {
            var dpk = key as DefaultPropertyKey;
            if (dpk is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.key); }
            return dpk.VerifyConnection(this);
        }

        /// <summary>
        /// Simple stream implementation. Streams can be compared to each other by priority.
        /// </summary>
        internal class DefaultHttp2Stream : IHttp2Stream
        {
            const int MetaStateSentRst = 1;
            const int MetaStateSentHeaders = 1 << 1;
            const int MetaStatesentTrailers = 1 << 2;
            const int MetaStateSentPushpromise = 1 << 3;
            const int MetaStateRecvHeaders = 1 << 4;
            const int MetaStateRecvTrailers = 1 << 5;

            readonly DefaultHttp2Connection conn;
            readonly int id;
            readonly PropertyMap properties;
            private Http2StreamState state;
            private int metaState;

            internal DefaultHttp2Stream(DefaultHttp2Connection conn, int id, Http2StreamState state)
            {
                this.conn = conn;
                this.id = id;
                this.state = state;
                this.properties = new PropertyMap(conn.propertyKeyRegistry);
            }

            public int Id => this.id;

            public Http2StreamState State => this.state;

            public virtual bool IsResetSent => (this.metaState & MetaStateSentRst) != 0;

            public virtual IHttp2Stream ResetSent()
            {
                this.metaState |= MetaStateSentRst;
                return this;
            }

            public virtual IHttp2Stream HeadersSent(bool isInformational)
            {
                if (!isInformational)
                {
                    this.metaState |= this.IsHeadersSent ? MetaStatesentTrailers : MetaStateSentHeaders;
                }

                return this;
            }

            public virtual bool IsHeadersSent => (this.metaState & MetaStateSentHeaders) != 0;

            public bool IsTrailersSent => (this.metaState & MetaStatesentTrailers) != 0;

            public IHttp2Stream HeadersReceived(bool isInformational)
            {
                if (!isInformational)
                {
                    this.metaState |= this.IsHeadersReceived ? MetaStateRecvTrailers : MetaStateRecvHeaders;
                }

                return this;
            }

            public bool IsHeadersReceived => (this.metaState & MetaStateRecvHeaders) != 0;

            public bool IsTrailersReceived => (this.metaState & MetaStateRecvTrailers) != 0;

            public virtual IHttp2Stream PushPromiseSent()
            {
                this.metaState |= MetaStateSentPushpromise;
                return this;
            }

            public virtual bool IsPushPromiseSent => (this.metaState & MetaStateSentPushpromise) != 0;

            public V SetProperty<V>(IHttp2ConnectionPropertyKey key, object value)
            {
                var prevValue = this.properties.Add(this.conn.VerifyKey(key), value);
                return prevValue is V ? (V)prevValue : default;
            }

            public object SetProperty(IHttp2ConnectionPropertyKey key, object value)
            {
                return this.properties.Add(this.conn.VerifyKey(key), value);
            }

            public V GetProperty<V>(IHttp2ConnectionPropertyKey key)
            {
                return (V)this.properties.Get(this.conn.VerifyKey(key));
            }

            public object RemoveProperty(IHttp2ConnectionPropertyKey key)
            {
                return this.properties.Remove(this.conn.VerifyKey(key));
            }

            public V RemoveProperty<V>(IHttp2ConnectionPropertyKey key)
            {
                return (V)this.properties.Remove(this.conn.VerifyKey(key));
            }

            public virtual IHttp2Stream Open(bool halfClosed)
            {
                this.state = ActiveState(this.id, this.state, this.IsLocal(), halfClosed);
                if (!this.CreatedBy().CanOpenStream)
                {
                    ThrowHelper.ThrowConnectionError_MaximumActiveStreamsViolatedForThisEndpoint();
                }

                this.Activate();
                return this;
            }

            internal void Activate()
            {
                // If the stream is opened in a half-closed state, the headers must have either
                // been sent if this is a local stream, or received if it is a remote stream.
                if (this.state == Http2StreamState.HalfClosedLocal)
                {
                    this.HeadersSent(/*isInformational*/ false);
                }
                else if (this.state == Http2StreamState.HalfClosedRemote)
                {
                    this.HeadersReceived(/*isInformational*/ false);
                }
                this.conn.activeStreams.Activate(this);
            }

            public virtual IHttp2Stream Close() => this.Close(false);

            public virtual IHttp2Stream Close(bool force)
            {
                if (this.state == Http2StreamState.Closed)
                {
                    return this;
                }

                this.state = Http2StreamState.Closed;

                --this.CreatedBy().numStreams;
                this.conn.activeStreams.Deactivate(this, force);
                return this;
            }

            public virtual IHttp2Stream CloseLocalSide()
            {
                if (this.state == Http2StreamState.Open)
                {
                    this.state = Http2StreamState.HalfClosedLocal;
                    this.conn.NotifyHalfClosed(this);
                }
                else if (this.state == Http2StreamState.HalfClosedLocal)
                {
                }
                else
                {
                    this.Close();
                }

                return this;
            }

            public virtual IHttp2Stream CloseRemoteSide()
            {
                if (state == Http2StreamState.Open)
                {
                    state = Http2StreamState.HalfClosedRemote;
                    this.conn.NotifyHalfClosed(this);
                }
                else if (this.state == Http2StreamState.HalfClosedRemote)
                {
                }
                else
                {
                    this.Close();
                }

                return this;
            }

            internal virtual DefaultEndpoint CreatedBy()
            {
                return this.conn.localEndpoint.IsValidStreamId(this.id)
                    ? (DefaultEndpoint)this.conn.localEndpoint
                    : this.conn.remoteEndpoint;
            }

            bool IsLocal()
            {
                return this.conn.localEndpoint.IsValidStreamId(this.id);
            }

            /// <summary>
            /// Provides the lazy initialization for the <see cref="DefaultHttp2Stream"/> data map.
            /// </summary>
            sealed class PropertyMap
            {
                readonly PropertyKeyRegistry registry;
                object[] values = EmptyArrays.EmptyObjects;

                public PropertyMap(PropertyKeyRegistry registry)
                {
                    this.registry = registry;
                }

                internal object Add(DefaultPropertyKey key, object value)
                {
                    this.ResizeIfNecessary(key.index);
                    object prevValue = this.values[key.index];
                    this.values[key.index] = value;
                    return prevValue;
                }

                internal object Get(DefaultPropertyKey key)
                {
                    var keyIndex = key.index;
                    var thisValues = this.values;
                    if ((uint)keyIndex < (uint)thisValues.Length) { return thisValues[keyIndex]; }

                    return default;
                }

                internal object Remove(DefaultPropertyKey key)
                {
                    object prevValue = null;
                    var keyIndex = key.index;
                    var thisValues = this.values;
                    if ((uint)keyIndex < (uint)thisValues.Length)
                    {
                        prevValue = thisValues[keyIndex];
                        thisValues[keyIndex] = null;
                    }

                    return prevValue;
                }

                void ResizeIfNecessary(int index)
                {
                    if (index >= this.values.Length)
                    {
                        object[] tmp = new object[this.registry.Size];
                        if ((uint)this.values.Length > 0u)
                        {
                            Array.Copy(this.values, tmp, Math.Min(this.values.Length, tmp.Length));
                        }
                        this.values = tmp;
                    }
                }
            }
        }

        /// <summary>
        /// Stream class representing the connection, itself.
        /// </summary>
        sealed class Http2ConnectionStream : DefaultHttp2Stream
        {
            internal Http2ConnectionStream(DefaultHttp2Connection conn)
                : base(conn, Http2CodecUtil.ConnectionStreamId, Http2StreamState.Idle)
            {
            }

            public override bool IsResetSent => false;

            internal override DefaultEndpoint CreatedBy() => null;

            public override IHttp2Stream ResetSent() => throw new NotSupportedException();

            public override IHttp2Stream Open(bool halfClosed) => throw new NotSupportedException();

            public override IHttp2Stream Close() => throw new NotSupportedException();

            public override IHttp2Stream CloseLocalSide() => throw new NotSupportedException();

            public override IHttp2Stream CloseRemoteSide() => throw new NotSupportedException();

            public override IHttp2Stream HeadersSent(bool isInformational) => throw new NotSupportedException();

            public override bool IsHeadersSent => throw new NotSupportedException();

            public override IHttp2Stream PushPromiseSent() => throw new NotSupportedException();

            public override bool IsPushPromiseSent => throw new NotSupportedException();
        }

        internal class DefaultEndpoint<F> : DefaultEndpoint, IHttp2ConnectionEndpoint<F>
            where F : class, IHttp2FlowController
        {
            F flowController;

            internal DefaultEndpoint(DefaultHttp2Connection conn, bool server, int maxReservedStreams)
                : base(conn, server, maxReservedStreams)
            {
            }

            public F FlowController
            {
                get => this.flowController;
                set
                {
                    if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                    this.flowController = value;
                }
            }
        }

        /// <summary>
        /// Simple endpoint implementation.
        /// </summary>
        internal class DefaultEndpoint : IHttp2ConnectionEndpoint
        {
            readonly DefaultHttp2Connection conn;

            readonly bool server;

            /// <summary>
            /// The minimum stream ID allowed when creating the next stream. This only applies at the time the stream is
            /// created. If the ID of the stream being created is less than this value, stream creation will fail. Upon
            /// successful creation of a stream, this value is incremented to the next valid stream ID.
            /// </summary>
            private int nextStreamIdToCreate;

            /// <summary>
            /// Used for reservation of stream IDs. Stream IDs can be reserved in advance by applications before the streams
            /// are actually created.  For example, applications may choose to buffer stream creation attempts as a way of
            /// working around <c>SETTINGS_MAX_CONCURRENT_STREAMS</c>, in which case they will reserve stream IDs for each
            /// buffered stream.
            /// </summary>
            private int nextReservationStreamId;
            internal int lastStreamKnownByPeer = -1;
            private bool pushToAllowed = true;

            private int maxStreams;
            private int maxActiveStreams;

            private readonly int maxReservedStreams;

            // Fields accessed by inner classes
            internal int numActiveStreams;
            internal int numStreams;

            internal DefaultEndpoint(DefaultHttp2Connection conn, bool server, int maxReservedStreams)
            {
                this.conn = conn;
                this.server = server;

                // Determine the starting stream ID for this endpoint. Client-initiated streams
                // are odd and server-initiated streams are even. Zero is reserved for the
                // connection. Stream 1 is reserved client-initiated stream for responding to an
                // upgrade from HTTP 1.1.
                if (server)
                {
                    this.nextStreamIdToCreate = 2;
                    this.nextReservationStreamId = 0;
                }
                else
                {
                    this.nextStreamIdToCreate = 1;
                    // For manually created client-side streams, 1 is reserved for HTTP upgrade, so start at 3.
                    this.nextReservationStreamId = 1;
                }

                // Push is disallowed by default for servers and allowed for clients.
                this.pushToAllowed = !server;
                this.maxActiveStreams = int.MaxValue;
                if (maxReservedStreams < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(maxReservedStreams, ExceptionArgument.maxReservedStreams); }
                this.maxReservedStreams = maxReservedStreams;
                this.UpdateMaxStreams();
            }

            public int IncrementAndGetNextStreamId =>
                this.nextReservationStreamId >= 0 ? this.nextReservationStreamId += 2 : this.nextReservationStreamId;

            private void IncrementExpectedStreamId(int streamId)
            {
                if (streamId > this.nextReservationStreamId && this.nextReservationStreamId >= 0)
                {
                    this.nextReservationStreamId = streamId;
                }

                this.nextStreamIdToCreate = streamId + 2;
                ++this.numStreams;
            }

            public bool IsValidStreamId(int streamId)
            {
                return streamId > 0 && this.server == (0u >= (uint)(streamId & 1));
            }

            public bool MayHaveCreatedStream(int streamId)
            {
                return this.IsValidStreamId(streamId) && streamId <= this.LastStreamCreated;
            }

            public bool CanOpenStream => this.numActiveStreams < this.maxActiveStreams;

            public IHttp2Stream CreateStream(int streamId, bool halfClosed)
            {
                Http2StreamState state = ActiveState(streamId, Http2StreamState.Idle, this.IsLocal(), halfClosed);

                this.CheckNewStreamAllowed(streamId, state);

                // Create and initialize the stream.
                DefaultHttp2Stream stream = new DefaultHttp2Stream(this.conn, streamId, state);

                this.IncrementExpectedStreamId(streamId);

                this.AddStream(stream);

                stream.Activate();
                return stream;
            }

            public bool Created(IHttp2Stream stream)
            {
                return stream is DefaultHttp2Stream defaultStream && ReferenceEquals(defaultStream.CreatedBy(), this);
            }

            public bool IsServer => this.server;

            public IHttp2Stream ReservePushStream(int streamId, IHttp2Stream parent)
            {
                if (parent is null)
                {
                    ThrowHelper.ThrowConnectionError_ParentStreamMissing();
                }

                if (this.IsLocal() ? !parent.State.LocalSideOpen : !parent.State.RemoteSideOpen)
                {
                    ThrowHelper.ThrowConnectionError_StreamIsNotOpenForSendingPushPromise(parent.Id);
                }

                if (!this.Opposite.AllowPushTo())
                {
                    ThrowHelper.ThrowConnectionError_ServerPushNotAllowedToOppositeEndpoint();
                }

                Http2StreamState state = this.IsLocal() ? Http2StreamState.ReservedLocal : Http2StreamState.ReservedRemote;
                this.CheckNewStreamAllowed(streamId, state);

                // Create and initialize the stream.
                DefaultHttp2Stream stream = new DefaultHttp2Stream(this.conn, streamId, state);

                this.IncrementExpectedStreamId(streamId);

                // Register the stream.
                this.AddStream(stream);
                return stream;
            }

            private void AddStream(DefaultHttp2Stream stream)
            {
                // Add the stream to the map and priority tree.
                this.conn.streamMap.Add(stream.Id, stream);

                // Notify the listeners of the event.
                var listeners = this.conn.listeners;
                for (int i = 0; i < listeners.Count; i++)
                {
                    try
                    {
                        listeners[i].OnStreamAdded(stream);
                    }
                    catch (Exception cause)
                    {
                        Logger.CaughtExceptionFromListenerOnStreamAdded(cause);
                    }
                }
            }

            public void AllowPushTo(bool allow)
            {
                if (allow && this.server)
                {
                    ThrowHelper.ThrowArgumentException_ServersDoNotAllowPush();
                }

                this.pushToAllowed = allow;
            }

            public bool AllowPushTo() => this.pushToAllowed;

            public int NumActiveStreams => this.numActiveStreams;

            public int MaxActiveStreams => this.maxActiveStreams;

            public void SetMaxActiveStreams(int maxActiveStreams)
            {
                this.maxActiveStreams = maxActiveStreams;
                this.UpdateMaxStreams();
            }

            public int LastStreamCreated => this.nextStreamIdToCreate > 1 ? this.nextStreamIdToCreate - 2 : 0;

            public int LastStreamKnownByPeer() => this.lastStreamKnownByPeer;

            public void LastStreamKnownByPeer(int lastKnownStream)
            {
                this.lastStreamKnownByPeer = lastKnownStream;
            }

            public IHttp2ConnectionEndpoint Opposite =>
                this.IsLocal() ? (IHttp2ConnectionEndpoint)this.conn.remoteEndpoint : this.conn.localEndpoint;

            private void UpdateMaxStreams()
            {
                this.maxStreams = (int)Math.Min(int.MaxValue, (long)this.maxActiveStreams + this.maxReservedStreams);
            }

            private void CheckNewStreamAllowed(int streamId, Http2StreamState state)
            {
                Debug.Assert(state != Http2StreamState.Idle);
                if (this.lastStreamKnownByPeer >= 0 && streamId > this.lastStreamKnownByPeer)
                {
                    ThrowHelper.ThrowStreamError_CannotCreateStreamGreaterThanLastStreamIDFromGoAway(streamId, this.lastStreamKnownByPeer);
                }

                if (!this.IsValidStreamId(streamId))
                {
                    if (streamId < 0)
                    {
                        ThrowHelper.ThrowHttp2NoMoreStreamIdsException();
                    }

                    ThrowHelper.ThrowConnectionError_RequestStreamIsNotCorrectForConnection(streamId, this.server);
                }

                // This check must be after all id validated checks, but before the max streams check because it may be
                // recoverable to some degree for handling frames which can be sent on closed streams.
                if (streamId < this.nextStreamIdToCreate)
                {
                    ThrowHelper.ThrowClosedStreamError_RequestStreamIsBehindTheNextExpectedStream(streamId, this.nextStreamIdToCreate);
                }

                if (this.nextStreamIdToCreate <= 0)
                {
                    ThrowHelper.ThrowConnectionError_StreamIDsAreExhaustedForThisEndpoint();
                }

                bool isReserved = state == Http2StreamState.ReservedLocal || state == Http2StreamState.ReservedRemote;
                if (!isReserved && !this.CanOpenStream || isReserved && this.numStreams >= this.maxStreams)
                {
                    ThrowHelper.ThrowStreamError_MaximumActiveStreamsViolatedForThisEndpoint(streamId);
                }

                if (this.conn.IsClosed())
                {
                    ThrowHelper.ThrowConnectionError_AttemptedToCreateStreamAfterConnectionWasClosed(streamId);
                }
            }

            bool IsLocal() => ReferenceEquals(this, this.conn.localEndpoint);
        }

        /// <summary>
        /// Manages the list of currently active streams.  Queues any <see cref="Action"/>s that would modify the list of
        /// active streams in order to prevent modification while iterating.
        /// </summary>
        sealed class ActiveStreams
        {
            readonly DefaultHttp2Connection conn;
            readonly List<IHttp2ConnectionListener> listeners;
            readonly Deque<Action> pendingEvents = new Deque<Action>();
            readonly ISet<IHttp2Stream> streams = new HashSet<IHttp2Stream>();
            private int pendingIterations;

            public ActiveStreams(DefaultHttp2Connection conn, List<IHttp2ConnectionListener> listeners)
            {
                this.conn = conn;
                this.listeners = listeners;
            }

            public int Size()
            {
                return this.streams.Count;
            }

            public void Activate(DefaultHttp2Stream stream)
            {
                if (this.AllowModifications())
                {
                    this.AddToActiveStreams(stream);
                }
                else
                {
                    this.pendingEvents.AddToBack(() => this.AddToActiveStreams(stream));
                }
            }

            internal void Deactivate(DefaultHttp2Stream stream, bool force)
            {
                if (this.AllowModifications() || force)
                {
                    this.RemoveFromActiveStreams(stream);
                }
                else
                {
                    this.pendingEvents.AddToBack(() => this.RemoveFromActiveStreams(stream));
                }
            }

            public IHttp2Stream ForEachActiveStream(IHttp2StreamVisitor visitor)
            {
                this.IncrementPendingIterations();
                try
                {
                    foreach (IHttp2Stream stream in streams)
                    {
                        if (!visitor.Visit(stream))
                        {
                            return stream;
                        }
                    }

                    return null;
                }
                finally
                {
                    this.DecrementPendingIterations();
                }
            }

            public IHttp2Stream ForEachActiveStream(Func<IHttp2Stream, bool> visitor)
            {
                this.IncrementPendingIterations();
                try
                {
                    foreach (IHttp2Stream stream in streams)
                    {
                        if (!visitor(stream))
                        {
                            return stream;
                        }
                    }

                    return null;
                }
                finally
                {
                    this.DecrementPendingIterations();
                }
            }

            void AddToActiveStreams(DefaultHttp2Stream stream)
            {
                if (this.streams.Add(stream))
                {
                    // Update the number of active streams initiated by the endpoint.
                    stream.CreatedBy().numActiveStreams++;

                    for (int i = 0; i < this.listeners.Count; i++)
                    {
                        try
                        {
                            this.listeners[i].OnStreamActive(stream);
                        }
                        catch (Exception cause)
                        {
                            Logger.CaughtExceptionFromListenerOnStreamActive(cause);
                        }
                    }
                }
            }

            void RemoveFromActiveStreams(DefaultHttp2Stream stream)
            {
                if (this.streams.Remove(stream))
                {
                    // Update the number of active streams initiated by the endpoint.
                    stream.CreatedBy().numActiveStreams--;
                    this.conn.NotifyClosed(stream);
                }

                this.conn.RemoveStream(stream);
            }

            internal bool AllowModifications()
            {
                return 0u >= (uint)this.pendingIterations;
            }

            internal void IncrementPendingIterations()
            {
                ++this.pendingIterations;
            }

            internal void DecrementPendingIterations()
            {
                --this.pendingIterations;
                if (this.AllowModifications())
                {
                    while (this.pendingEvents.TryRemoveFromFront(out Action evt))
                    {
                        try
                        {
                            evt();
                        }
                        catch (Exception cause)
                        {
                            Logger.CaughtExceptionWhileProcessingPendingActiveStreamsEvent(cause);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Implementation of <see cref="IHttp2ConnectionPropertyKey"/> that specifies the index position of the property.
        /// </summary>
        sealed class DefaultPropertyKey : IHttp2ConnectionPropertyKey
        {
            readonly DefaultHttp2Connection conn;
            internal readonly int index;

            internal DefaultPropertyKey(DefaultHttp2Connection conn, int index)
            {
                this.conn = conn;
                this.index = index;
            }

            internal DefaultPropertyKey VerifyConnection(IHttp2Connection connection)
            {
                if (connection != this.conn)
                {
                    ThrowHelper.ThrowArgumentException_UsingAKeyThatWasNotCreatedByThisConnection();
                }

                return this;
            }
        }

        /// <summary>
        /// A registry of all stream property keys known by this connection.
        /// </summary>
        sealed class PropertyKeyRegistry
        {
            /// <summary>
            /// Initial size of 4 because the default configuration currently has 3 listeners
            /// (local/remote flow controller and <see cref="IStreamByteDistributor"/>) and we leave room for 1 extra.
            /// We could be more aggressive but the ArrayList resize will double the size if we are too small.
            /// </summary>
            readonly IList<DefaultPropertyKey> keys = new List<DefaultPropertyKey>(4);

            /// <summary>
            /// Registers a new property key.
            /// </summary>
            /// <param name="conn"></param>
            /// <returns></returns>
            internal DefaultPropertyKey NewKey(DefaultHttp2Connection conn)
            {
                var key = new DefaultPropertyKey(conn, keys.Count);
                this.keys.Add(key);
                return key;
            }

            internal int Size => this.keys.Count;
        }
    }
}