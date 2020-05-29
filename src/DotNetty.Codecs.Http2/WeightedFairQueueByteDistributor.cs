// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// A <see cref="IStreamByteDistributor"/> that is sensitive to stream priority and uses
    /// <a href="https://en.wikipedia.org/wiki/Weighted_fair_queueing">Weighted Fair Queueing</a> approach for distributing
    /// bytes.
    /// <para>Inspiration for this distributor was taken from Linux's
    /// <a href="https://www.kernel.org/doc/Documentation/scheduler/sched-design-CFS.txt">Completely Fair Scheduler</a>
    /// to model the distribution of bytes to simulate an "ideal multi-tasking CPU", but in this case we are simulating
    /// an "ideal multi-tasking NIC".</para>
    /// Each write operation will use the <see cref="AllocationQuantum(int)"/> to know how many more bytes should be allocated
    /// relative to the next stream which wants to write. This is to balance fairness while also considering goodput.
    /// </summary>
    public sealed class WeightedFairQueueByteDistributor : Http2ConnectionAdapter, IStreamByteDistributor
    {
        /// <summary>
        /// The initial size of the children map is chosen to be conservative on initial memory allocations under
        /// the assumption that most streams will have a small number of children.This choice may be
        /// sub-optimal if when children are present there are many children(i.e.a web page which has many
        /// dependencies to load).
        ///
        /// Visible only for testing!
        /// </summary>
        internal static readonly int InitialChildrenMapSize = Math.Max(1, SystemPropertyUtil.GetInt("io.netty.http2.childrenMapSize", 2));

        /// <summary>
        /// FireFox currently uses 5 streams to establish QoS classes.
        /// </summary>
        private const int DefaultMaxStateOnlySize = 5;

        private readonly IHttp2ConnectionPropertyKey _stateKey;

        /// <summary>
        /// If there is no <see cref="IHttp2Stream"/> object, but we still persist priority information then this is where the state will
        /// reside.
        /// </summary>
        private readonly IDictionary<int, State> _stateOnlyMap;

        /// <summary>
        /// This queue will hold streams that are not active and provides the capability to retain priority for streams which
        /// have no <see cref="IHttp2Stream"/> object. See <see cref="StateOnlyComparator"/> for the priority comparator.
        /// </summary>
        private readonly IPriorityQueue<State> _stateOnlyRemovalQueue;
        private readonly IHttp2Connection _connection;

        private readonly State _connectionState;

        /// <summary>
        /// The minimum number of bytes that we will attempt to allocate to a stream. This is to
        /// help improve goodput on a per-stream basis.
        /// </summary>
        private int _allocationQuantum = Http2CodecUtil.DefaultMinAllocationChunk;
        private readonly int _maxStateOnlySize;

        public WeightedFairQueueByteDistributor(IHttp2Connection connection)
            : this(connection, DefaultMaxStateOnlySize)
        {
        }

        public WeightedFairQueueByteDistributor(IHttp2Connection connection, int maxStateOnlySize)
        {
            uint uMaxStateOnlySize = (uint)maxStateOnlySize;
            if (uMaxStateOnlySize > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(maxStateOnlySize, ExceptionArgument.maxStateOnlySize); }
            if (0u >= uMaxStateOnlySize)
            {
                _stateOnlyMap = EmptyDictionary<int, State>.Instance;
                _stateOnlyRemovalQueue = EmptyPriorityQueue<State>.Instance;
            }
            else
            {
                _stateOnlyMap = new Dictionary<int, State>(maxStateOnlySize);
                // +2 because we may exceed the limit by 2 if a new dependency has no associated IHttp2Stream object. We need
                // to create the State objects to put them into the dependency tree, which then impacts priority.
                _stateOnlyRemovalQueue = new PriorityQueue<State>(StateOnlyComparator.Instance, maxStateOnlySize + 2);
            }

            _maxStateOnlySize = maxStateOnlySize;

            _connection = connection;
            _stateKey = connection.NewKey();
            IHttp2Stream connectionStream = connection.ConnectionStream;
            connectionStream.SetProperty(_stateKey, _connectionState = new State(this, connectionStream, 16));

            // Register for notification of new streams.
            connection.AddListener(this);
        }

        public override void OnStreamAdded(IHttp2Stream stream)
        {
            int streamId = stream.Id;
            if (!_stateOnlyMap.TryGetValue(streamId, out State state))
            {
                state = new State(this, stream);
                // Only the stream which was just added will change parents. So we only need an array of size 1.
                List<ParentChangedEvent> events = new List<ParentChangedEvent>(1);
                _connectionState.TakeChild(state, false, events);
                NotifyParentChanged(events);
            }
            else
            {
                _stateOnlyMap.Remove(streamId);
                _stateOnlyRemovalQueue.TryRemove(state);
                state._stream = stream;
            }

            Http2StreamState streamState = stream.State;
            if (Http2StreamState.ReservedRemote == streamState || Http2StreamState.ReservedLocal == streamState)
            {
                state.SetStreamReservedOrActivated();
                // wasStreamReservedOrActivated is part of the comparator for stateOnlyRemovalQueue there is no
                // need to reprioritize here because it will not be in stateOnlyRemovalQueue.
            }

            stream.SetProperty(_stateKey, state);
        }

        public override void OnStreamActive(IHttp2Stream stream)
        {
            GetState(stream).SetStreamReservedOrActivated();
            // wasStreamReservedOrActivated is part of the comparator for stateOnlyRemovalQueue there is no need to
            // reprioritize here because it will not be in stateOnlyRemovalQueue.
        }

        public override void OnStreamClosed(IHttp2Stream stream)
        {
            GetState(stream).Close();
        }

        public override void OnStreamRemoved(IHttp2Stream stream)
        {
            // The stream has been removed from the connection. We can no longer rely on the stream's property
            // storage to track the State. If we have room, and the precedence of the stream is sufficient, we
            // should retain the State in the stateOnlyMap.
            State state = GetState(stream);

            // Typically the stream is set to null when the stream is closed because it is no longer needed to write
            // data. However if the stream was not activated it may not be closed (reserved streams) so we ensure
            // the stream reference is set to null to avoid retaining a reference longer than necessary.
            state._stream = null;

            if (0u >= (uint)_maxStateOnlySize)
            {
                state._parent.RemoveChild(state);
                return;
            }

            if (_stateOnlyRemovalQueue.Count == _maxStateOnlySize)
            {
                _stateOnlyRemovalQueue.TryPeek(out State stateToRemove);
                if (StateOnlyComparator.Instance.Compare(stateToRemove, state) >= 0)
                {
                    // The "lowest priority" stream is a "higher priority" than the stream being removed, so we
                    // just discard the state.
                    state._parent.RemoveChild(state);
                    return;
                }

                _stateOnlyRemovalQueue.TryDequeue(out State _);
                stateToRemove._parent.RemoveChild(stateToRemove);
                _stateOnlyMap.Remove(stateToRemove._streamId);
            }

            _stateOnlyRemovalQueue.TryEnqueue(state);
            _stateOnlyMap.Add(state._streamId, state);
        }

        public void UpdateStreamableBytes(IStreamByteDistributorStreamState state)
        {
            GetState(state.Stream).UpdateStreamableBytes(
                Http2CodecUtil.StreamableBytes(state),
                state.HasFrame && state.WindowSize >= 0);
        }

        public void UpdateDependencyTree(int childStreamId, int parentStreamId, short weight, bool exclusive)
        {
            State state = GetState(childStreamId);
            if (state is null)
            {
                // If there is no State object that means there is no IHttp2Stream object and we would have to keep the
                // State object in the stateOnlyMap and stateOnlyRemovalQueue. However if maxStateOnlySize is 0 this means
                // stateOnlyMap and stateOnlyRemovalQueue are empty collections and cannot be modified so we drop the State.
                if (0u >= (uint)_maxStateOnlySize) { return; }

                state = new State(this, childStreamId);
                _stateOnlyRemovalQueue.TryEnqueue(state);
                _stateOnlyMap.Add(childStreamId, state);
            }

            State newParent = GetState(parentStreamId);
            if (newParent is null)
            {
                // If there is no State object that means there is no IHttp2Stream object and we would have to keep the
                // State object in the stateOnlyMap and stateOnlyRemovalQueue. However if maxStateOnlySize is 0 this means
                // stateOnlyMap and stateOnlyRemovalQueue are empty collections and cannot be modified so we drop the State.
                if (0u >= (uint)_maxStateOnlySize) { return; }

                newParent = new State(this, parentStreamId);
                _stateOnlyRemovalQueue.TryEnqueue(newParent);
                _stateOnlyMap.Add(parentStreamId, newParent);
                // Only the stream which was just added will change parents. So we only need an array of size 1.
                List<ParentChangedEvent> events = new List<ParentChangedEvent>(1);
                _connectionState.TakeChild(newParent, false, events);
                NotifyParentChanged(events);
            }

            // if activeCountForTree == 0 then it will not be in its parent's pseudoTimeQueue and thus should not be counted
            // toward parent.totalQueuedWeights.
            if (state._activeCountForTree != 0 && state._parent is object)
            {
                state._parent._totalQueuedWeights += weight - state._weight;
            }

            state._weight = weight;

            if (newParent != state._parent || (exclusive && newParent._children.Count != 1))
            {
                List<ParentChangedEvent> events;
                if (newParent.IsDescendantOf(state))
                {
                    events = new List<ParentChangedEvent>(2 + (exclusive ? newParent._children.Count : 0));
                    state._parent.TakeChild(newParent, false, events);
                }
                else
                {
                    events = new List<ParentChangedEvent>(1 + (exclusive ? newParent._children.Count : 0));
                }

                newParent.TakeChild(state, exclusive, events);
                NotifyParentChanged(events);
            }

            // The location in the dependency tree impacts the priority in the stateOnlyRemovalQueue map. If we created new
            // State objects we must check if we exceeded the limit after we insert into the dependency tree to ensure the
            // stateOnlyRemovalQueue has been updated.
            while ((uint)_stateOnlyRemovalQueue.Count > (uint)_maxStateOnlySize)
            {
                _stateOnlyRemovalQueue.TryDequeue(out State stateToRemove);
                stateToRemove._parent.RemoveChild(stateToRemove);
                _stateOnlyMap.Remove(stateToRemove._streamId);
            }
        }

        public bool Distribute(int maxBytes, Action<IHttp2Stream, int> writer) => Distribute(maxBytes, new ActionStreamByteDistributorWriter(writer));
        public bool Distribute(int maxBytes, IStreamByteDistributorWriter writer)
        {
            // As long as there is some active frame we should write at least 1 time.
            if (0u >= (uint)_connectionState._activeCountForTree) { return false; }

            // The goal is to write until we write all the allocated bytes or are no longer making progress.
            // We still attempt to write even after the number of allocated bytes has been exhausted to allow empty frames
            // to be sent. Making progress means the active streams rooted at the connection stream has changed.
            int oldIsActiveCountForTree;
            do
            {
                oldIsActiveCountForTree = _connectionState._activeCountForTree;
                // connectionState will never be active, so go right to its children.
                maxBytes -= DistributeToChildren(maxBytes, writer, _connectionState);
            }
            while (_connectionState._activeCountForTree != 0 &&
                  (maxBytes > 0 || oldIsActiveCountForTree != _connectionState._activeCountForTree));

            return _connectionState._activeCountForTree != 0;
        }

        /// <summary>
        /// Sets the amount of bytes that will be allocated to each stream. Defaults to 1KiB.
        /// </summary>
        /// <param name="allocationQuantum">the amount of bytes that will be allocated to each stream. Must be &gt; 0.</param>
        public void AllocationQuantum(int allocationQuantum)
        {
            if ((uint)(allocationQuantum - 1) > SharedConstants.TooBigOrNegative)
            {
                ThrowHelper.ThrowArgumentException_Positive(allocationQuantum, ExceptionArgument.allocationQuantum);
            }

            _allocationQuantum = allocationQuantum;
        }

        int Distribute(int maxBytes, IStreamByteDistributorWriter writer, State state)
        {
            if (state.IsActive())
            {
                int nsent = Math.Min(maxBytes, state._streamableBytes);
                state.Write(nsent, writer);
                if (0u >= (uint)nsent && maxBytes != 0)
                {
                    // If a stream sends zero bytes, then we gave it a chance to write empty frames and it is now
                    // considered inactive until the next call to updateStreamableBytes. This allows descendant streams to
                    // be allocated bytes when the parent stream can't utilize them. This may be as a result of the
                    // stream's flow control window being 0.
                    state.UpdateStreamableBytes(state._streamableBytes, false);
                }

                return nsent;
            }

            return DistributeToChildren(maxBytes, writer, state);
        }

        /// <summary>
        /// It is a pre-condition that <see cref="State.PollPseudoTimeQueue"/> returns a non-<c>null</c> value. This is a result of the way
        /// the allocation algorithm is structured and can be explained in the following cases:
        /// <h3>For the recursive case</h3>
        /// If a stream has no children (in the allocation tree) than that node must be active or it will not be in the
        /// allocation tree. If a node is active then it will not delegate to children and recursion ends.
        /// <h3>For the initial case</h3>
        /// We check connectionState.activeCountForTree == 0 before any allocation is done. So if the connection stream
        /// has no active children we don't get into this method.
        /// </summary>
        /// <param name="maxBytes"></param>
        /// <param name="writer"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        int DistributeToChildren(int maxBytes, IStreamByteDistributorWriter writer, State state)
        {
            long oldTotalQueuedWeights = state._totalQueuedWeights;
            State childState = state.PollPseudoTimeQueue();
            State nextChildState = state.PeekPseudoTimeQueue();
            childState.SetDistributing();
            try
            {
                Debug.Assert(
                    nextChildState is null || nextChildState._pseudoTimeToWrite >= childState._pseudoTimeToWrite,
                    $"nextChildState[{nextChildState?._streamId}].pseudoTime({nextChildState?._pseudoTimeToWrite}) <  childState[{childState._streamId}].pseudoTime({childState._pseudoTimeToWrite})");

                int nsent = Distribute(
                    nextChildState is null
                        ? maxBytes
                        : Math.Min(
                            maxBytes,
                            (int)Math.Min(
                                (nextChildState._pseudoTimeToWrite - childState._pseudoTimeToWrite) * childState._weight / oldTotalQueuedWeights + _allocationQuantum,
                                int.MaxValue
                            )
                        ),
                    writer,
                    childState);
                state._pseudoTime += nsent;
                childState.UpdatePseudoTime(state, nsent, oldTotalQueuedWeights);
                return nsent;
            }
            finally
            {
                childState.UnsetDistributing();
                // Do in finally to ensure the internal flags is not corrupted if an exception is thrown.
                // The offer operation is delayed until we unroll up the recursive stack, so we don't have to remove from
                // the priority pseudoTimeQueue due to a write operation.
                if (childState._activeCountForTree != 0)
                {
                    state.OfferPseudoTimeQueue(childState);
                }
            }
        }

        State GetState(IHttp2Stream stream)
        {
            return stream.GetProperty<State>(_stateKey);
        }

        State GetState(int streamId)
        {
            IHttp2Stream stream = _connection.Stream(streamId);

            return stream is object ? GetState(stream) : (_stateOnlyMap.TryGetValue(streamId, out State state) ? state : null);
        }

        /// <summary>
        /// For testing only!
        /// </summary>
        /// <param name="childId"></param>
        /// <param name="parentId"></param>
        /// <param name="weight"></param>
        /// <returns></returns>
        internal bool IsChild(int childId, int parentId, short weight)
        {
            State parent = GetState(parentId);
            State child;
            return parent._children.ContainsKey(childId) &&
                (child = GetState(childId))._parent == parent && child._weight == weight;
        }

        /// <summary>
        /// For testing only!
        /// </summary>
        /// <param name="streamId"></param>
        /// <returns></returns>
        internal int NumChildren(int streamId)
        {
            State state = GetState(streamId);
            return state is null ? 0 : state._children.Count;
        }

        /// <summary>
        /// Notify all listeners of the priority tree change events (in ascending order)
        /// </summary>
        /// <param name="events">The events (top down order) which have changed</param>
        void NotifyParentChanged(List<ParentChangedEvent> events)
        {
            for (int i = 0; i < events.Count; ++i)
            {
                ParentChangedEvent evt = events[i];
                var evtState = evt._state;
                _stateOnlyRemovalQueue.PriorityChanged(evtState);
                var evtStateParent = evtState._parent;
                if (evtStateParent is object && evtState._activeCountForTree != 0)
                {
                    evtStateParent.OfferAndInitializePseudoTime(evtState);
                    evtStateParent.ActiveCountChangeForTree(evtState._activeCountForTree);
                }
            }
        }

        /// <summary>
        /// A comparator for <see cref="State"/> which has no associated <see cref="IHttp2Stream"/> object. The general precedence is:
        /// <ul>
        ///     <li>Was a stream activated or reserved (streams only used for priority are higher priority)</li>
        ///     <li>Depth in the priority tree (closer to root is higher priority></li>
        ///     <li>Stream ID (higher stream ID is higher priority - used for tie breaker)</li>
        /// </ul>
        /// </summary>
        sealed class StateOnlyComparator : IComparer<State>
        {
            internal static readonly StateOnlyComparator Instance = new StateOnlyComparator();

            private StateOnlyComparator() { }

            public int Compare(State o1, State o2)
            {
                // "priority only streams" (which have not been activated) are higher priority than streams used for data.
                bool o1Actived = o1.WasStreamReservedOrActivated();

                if (o1Actived != o2.WasStreamReservedOrActivated())
                {
                    return o1Actived ? -1 : 1;
                }

                // Numerically greater depth is higher priority.
                int x = o2._dependencyTreeDepth - o1._dependencyTreeDepth;

                // I also considered tracking the number of streams which are "activated" (eligible transfer data) at each
                // subtree. This would require a traversal from each node to the root on dependency tree structural changes,
                // and then it would require a re-prioritization at each of these nodes (instead of just the nodes where the
                // direct parent changed). The costs of this are judged to be relatively high compared to the nominal
                // benefit it provides to the heuristic. Instead folks should just increase maxStateOnlySize.

                // Last resort is to give larger stream ids more priority.
                return x != 0 ? x : o1._streamId - o2._streamId;
            }
        }

        sealed class StatePseudoTimeComparator : IComparer<State>
        {
            internal static readonly StatePseudoTimeComparator Instance = new StatePseudoTimeComparator();

            private StatePseudoTimeComparator() { }

            public int Compare(State o1, State o2)
            {
                return MathUtil.Compare(o1._pseudoTimeToWrite, o2._pseudoTimeToWrite);
            }
        }

        /// <summary>
        /// The remote flow control state for a single stream.
        /// </summary>
        sealed class State : IPriorityQueueNode<State>
        {
            private const int IndexNotInQueue = -1;

            private const byte StateIsActive = 0x1;
            private const byte StateIsDistributing = 0x2;
            private const byte StateStreamActivated = 0x4;

            /// <summary>
            /// Maybe <c>null</c> if the stream if the stream is not active.
            /// </summary>
            internal IHttp2Stream _stream;
            internal State _parent;

            internal IDictionary<int, State> _children = EmptyDictionary<int, State>.Instance;

            readonly IPriorityQueue<State> _pseudoTimeQueue;
            internal readonly int _streamId;
            internal int _streamableBytes;

            internal int _dependencyTreeDepth;

            /// <summary>
            /// Count of nodes rooted at this sub tree with <see cref="IsActive"/> equal to <c>true</c>.
            /// </summary>
            internal int _activeCountForTree;
            private int _pseudoTimeQueueIndex = IndexNotInQueue;

            private int _stateOnlyQueueIndex = IndexNotInQueue;

            /// <summary>
            /// An estimate of when this node should be given the opportunity to write data.
            /// </summary>
            internal long _pseudoTimeToWrite;

            /// <summary>
            /// A pseudo time maintained for immediate children to base their <see cref="_pseudoTimeToWrite"/> off of.
            /// </summary>
            internal long _pseudoTime;
            internal long _totalQueuedWeights;
            private int _flags;
            internal short _weight = Http2CodecUtil.DefaultPriorityWeight;

            private readonly WeightedFairQueueByteDistributor _distributor;

            internal State(WeightedFairQueueByteDistributor distributor, int streamId)
                : this(distributor, streamId, null, 0)
            {
            }

            internal State(WeightedFairQueueByteDistributor distributor, IHttp2Stream stream)
                : this(distributor, stream, 0)
            {
            }

            internal State(WeightedFairQueueByteDistributor distributor, IHttp2Stream stream, int initialSize)
                : this(distributor, stream.Id, stream, initialSize)
            {
            }

            internal State(WeightedFairQueueByteDistributor distributor, int streamId, IHttp2Stream stream, int initialSize)
            {
                _distributor = distributor;
                _stream = stream;
                _streamId = streamId;
                _pseudoTimeQueue = new PriorityQueue<State>(StatePseudoTimeComparator.Instance, initialSize);
            }

            internal bool IsDescendantOf(State state)
            {
                State next = _parent;
                while (next is object)
                {
                    if (next == state) { return true; }

                    next = next._parent;
                }

                return false;
            }

            internal void TakeChild(State child, bool exclusive, List<ParentChangedEvent> events)
            {
                TakeChild(null, child, exclusive, events);
            }

            /// <summary>
            /// Adds a child to this priority. If exclusive is set, any children of this node are moved to being dependent on
            /// the child.
            /// </summary>
            /// <param name="childItr"></param>
            /// <param name="child"></param>
            /// <param name="exclusive"></param>
            /// <param name="events"></param>
            void TakeChild(IDictionary<int, State> childItr, State child, bool exclusive, List<ParentChangedEvent> events)
            {
                State oldParent = child._parent;

                if (oldParent != this)
                {
                    events.Add(new ParentChangedEvent(child, oldParent));
                    child.SetParent(this);

                    // If the childItr is not null we are iterating over the oldParent.children collection and should
                    // use the iterator to remove from the collection to avoid concurrent modification. Otherwise it is
                    // assumed we are not iterating over this collection and it is safe to call remove directly.
                    if (childItr is object)
                    {
                        //childItr.Remove();
                    }
                    else if (oldParent is object)
                    {
                        oldParent._children.Remove(child._streamId);
                    }

                    // Lazily initialize the children to save object allocations.
                    InitChildrenIfEmpty();

                    _children.Add(child._streamId, child);
                    //Debug.Assert(added, "A stream with the same stream ID was already in the child map.");
                }

                if (exclusive && (uint)_children.Count > 0u)
                {
                    // If it was requested that this child be the exclusive dependency of this node,
                    // move any previous children to the child node, becoming grand children of this node.
                    IDictionary<int, State> prevChildren = RemoveAllChildrenExcept(child);
                    /*IEnumerator<KeyValuePair<int, State>> itr = prevChildren.GetEnumerator();
                    while (itr.MoveNext())
                    {
                        child.takeChild(itr, itr.Current.Value, false, events);
                    }*/

                    foreach (var item in prevChildren)
                    {
                        child.TakeChild(prevChildren, item.Value, false, events);
                    }
                    prevChildren.Clear();
                }
            }

            /// <summary>
            /// Removes the child priority and moves any of its dependencies to being direct dependencies on this node.
            /// </summary>
            /// <param name="child"></param>
            internal void RemoveChild(State child)
            {
                if (_children.Remove(child._streamId))
                {
                    IDictionary<int, State> grandChildren = child._children;
                    List<ParentChangedEvent> events = new List<ParentChangedEvent>(1 + grandChildren.Count)
                    {
                        new ParentChangedEvent(child, child._parent)
                    };
                    child.SetParent(null);

                    // Move up any grand children to be directly dependent on this node.
                    /*IEnumerator<KeyValuePair<int, State>> itr = grandChildren.GetEnumerator();
                    while (itr.MoveNext())
                    {
                        takeChild(itr, itr.Current.Value, false, events);
                    }*/

                    foreach (var item in grandChildren)
                    {
                        TakeChild(grandChildren, item.Value, false, events);
                    }
                    grandChildren.Clear();

                    _distributor.NotifyParentChanged(events);
                }
            }

            /// <summary>
            /// Remove all children with the exception of <paramref name="stateToRetain"/>.
            /// This method is intended to be used to support an exclusive priority dependency operation.
            /// </summary>
            /// <param name="stateToRetain"></param>
            /// <returns>The map of children prior to this operation, excluding <paramref name="stateToRetain"/> if present.</returns>
            IDictionary<int, State> RemoveAllChildrenExcept(State stateToRetain)
            {
                bool removed = _children.TryGetValue(stateToRetain._streamId, out stateToRetain) && _children.Remove(stateToRetain._streamId);
                IDictionary<int, State> prevChildren = _children;
                // This map should be re-initialized in anticipation for the 1 exclusive child which will be added.
                // It will either be added directly in this method, or after this method is called...but it will be added.
                InitChildren();
                if (removed)
                {
                    _children.Add(stateToRetain._streamId, stateToRetain);
                }

                return prevChildren;
            }

            void SetParent(State newParent)
            {
                // if activeCountForTree == 0 then it will not be in its parent's pseudoTimeQueue.
                if (_activeCountForTree != 0 && _parent is object)
                {
                    _parent.RemovePseudoTimeQueue(this);
                    _parent.ActiveCountChangeForTree(-1 * _activeCountForTree);
                }

                _parent = newParent;
                // Use MAX_VALUE if no parent because lower depth is considered higher priority by StateOnlyComparator.
                _dependencyTreeDepth = newParent is null ? int.MaxValue : newParent._dependencyTreeDepth + 1;
            }

            void InitChildrenIfEmpty()
            {
                if (_children == EmptyDictionary<int, State>.Instance)
                {
                    InitChildren();
                }
            }

            void InitChildren()
            {
                //children = new ConcurrentDictionary<int, State>(WeightedFairQueueByteDistributor.INITIAL_CHILDREN_MAP_SIZE);
                _children = new Dictionary<int, State>(InitialChildrenMapSize);
            }

            internal void Write(int numBytes, IStreamByteDistributorWriter writer)
            {
                Debug.Assert(_stream is object);
                try
                {
                    writer.Write(_stream, numBytes);
                }
                catch (Exception t)
                {
                    ThrowHelper.ThrowConnectionError_ByteDistributionWriteError(t);
                }
            }

            internal void ActiveCountChangeForTree(int increment)
            {
                Debug.Assert(_activeCountForTree + increment >= 0);
                _activeCountForTree += increment;
                if (_parent is object)
                {
                    Debug.Assert(
                        _activeCountForTree != increment || _pseudoTimeQueueIndex == IndexNotInQueue || _parent._pseudoTimeQueue.Contains(this),
                        $"State[{_streamId}].activeCountForTree changed from 0 to {increment} is in a pseudoTimeQueue, but not in parent[{_parent._streamId}]'s pseudoTimeQueue");
                    if (0u >= (uint)_activeCountForTree)
                    {
                        _parent.RemovePseudoTimeQueue(this);
                    }
                    else if (_activeCountForTree == increment && !IsDistributing())
                    {
                        // If frame count was 0 but is now not, and this node is not already in a pseudoTimeQueue (assumed
                        // to be pState's pseudoTimeQueue) then enqueue it. If this State object is being processed the
                        // pseudoTime for this node should not be adjusted, and the node will be added back to the
                        // pseudoTimeQueue/tree structure after it is done being processed. This may happen if the
                        // activeCountForTree == 0 (a node which can't stream anything and is blocked) is at/near root of
                        // the tree, and is popped off the pseudoTimeQueue during processing, and then put back on the
                        // pseudoTimeQueue because a child changes position in the priority tree (or is closed because it is
                        // not blocked and finished writing all data).
                        _parent.OfferAndInitializePseudoTime(this);
                    }

                    _parent.ActiveCountChangeForTree(increment);
                }
            }

            internal void UpdateStreamableBytes(int newStreamableBytes, bool isActive)
            {
                if (IsActive() != isActive)
                {
                    if (isActive)
                    {
                        ActiveCountChangeForTree(1);
                        SetActive();
                    }
                    else
                    {
                        ActiveCountChangeForTree(-1);
                        UnsetActive();
                    }
                }

                _streamableBytes = newStreamableBytes;
            }

            /// <summary>
            /// Assumes the parents <paramref name="totalQueuedWeights"/> includes this node's weight.
            /// </summary>
            /// <param name="parentState"></param>
            /// <param name="nsent"></param>
            /// <param name="totalQueuedWeights"></param>
            internal void UpdatePseudoTime(State parentState, int nsent, long totalQueuedWeights)
            {
                Debug.Assert(_streamId != Http2CodecUtil.ConnectionStreamId && nsent >= 0);
                // If the current pseudoTimeToSend is greater than parentState.pseudoTime then we previously over accounted
                // and should use parentState.pseudoTime.
                _pseudoTimeToWrite = Math.Min(_pseudoTimeToWrite, parentState._pseudoTime) + nsent * totalQueuedWeights / _weight;
            }

            /// <summary>
            /// The concept of pseudoTime can be influenced by priority tree manipulations or if a stream goes from "active"
            /// to "non-active". This method accounts for that by initializing the <see cref="_pseudoTimeToWrite"/>  for
            /// <paramref name="state"/> to <see cref="_pseudoTime"/> of this node and then calls <see cref="OfferPseudoTimeQueue(State)"/>.
            /// </summary>
            /// <param name="state"></param>
            internal void OfferAndInitializePseudoTime(State state)
            {
                state._pseudoTimeToWrite = _pseudoTime;
                OfferPseudoTimeQueue(state);
            }

            internal void OfferPseudoTimeQueue(State state)
            {
                _pseudoTimeQueue.TryEnqueue(state);
                _totalQueuedWeights += state._weight;
            }

            /// <summary>
            /// Must only be called if the pseudoTimeQueue is non-empty!
            /// </summary>
            /// <returns></returns>
            internal State PollPseudoTimeQueue()
            {
                _pseudoTimeQueue.TryDequeue(out State state);
                // This method is only ever called if the pseudoTimeQueue is non-empty.
                _totalQueuedWeights -= state._weight;
                return state;
            }

            void RemovePseudoTimeQueue(State state)
            {
                if (_pseudoTimeQueue.TryRemove(state))
                {
                    _totalQueuedWeights -= state._weight;
                }
            }

            internal State PeekPseudoTimeQueue()
            {
                return _pseudoTimeQueue.TryPeek(out State result) ? result : null;
            }

            internal void Close()
            {
                UpdateStreamableBytes(0, false);
                _stream = null;
            }

            internal bool WasStreamReservedOrActivated()
            {
                return (_flags & StateStreamActivated) != 0;
            }

            internal void SetStreamReservedOrActivated()
            {
                _flags |= StateStreamActivated;
            }

            internal bool IsActive()
            {
                return (_flags & StateIsActive) != 0;
            }

            void SetActive()
            {
                _flags |= StateIsActive;
            }

            void UnsetActive()
            {
                _flags &= ~StateIsActive;
            }

            bool IsDistributing()
            {
                return (_flags & StateIsDistributing) != 0;
            }

            internal void SetDistributing()
            {
                _flags |= StateIsDistributing;
            }

            internal void UnsetDistributing()
            {
                _flags &= ~StateIsDistributing;
            }

            public int GetPriorityQueueIndex(IPriorityQueue<State> queue)
            {
                return queue == _distributor._stateOnlyRemovalQueue ? _stateOnlyQueueIndex : _pseudoTimeQueueIndex;
            }

            public void SetPriorityQueueIndex(IPriorityQueue<State> queue, int i)
            {
                if (queue == _distributor._stateOnlyRemovalQueue)
                {
                    _stateOnlyQueueIndex = i;
                }
                else
                {
                    _pseudoTimeQueueIndex = i;
                }
            }

            public override string ToString()
            {
                // Use activeCountForTree as a rough estimate for how many nodes are in this subtree.
                StringBuilder sb = new StringBuilder(256 * (_activeCountForTree > 0 ? _activeCountForTree : 1));
                ToString(sb);
                return sb.ToString();
            }

            void ToString(StringBuilder sb)
            {
                sb.Append("{streamId ").Append(_streamId)
                  .Append(" streamableBytes ").Append(_streamableBytes)
                  .Append(" activeCountForTree ").Append(_activeCountForTree)
                  .Append(" pseudoTimeQueueIndex ").Append(_pseudoTimeQueueIndex)
                  .Append(" pseudoTimeToWrite ").Append(_pseudoTimeToWrite)
                  .Append(" pseudoTime ").Append(_pseudoTime)
                  .Append(" flags ").Append(_flags)
                  .Append(" pseudoTimeQueue.Count ").Append(_pseudoTimeQueue.Count)
                  .Append(" stateOnlyQueueIndex ").Append(_stateOnlyQueueIndex)
                  .Append(" parent.streamId ").Append(_parent is null ? -1 : _parent._streamId).Append("} [");

                if ((uint)_pseudoTimeQueue.Count > 0u)
                {
                    foreach (var s in _pseudoTimeQueue)
                    {
                        s.ToString(sb);
                        sb.Append(", ");
                    }

                    // Remove the last ", "
                    sb.Length -= 2;
                }

                sb.Append(']');
            }
        }

        /// <summary>
        /// Allows a correlation to be made between a stream and its old parent before a parent change occurs.
        /// </summary>
        sealed class ParentChangedEvent
        {
            internal readonly State _state;
            private readonly State _oldParent;

            /// <summary>
            /// Create a new instance.
            /// </summary>
            /// <param name="state">The state who has had a parent change.</param>
            /// <param name="oldParent">The previous parent.</param>
            internal ParentChangedEvent(State state, State oldParent)
            {
                _state = state;
                _oldParent = oldParent;
            }
        }
    }
}