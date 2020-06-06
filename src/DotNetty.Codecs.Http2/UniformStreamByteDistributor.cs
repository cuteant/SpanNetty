// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics;
    using DotNetty.Common.Internal;

    /// <summary>
    /// A <see cref="IStreamByteDistributor"/> that ignores stream priority and uniformly allocates bytes to all
    /// streams. This class uses a minimum chunk size that will be allocated to each stream. While
    /// fewer streams may be written to in each call to <see cref="Distribute(int, IStreamByteDistributorWriter)"/>, doing this
    /// should improve the goodput on each written stream.
    /// </summary>
    public sealed class UniformStreamByteDistributor : Http2ConnectionAdapter, IStreamByteDistributor
    {
        private readonly IHttp2ConnectionPropertyKey _stateKey;
        private readonly Deque<State> _queue = new Deque<State>(4);

        /// <summary>
        /// The minimum number of bytes that we will attempt to allocate to a stream. This is to
        /// help improve goodput on a per-stream basis.
        /// </summary>
        private int _minAllocationChunk = Http2CodecUtil.DefaultMinAllocationChunk;
        private long _totalStreamableBytes;

        public UniformStreamByteDistributor(IHttp2Connection connection)
        {
            // Add a state for the connection.
            _stateKey = connection.NewKey();
            var connectionStream = connection.ConnectionStream;
            connectionStream.SetProperty(_stateKey, new State(this, connectionStream));

            // Register for notification of new streams.
            connection.AddListener(this);
        }

        public override void OnStreamAdded(IHttp2Stream stream)
        {
            stream.SetProperty(_stateKey, new State(this, stream));
        }

        public override void OnStreamRemoved(IHttp2Stream stream)
        {
            GetState(stream).Close();
        }

        /// <summary>
        /// Sets the minimum allocation chunk that will be allocated to each stream. Defaults to 1KiB.
        /// </summary>
        /// <param name="minAllocationChunk">the minimum number of bytes that will be allocated to each stream.
        /// Must be > 0.</param>
        public void MinAllocationChunk(int minAllocationChunk)
        {
            if ((uint)(minAllocationChunk - 1) > SharedConstants.TooBigOrNegative) // <= 0
            {
                ThrowHelper.ThrowArgumentException_Positive(minAllocationChunk, ExceptionArgument.minAllocationChunk);
            }
            _minAllocationChunk = minAllocationChunk;
        }

        public void UpdateStreamableBytes(IStreamByteDistributorStreamState streamState)
        {
            GetState(streamState.Stream).UpdateStreamableBytes(Http2CodecUtil.StreamableBytes(streamState),
                                                               streamState.HasFrame,
                                                               streamState.WindowSize);
        }

        public void UpdateDependencyTree(int childStreamId, int parentStreamId, short weight, bool exclusive)
        {
            // This class ignores priority and dependency!
        }

        public bool Distribute(int maxBytes, Action<IHttp2Stream, int> writer)
        {
            throw new NotImplementedException();
        }

        public bool Distribute(int maxBytes, IStreamByteDistributorWriter writer)
        {
            var size = _queue.Count;
            if (0u >= (uint)size)
            {
                return _totalStreamableBytes > 0L;
            }

            var chunkSize = Math.Max(_minAllocationChunk, maxBytes / size);

            State state = _queue.RemoveFromFront();
            do
            {
                state._enqueued = false;
                if (state._windowNegative)
                {
                    continue;
                }
                if (0u >= (uint)maxBytes && state._streamableBytes > 0)
                {
                    // Stop at the first state that can't send. Add this state back to the head of the queue. Note
                    // that empty frames at the head of the queue will always be written, assuming the stream window
                    // is not negative.
                    _queue.AddToFront(state);
                    state._enqueued = true;
                    break;
                }

                // Allocate as much data as we can for this stream.
                int chunk = Math.Min(chunkSize, Math.Min(maxBytes, state._streamableBytes));
                maxBytes -= chunk;

                // Write the allocated bytes and enqueue as necessary.
                state.Write(chunk, writer);
            } while (_queue.TryRemoveFromFront(out state));

            return _totalStreamableBytes > 0L;
        }

        private State GetState(IHttp2Stream stream)
        {
            if (stream is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.stream); }
            return stream.GetProperty<State>(_stateKey);
        }

        /// <summary>
        /// The remote flow control state for a single stream.
        /// </summary>
        private sealed class State
        {
            readonly UniformStreamByteDistributor _distributor;
            readonly IHttp2Stream _stream;
            internal int _streamableBytes;
            internal bool _windowNegative;
            internal bool _enqueued;
            bool _writing;

            public State(UniformStreamByteDistributor distributor, IHttp2Stream stream)
            {
                _distributor = distributor;
                _stream = stream;
            }

            public void UpdateStreamableBytes(int newStreamableBytes, bool hasFrame, int windowSize)
            {
                Debug.Assert(hasFrame || newStreamableBytes == 0, "hasFrame: " + hasFrame + " newStreamableBytes: " + newStreamableBytes);

                int delta = newStreamableBytes - _streamableBytes;
                if (delta != 0)
                {
                    _streamableBytes = newStreamableBytes;
                    _distributor._totalStreamableBytes += delta;
                }
                // In addition to only enqueuing state when they have frames we enforce the following restrictions:
                // 1. If the window has gone negative. We never want to queue a state. However we also don't want to
                //    Immediately remove the item if it is already queued because removal from deque is O(n). So
                //    we allow it to stay queued and rely on the distribution loop to remove this state.
                // 2. If the window is zero we only want to queue if we are not writing. If we are writing that means
                //    we gave the state a chance to write zero length frames. We wait until updateStreamableBytes is
                //    called again before this state is allowed to write.
                _windowNegative = windowSize < 0;
                if (hasFrame && (windowSize > 0 || (0u >= (uint)windowSize && !_writing)))
                {
                    AddToQueue();
                }
            }

            /// <summary>
            /// Write any allocated bytes for the given stream and updates the streamable bytes,
            /// assuming all of the bytes will be written.
            /// </summary>
            /// <param name="numBytes"></param>
            /// <param name="writer"></param>
            public void Write(int numBytes, IStreamByteDistributorWriter writer)
            {
                _writing = true;
                try
                {
                    // Write the allocated bytes.
                    writer.Write(_stream, numBytes);
                }
                catch (Exception t)
                {
                    ThrowHelper.ThrowConnectionError_ByteDistributionWriteError(t);
                }
                finally
                {
                    _writing = false;
                }
            }

            void AddToQueue()
            {
                if (!_enqueued)
                {
                    _enqueued = true;
                    _distributor._queue.AddToBack(this);
                }
            }

            void RemoveFromQueue()
            {
                if (_enqueued)
                {
                    _enqueued = false;
                    _distributor._queue.Remove(this);
                }
            }

            public void Close()
            {
                // Remove this state from the queue.
                RemoveFromQueue();

                // Clear the streamable bytes.
                UpdateStreamableBytes(0, false, 0);
            }
        }
    }
}
