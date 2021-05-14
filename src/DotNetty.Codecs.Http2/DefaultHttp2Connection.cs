/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

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
        private static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<DefaultHttp2Connection>();

        // Fields accessed by inner classes
        private readonly Dictionary<int, DefaultHttp2Stream> _streamMap;
        private readonly PropertyKeyRegistry _propertyKeyRegistry;
        private readonly Http2ConnectionStream _connectionStream;
        private readonly DefaultEndpoint<IHttp2LocalFlowController> _localEndpoint;
        private readonly DefaultEndpoint<IHttp2RemoteFlowController> _remoteEndpoint;

        /// <summary>
        /// We chose a <see cref="List{T}"/> over a <see cref="ISet{T}"/> to avoid allocating an <see cref="IEnumerator{T}"/>
        /// objects when iterating over the listeners.
        /// <para>Initial size of 4 because the default configuration currently has 3 listeners
        /// (local/remote flow controller and <see cref="IStreamByteDistributor"/> and we leave room for 1 extra.
        /// We could be more aggressive but the ArrayList resize will double the size if we are too small.</para>
        /// </summary>
        private readonly List<IHttp2ConnectionListener> _listeners;
        private readonly ActiveStreams _activeStreams;
        private readonly IPromise _closeFuture;

        private IPromise v_closePromise;
        private IPromise InternalClosePromise
        {
            get => Volatile.Read(ref v_closePromise);
            set => Interlocked.Exchange(ref v_closePromise, value);
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
            _streamMap = new Dictionary<int, DefaultHttp2Stream>();
            _propertyKeyRegistry = new PropertyKeyRegistry();
            _listeners = new List<IHttp2ConnectionListener>(4);
            _connectionStream = new Http2ConnectionStream(this);
            _activeStreams = new ActiveStreams(this, _listeners);
            // Reserved streams are excluded from the SETTINGS_MAX_CONCURRENT_STREAMS limit according to [1] and the RFC
            // doesn't define a way to communicate the limit on reserved streams. We rely upon the peer to send RST_STREAM
            // in response to any locally enforced limits being exceeded [2].
            // [1] https://tools.ietf.org/html/rfc7540#section-5.1.2
            // [2] https://tools.ietf.org/html/rfc7540#section-8.2.2
            _localEndpoint = new DefaultEndpoint<IHttp2LocalFlowController>(this, server, server ? int.MaxValue : maxReservedStreams);
            _remoteEndpoint = new DefaultEndpoint<IHttp2RemoteFlowController>(this, !server, maxReservedStreams);

            // Add the connection stream to the map.
            _streamMap.Add(_connectionStream.Id, _connectionStream);
            _closeFuture = new DefaultPromise();
        }

        /// <summary>
        /// Determine if <see cref="CloseAsync(IPromise)"/> has been called and no more streams are allowed to be created.
        /// </summary>
        /// <returns></returns>
        bool IsClosed()
        {
            return InternalClosePromise is object;
        }

        public Task CloseCompletion => _closeFuture.Task;

        public Task CloseAsync(IPromise promise)
        {
            if (promise is null) { return ThrowHelper.FromArgumentNullException(ExceptionArgument.promise); }

            // Since we allow this method to be called multiple times, we must make sure that all the promises are notified
            // when all streams are removed and the close operation completes.
            var prevClosePromise = InternalClosePromise;
            if (prevClosePromise is object)
            {
                if (ReferenceEquals(prevClosePromise, promise))
                {
                    // Do nothing
                }
                else if (prevClosePromise.IsVoid)
                {
                    InternalClosePromise = promise;
                }
            }
            else
            {
                InternalClosePromise = promise;
            }
            if (!promise.IsVoid)
            {
                _closeFuture.Task.CascadeTo(promise, Logger);
            }

            if (IsStreamMapEmpty())
            {
                _ = _closeFuture.TryComplete();
                return _closeFuture.Task;
            }

            //IEnumerator<KeyValuePair<int, IHttp2Stream>> itr = streamMap.GetEnumerator();
            //copying streams to array to be able to modify streamMap
            DefaultHttp2Stream[] streams = _streamMap.Values.ToArray();

            // We must take care while iterating the streamMap as to not modify while iterating in case there are other code
            // paths iterating over the active streams.
            if (_activeStreams.AllowModifications())
            {
                _activeStreams.IncrementPendingIterations();
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
                            _ = stream.Close(true);
                        }
                    }
                }
                finally
                {
                    _activeStreams.DecrementPendingIterations();
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
                        _ = stream.Close();
                    }
                }
            }

            return _closeFuture.Task;
        }

        public void AddListener(IHttp2ConnectionListener listener)
        {
            _listeners.Add(listener);
        }

        public void RemoveListener(IHttp2ConnectionListener listener)
        {
            _ = _listeners.Remove(listener);
        }

        public bool IsServer => _localEndpoint.IsServer;

        public IHttp2Stream ConnectionStream => _connectionStream;

        public IHttp2Stream Stream(int streamId)
        {
            return _streamMap.TryGetValue(streamId, out DefaultHttp2Stream result) ? result : null;
        }

        public bool StreamMayHaveExisted(int streamId)
        {
            return _remoteEndpoint.MayHaveCreatedStream(streamId) || _localEndpoint.MayHaveCreatedStream(streamId);
        }

        public int NumActiveStreams => _activeStreams.Size();

        public IHttp2Stream ForEachActiveStream(IHttp2StreamVisitor visitor)
        {
            return _activeStreams.ForEachActiveStream(visitor);
        }

        public IHttp2Stream ForEachActiveStream(Func<IHttp2Stream, bool> visitor)
        {
            return _activeStreams.ForEachActiveStream(visitor);
        }

        public IHttp2ConnectionEndpoint<IHttp2LocalFlowController> Local => _localEndpoint;

        public IHttp2ConnectionEndpoint<IHttp2RemoteFlowController> Remote => _remoteEndpoint;

        public bool GoAwayReceived()
        {
            return _localEndpoint._lastStreamKnownByPeer >= 0;
        }

        public void GoAwayReceived(int lastKnownStream, Http2Error errorCode, IByteBuffer debugData)
        {
            var oldLastStreamKnownByPeer = _localEndpoint.LastStreamKnownByPeer();
            if (oldLastStreamKnownByPeer >= 0 && oldLastStreamKnownByPeer < lastKnownStream)
            {
                ThrowHelper.ThrowConnectionError_LastStreamIdMustNotIncrease(oldLastStreamKnownByPeer, lastKnownStream);
            }
            _localEndpoint.LastStreamKnownByPeer(lastKnownStream);
            for (int i = 0; i < _listeners.Count; ++i)
            {
                try
                {
                    _listeners[i].OnGoAwayReceived(lastKnownStream, errorCode, debugData);
                }
                catch (Exception cause)
                {
                    Logger.CaughtExceptionFromListenerOnGoAwayReceived(cause);
                }
            }

            CloseStreamsGreaterThanLastKnownStreamId(lastKnownStream, _localEndpoint);
        }

        public bool GoAwaySent()
        {
            return _remoteEndpoint._lastStreamKnownByPeer >= 0;
        }

        public bool GoAwaySent(int lastKnownStream, Http2Error errorCode, IByteBuffer debugData)
        {
            var oldLastStreamKnownByPeer = _remoteEndpoint.LastStreamKnownByPeer();
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

            _remoteEndpoint.LastStreamKnownByPeer(lastKnownStream);
            for (int i = 0; i < _listeners.Count; ++i)
            {
                try
                {
                    _listeners[i].OnGoAwaySent(lastKnownStream, errorCode, debugData);
                }
                catch (Exception cause)
                {
                    Logger.CaughtExceptionFromListenerOnGoAwaySent(cause);
                }
            }

            CloseStreamsGreaterThanLastKnownStreamId(lastKnownStream, _remoteEndpoint);
            return true;
        }

        private void CloseStreamsGreaterThanLastKnownStreamId(int lastKnownStream, DefaultEndpoint endpoint)
        {
            _ = ForEachActiveStream(localVisit);
            bool localVisit(IHttp2Stream stream)
            {
                if (stream.Id > lastKnownStream && endpoint.IsValidStreamId(stream.Id))
                {
                    _ = stream.Close();
                }
                return true;
            }
        }

        /// <summary>
        /// Determine if <see cref="_streamMap"/> only contains the connection stream.
        /// </summary>
        /// <returns></returns>
        private bool IsStreamMapEmpty() => _streamMap.Count == 1;

        /// <summary>
        /// Remove a stream from the <see cref="_streamMap"/>.
        /// </summary>
        /// <param name="stream">the stream to remove.</param>
        void RemoveStream(DefaultHttp2Stream stream)
        {
            bool removed = _streamMap.Remove(stream.Id);

            if (removed)
            {
                for (int i = 0; i < _listeners.Count; i++)
                {
                    try
                    {
                        _listeners[i].OnStreamRemoved(stream);
                    }
                    catch (Exception cause)
                    {
                        Logger.CaughtExceptionFromListenerOnStreamRemoved(cause);
                    }
                }

                if (InternalClosePromise is object && IsStreamMapEmpty())
                {
                    _ = _closeFuture.TryComplete();
                }
            }
        }

        static Http2StreamState ActiveState(int streamId, Http2StreamState initialState, bool isLocal, bool halfClosed) => initialState switch
        {
            Http2StreamState.Idle => halfClosed ? isLocal ? Http2StreamState.HalfClosedLocal : Http2StreamState.HalfClosedRemote : Http2StreamState.Open,
            Http2StreamState.ReservedLocal => Http2StreamState.HalfClosedRemote,
            Http2StreamState.ReservedRemote => Http2StreamState.HalfClosedLocal,
            _ => throw ThrowHelper.GetStreamError_AttemptingToOpenAStreamInAnInvalidState(streamId, initialState),
        };

        void NotifyHalfClosed(IHttp2Stream stream)
        {
            for (int i = 0; i < _listeners.Count; i++)
            {
                try
                {
                    _listeners[i].OnStreamHalfClosed(stream);
                }
                catch (Exception cause)
                {
                    Logger.CaughtExceptionFromListenerOnStreamHalfClosed(cause);
                }
            }
        }

        void NotifyClosed(IHttp2Stream stream)
        {
            for (int i = 0; i < _listeners.Count; i++)
            {
                try
                {
                    _listeners[i].OnStreamClosed(stream);
                }
                catch (Exception cause)
                {
                    Logger.CaughtExceptionFromListenerOnStreamClosed(cause);
                }
            }
        }

        public IHttp2ConnectionPropertyKey NewKey()
        {
            return _propertyKeyRegistry.NewKey(this);
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
            const int MetaStateSentTrailers = 1 << 2;
            const int MetaStateSentPushpromise = 1 << 3;
            const int MetaStateRecvHeaders = 1 << 4;
            const int MetaStateRecvTrailers = 1 << 5;

            readonly DefaultHttp2Connection _conn;
            readonly int _id;
            readonly PropertyMap _properties;
            private Http2StreamState _state;
            private int _metaState;

            internal DefaultHttp2Stream(DefaultHttp2Connection conn, int id, Http2StreamState state)
            {
                _conn = conn;
                _id = id;
                _state = state;
                _properties = new PropertyMap(conn._propertyKeyRegistry);
            }

            public int Id => _id;

            public Http2StreamState State => _state;

            public virtual bool IsResetSent => (_metaState & MetaStateSentRst) != 0;

            public virtual IHttp2Stream ResetSent()
            {
                _metaState |= MetaStateSentRst;
                return this;
            }

            public virtual IHttp2Stream HeadersSent(bool isInformational)
            {
                if (!isInformational)
                {
                    _metaState |= IsHeadersSent ? MetaStateSentTrailers : MetaStateSentHeaders;
                }

                return this;
            }

            public virtual bool IsHeadersSent => (_metaState & MetaStateSentHeaders) != 0;

            public bool IsTrailersSent => (_metaState & MetaStateSentTrailers) != 0;

            public IHttp2Stream HeadersReceived(bool isInformational)
            {
                if (!isInformational)
                {
                    _metaState |= IsHeadersReceived ? MetaStateRecvTrailers : MetaStateRecvHeaders;
                }

                return this;
            }

            public bool IsHeadersReceived => (_metaState & MetaStateRecvHeaders) != 0;

            public bool IsTrailersReceived => (_metaState & MetaStateRecvTrailers) != 0;

            public virtual IHttp2Stream PushPromiseSent()
            {
                _metaState |= MetaStateSentPushpromise;
                return this;
            }

            public virtual bool IsPushPromiseSent => (_metaState & MetaStateSentPushpromise) != 0;

            public V SetProperty<V>(IHttp2ConnectionPropertyKey key, object value)
            {
                var prevValue = _properties.Add(_conn.VerifyKey(key), value);
                return prevValue is V v ? v : default;
            }

            public object SetProperty(IHttp2ConnectionPropertyKey key, object value)
            {
                return _properties.Add(_conn.VerifyKey(key), value);
            }

            public V GetProperty<V>(IHttp2ConnectionPropertyKey key)
            {
                return (V)_properties.Get(_conn.VerifyKey(key));
            }

            public object RemoveProperty(IHttp2ConnectionPropertyKey key)
            {
                return _properties.Remove(_conn.VerifyKey(key));
            }

            public V RemoveProperty<V>(IHttp2ConnectionPropertyKey key)
            {
                return (V)_properties.Remove(_conn.VerifyKey(key));
            }

            public virtual IHttp2Stream Open(bool halfClosed)
            {
                _state = ActiveState(_id, _state, IsLocal(), halfClosed);
                if (!CreatedBy().CanOpenStream)
                {
                    ThrowHelper.ThrowConnectionError_MaximumActiveStreamsViolatedForThisEndpoint();
                }

                Activate();
                return this;
            }

            internal void Activate()
            {
                // If the stream is opened in a half-closed state, the headers must have either
                // been sent if this is a local stream, or received if it is a remote stream.
                if (Http2StreamState.HalfClosedLocal == _state)
                {
                    _ = HeadersSent(/*isInformational*/ false);
                }
                else if (Http2StreamState.HalfClosedRemote == _state)
                {
                    _ = HeadersReceived(/*isInformational*/ false);
                }
                _conn._activeStreams.Activate(this);
            }

            public virtual IHttp2Stream Close() => Close(false);

            public virtual IHttp2Stream Close(bool force)
            {
                if (Http2StreamState.Closed == _state)
                {
                    return this;
                }

                _state = Http2StreamState.Closed;

                --CreatedBy()._numStreams;
                _conn._activeStreams.Deactivate(this, force);
                return this;
            }

            public virtual IHttp2Stream CloseLocalSide()
            {
                switch (_state)
                {
                    case Http2StreamState.Open:
                        _state = Http2StreamState.HalfClosedLocal;
                        _conn.NotifyHalfClosed(this);
                        break;

                    case Http2StreamState.HalfClosedLocal:
                        break;

                    default:
                        _ = Close();
                        break;
                }
                return this;
            }

            public virtual IHttp2Stream CloseRemoteSide()
            {
                switch (_state)
                {
                    case Http2StreamState.Open:
                        _state = Http2StreamState.HalfClosedRemote;
                        _conn.NotifyHalfClosed(this);
                        break;

                    case Http2StreamState.HalfClosedRemote:
                        break;

                    default:
                        _ = Close();
                        break;
                }
                return this;
            }

            internal virtual DefaultEndpoint CreatedBy()
            {
                return _conn._localEndpoint.IsValidStreamId(_id)
                    ? (DefaultEndpoint)_conn._localEndpoint
                    : _conn._remoteEndpoint;
            }

            bool IsLocal()
            {
                return _conn._localEndpoint.IsValidStreamId(_id);
            }

            /// <summary>
            /// Provides the lazy initialization for the <see cref="DefaultHttp2Stream"/> data map.
            /// </summary>
            sealed class PropertyMap
            {
                readonly PropertyKeyRegistry _registry;
                object[] _values = EmptyArrays.EmptyObjects;

                public PropertyMap(PropertyKeyRegistry registry)
                {
                    _registry = registry;
                }

                internal object Add(DefaultPropertyKey key, object value)
                {
                    ResizeIfNecessary(key._index);
                    object prevValue = _values[key._index];
                    _values[key._index] = value;
                    return prevValue;
                }

                internal object Get(DefaultPropertyKey key)
                {
                    var keyIndex = key._index;
                    var thisValues = _values;
                    if ((uint)keyIndex < (uint)thisValues.Length) { return thisValues[keyIndex]; }

                    return default;
                }

                internal object Remove(DefaultPropertyKey key)
                {
                    object prevValue = null;
                    var keyIndex = key._index;
                    var thisValues = _values;
                    if ((uint)keyIndex < (uint)thisValues.Length)
                    {
                        prevValue = thisValues[keyIndex];
                        thisValues[keyIndex] = null;
                    }

                    return prevValue;
                }

                void ResizeIfNecessary(int index)
                {
                    if (index >= _values.Length)
                    {
                        object[] tmp = new object[_registry.Size];
                        if ((uint)_values.Length > 0u)
                        {
                            Array.Copy(_values, tmp, Math.Min(_values.Length, tmp.Length));
                        }
                        _values = tmp;
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
            F _flowController;

            internal DefaultEndpoint(DefaultHttp2Connection conn, bool server, int maxReservedStreams)
                : base(conn, server, maxReservedStreams)
            {
            }

            public F FlowController
            {
                get => _flowController;
                set
                {
                    if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }
                    _flowController = value;
                }
            }
        }

        /// <summary>
        /// Simple endpoint implementation.
        /// </summary>
        internal class DefaultEndpoint : IHttp2ConnectionEndpoint
        {
            readonly DefaultHttp2Connection _conn;

            readonly bool _server;

            /// <summary>
            /// The minimum stream ID allowed when creating the next stream. This only applies at the time the stream is
            /// created. If the ID of the stream being created is less than this value, stream creation will fail. Upon
            /// successful creation of a stream, this value is incremented to the next valid stream ID.
            /// </summary>
            private int _nextStreamIdToCreate;

            /// <summary>
            /// Used for reservation of stream IDs. Stream IDs can be reserved in advance by applications before the streams
            /// are actually created.  For example, applications may choose to buffer stream creation attempts as a way of
            /// working around <c>SETTINGS_MAX_CONCURRENT_STREAMS</c>, in which case they will reserve stream IDs for each
            /// buffered stream.
            /// </summary>
            private int _nextReservationStreamId;
            internal int _lastStreamKnownByPeer = -1;
            private bool _pushToAllowed = true;

            private int _maxStreams;
            private int _maxActiveStreams;

            private readonly int _maxReservedStreams;

            // Fields accessed by inner classes
            internal int _numActiveStreams;
            internal int _numStreams;

            internal DefaultEndpoint(DefaultHttp2Connection conn, bool server, int maxReservedStreams)
            {
                _conn = conn;
                _server = server;

                // Determine the starting stream ID for this endpoint. Client-initiated streams
                // are odd and server-initiated streams are even. Zero is reserved for the
                // connection. Stream 1 is reserved client-initiated stream for responding to an
                // upgrade from HTTP 1.1.
                if (server)
                {
                    _nextStreamIdToCreate = 2;
                    _nextReservationStreamId = 0;
                }
                else
                {
                    _nextStreamIdToCreate = 1;
                    // For manually created client-side streams, 1 is reserved for HTTP upgrade, so start at 3.
                    _nextReservationStreamId = 1;
                }

                // Push is disallowed by default for servers and allowed for clients.
                _pushToAllowed = !server;
                _maxActiveStreams = int.MaxValue;
                if ((uint)maxReservedStreams > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(maxReservedStreams, ExceptionArgument.maxReservedStreams); }
                _maxReservedStreams = maxReservedStreams;
                UpdateMaxStreams();
            }

            public int IncrementAndGetNextStreamId =>
                _nextReservationStreamId >= 0 ? _nextReservationStreamId += 2 : _nextReservationStreamId;

            private void IncrementExpectedStreamId(int streamId)
            {
                if (streamId > _nextReservationStreamId && _nextReservationStreamId >= 0)
                {
                    _nextReservationStreamId = streamId;
                }

                _nextStreamIdToCreate = streamId + 2;
                ++_numStreams;
            }

            public bool IsValidStreamId(int streamId)
            {
                return streamId > 0 && _server == (0u >= (uint)(streamId & 1));
            }

            public bool MayHaveCreatedStream(int streamId)
            {
                return IsValidStreamId(streamId) && streamId <= LastStreamCreated;
            }

            public bool CanOpenStream => _numActiveStreams < _maxActiveStreams;

            public IHttp2Stream CreateStream(int streamId, bool halfClosed)
            {
                Http2StreamState state = ActiveState(streamId, Http2StreamState.Idle, IsLocal(), halfClosed);

                CheckNewStreamAllowed(streamId, state);

                // Create and initialize the stream.
                DefaultHttp2Stream stream = new DefaultHttp2Stream(_conn, streamId, state);

                IncrementExpectedStreamId(streamId);

                AddStream(stream);

                stream.Activate();
                return stream;
            }

            public bool Created(IHttp2Stream stream)
            {
                return stream is DefaultHttp2Stream defaultStream && ReferenceEquals(defaultStream.CreatedBy(), this);
            }

            public bool IsServer => _server;

            public IHttp2Stream ReservePushStream(int streamId, IHttp2Stream parent)
            {
                if (parent is null)
                {
                    ThrowHelper.ThrowConnectionError_ParentStreamMissing();
                }

                if (IsLocal() ? !parent.State.LocalSideOpen() : !parent.State.RemoteSideOpen())
                {
                    ThrowHelper.ThrowConnectionError_StreamIsNotOpenForSendingPushPromise(parent.Id);
                }

                if (!Opposite.AllowPushTo())
                {
                    ThrowHelper.ThrowConnectionError_ServerPushNotAllowedToOppositeEndpoint();
                }

                Http2StreamState state = IsLocal() ? Http2StreamState.ReservedLocal : Http2StreamState.ReservedRemote;
                CheckNewStreamAllowed(streamId, state);

                // Create and initialize the stream.
                DefaultHttp2Stream stream = new DefaultHttp2Stream(_conn, streamId, state);

                IncrementExpectedStreamId(streamId);

                // Register the stream.
                AddStream(stream);
                return stream;
            }

            private void AddStream(DefaultHttp2Stream stream)
            {
                // Add the stream to the map and priority tree.
                _conn._streamMap.Add(stream.Id, stream);

                // Notify the listeners of the event.
                var listeners = _conn._listeners;
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
                if (allow && _server)
                {
                    ThrowHelper.ThrowArgumentException_ServersDoNotAllowPush();
                }

                _pushToAllowed = allow;
            }

            public bool AllowPushTo() => _pushToAllowed;

            public int NumActiveStreams => _numActiveStreams;

            public int MaxActiveStreams => _maxActiveStreams;

            public void SetMaxActiveStreams(int maxActiveStreams)
            {
                _maxActiveStreams = maxActiveStreams;
                UpdateMaxStreams();
            }

            public int LastStreamCreated => _nextStreamIdToCreate > 1 ? _nextStreamIdToCreate - 2 : 0;

            public int LastStreamKnownByPeer() => _lastStreamKnownByPeer;

            public void LastStreamKnownByPeer(int lastKnownStream)
            {
                _lastStreamKnownByPeer = lastKnownStream;
            }

            public IHttp2ConnectionEndpoint Opposite =>
                IsLocal() ? (IHttp2ConnectionEndpoint)_conn._remoteEndpoint : _conn._localEndpoint;

            private void UpdateMaxStreams()
            {
                _maxStreams = (int)Math.Min(int.MaxValue, (long)_maxActiveStreams + _maxReservedStreams);
            }

            private void CheckNewStreamAllowed(int streamId, Http2StreamState state)
            {
                Debug.Assert(state != Http2StreamState.Idle);
                if (_lastStreamKnownByPeer >= 0 && streamId > _lastStreamKnownByPeer)
                {
                    ThrowHelper.ThrowStreamError_CannotCreateStreamGreaterThanLastStreamIDFromGoAway(streamId, _lastStreamKnownByPeer);
                }

                if (!IsValidStreamId(streamId))
                {
                    if (streamId < 0)
                    {
                        ThrowHelper.ThrowHttp2NoMoreStreamIdsException();
                    }

                    ThrowHelper.ThrowConnectionError_RequestStreamIsNotCorrectForConnection(streamId, _server);
                }

                // This check must be after all id validated checks, but before the max streams check because it may be
                // recoverable to some degree for handling frames which can be sent on closed streams.
                if (streamId < _nextStreamIdToCreate)
                {
                    ThrowHelper.ThrowClosedStreamError_RequestStreamIsBehindTheNextExpectedStream(streamId, _nextStreamIdToCreate);
                }

                if (_nextStreamIdToCreate <= 0)
                {
                    ThrowHelper.ThrowConnectionError_StreamIDsAreExhaustedForThisEndpoint();
                }

                bool isReserved;
                switch (state)
                {
                    case Http2StreamState.ReservedLocal:
                    case Http2StreamState.ReservedRemote:
                        isReserved = true;
                        break;

                    default:
                        isReserved = false;
                        break;
                }
                if (!isReserved && !CanOpenStream || isReserved && _numStreams >= _maxStreams)
                {
                    ThrowHelper.ThrowStreamError_MaximumActiveStreamsViolatedForThisEndpoint(streamId);
                }

                if (_conn.IsClosed())
                {
                    ThrowHelper.ThrowConnectionError_AttemptedToCreateStreamAfterConnectionWasClosed(streamId);
                }
            }

            bool IsLocal() => ReferenceEquals(this, _conn._localEndpoint);
        }

        /// <summary>
        /// Manages the list of currently active streams.  Queues any <see cref="Action"/>s that would modify the list of
        /// active streams in order to prevent modification while iterating.
        /// </summary>
        sealed class ActiveStreams
        {
            readonly DefaultHttp2Connection _conn;
            readonly List<IHttp2ConnectionListener> _listeners;
            readonly Deque<Action> _pendingEvents = new Deque<Action>();
            readonly ISet<IHttp2Stream> _streams = new HashSet<IHttp2Stream>();
            private int _pendingIterations;

            public ActiveStreams(DefaultHttp2Connection conn, List<IHttp2ConnectionListener> listeners)
            {
                _conn = conn;
                _listeners = listeners;
            }

            public int Size()
            {
                return _streams.Count;
            }

            public void Activate(DefaultHttp2Stream stream)
            {
                if (AllowModifications())
                {
                    AddToActiveStreams(stream);
                }
                else
                {
                    _pendingEvents.AddLast​(() => AddToActiveStreams(stream));
                }
            }

            internal void Deactivate(DefaultHttp2Stream stream, bool force)
            {
                if (AllowModifications() || force)
                {
                    RemoveFromActiveStreams(stream);
                }
                else
                {
                    _pendingEvents.AddLast​(() => RemoveFromActiveStreams(stream));
                }
            }

            public IHttp2Stream ForEachActiveStream(IHttp2StreamVisitor visitor)
            {
                IncrementPendingIterations();
                try
                {
                    foreach (IHttp2Stream stream in _streams)
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
                    DecrementPendingIterations();
                }
            }

            public IHttp2Stream ForEachActiveStream(Func<IHttp2Stream, bool> visitor)
            {
                IncrementPendingIterations();
                try
                {
                    foreach (IHttp2Stream stream in _streams)
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
                    DecrementPendingIterations();
                }
            }

            void AddToActiveStreams(DefaultHttp2Stream stream)
            {
                if (_streams.Add(stream))
                {
                    // Update the number of active streams initiated by the endpoint.
                    stream.CreatedBy()._numActiveStreams++;

                    for (int i = 0; i < _listeners.Count; i++)
                    {
                        try
                        {
                            _listeners[i].OnStreamActive(stream);
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
                if (_streams.Remove(stream))
                {
                    // Update the number of active streams initiated by the endpoint.
                    stream.CreatedBy()._numActiveStreams--;
                    _conn.NotifyClosed(stream);
                }

                _conn.RemoveStream(stream);
            }

            internal bool AllowModifications()
            {
                return 0u >= (uint)_pendingIterations;
            }

            internal void IncrementPendingIterations()
            {
                ++_pendingIterations;
            }

            internal void DecrementPendingIterations()
            {
                --_pendingIterations;
                if (AllowModifications())
                {
                    while (_pendingEvents.TryRemoveFirst(out Action evt))
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
            readonly DefaultHttp2Connection _conn;
            internal readonly int _index;

            internal DefaultPropertyKey(DefaultHttp2Connection conn, int index)
            {
                _conn = conn;
                _index = index;
            }

            internal DefaultPropertyKey VerifyConnection(IHttp2Connection connection)
            {
                if (connection != _conn)
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
            readonly IList<DefaultPropertyKey> _keys = new List<DefaultPropertyKey>(4);

            /// <summary>
            /// Registers a new property key.
            /// </summary>
            /// <param name="conn"></param>
            /// <returns></returns>
            internal DefaultPropertyKey NewKey(DefaultHttp2Connection conn)
            {
                var key = new DefaultPropertyKey(conn, _keys.Count);
                _keys.Add(key);
                return key;
            }

            internal int Size => _keys.Count;
        }
    }
}