// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics;
    using CuteAnt.Collections;

    /// <summary>
    /// A <see cref="IStreamByteDistributor"/> that ignores stream priority and uniformly allocates bytes to all
    /// streams. This class uses a minimum chunk size that will be allocated to each stream. While
    /// fewer streams may be written to in each call to <see cref="Distribute(int, IStreamByteDistributorWriter)"/>, doing this
    /// should improve the goodput on each written stream.
    /// </summary>
    public sealed class UniformStreamByteDistributor : Http2ConnectionAdapter, IStreamByteDistributor
    {
        private readonly IHttp2ConnectionPropertyKey stateKey;
        private readonly Deque<State> queue = new Deque<State>(4);

        /// <summary>
        /// The minimum number of bytes that we will attempt to allocate to a stream. This is to
        /// help improve goodput on a per-stream basis.
        /// </summary>
        private int minAllocationChunk = Http2CodecUtil.DefaultMinAllocationChunk;
        private long totalStreamableBytes;

        public UniformStreamByteDistributor(IHttp2Connection connection)
        {
            // Add a state for the connection.
            this.stateKey = connection.NewKey();
            var connectionStream = connection.ConnectionStream;
            connectionStream.SetProperty(stateKey, new State(this, connectionStream));

            // Register for notification of new streams.
            connection.AddListener(this);
        }

        public override void OnStreamAdded(IHttp2Stream stream)
        {
            stream.SetProperty(this.stateKey, new State(this, stream));
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
            if (minAllocationChunk <= 0)
            {
                ThrowHelper.ThrowArgumentException_Positive(minAllocationChunk, ExceptionArgument.minAllocationChunk);
            }
            this.minAllocationChunk = minAllocationChunk;
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
            var size = queue.Count;
            if (0u >= (uint)size)
            {
                return totalStreamableBytes > 0;
            }

            var chunkSize = Math.Max(minAllocationChunk, maxBytes / size);

            State state = queue.RemoveFromFront();
            do
            {
                state.enqueued = false;
                if (state.windowNegative)
                {
                    continue;
                }
                if (0u >= (uint)maxBytes && state.streamableBytes > 0)
                {
                    // Stop at the first state that can't send. Add this state back to the head of the queue. Note
                    // that empty frames at the head of the queue will always be written, assuming the stream window
                    // is not negative.
                    queue.AddToFront(state);
                    state.enqueued = true;
                    break;
                }

                // Allocate as much data as we can for this stream.
                int chunk = Math.Min(chunkSize, Math.Min(maxBytes, state.streamableBytes));
                maxBytes -= chunk;

                // Write the allocated bytes and enqueue as necessary.
                state.Write(chunk, writer);
            } while (queue.TryRemoveFromFront(out state));

            return totalStreamableBytes > 0;
        }

        private State GetState(IHttp2Stream stream)
        {
            if (null == stream) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.stream); }
            return stream.GetProperty<State>(stateKey);
        }

        /// <summary>
        /// The remote flow control state for a single stream.
        /// </summary>
        private sealed class State
        {
            readonly UniformStreamByteDistributor distributor;
            readonly IHttp2Stream stream;
            internal int streamableBytes;
            internal bool windowNegative;
            internal bool enqueued;
            bool writing;

            public State(UniformStreamByteDistributor distributor, IHttp2Stream stream)
            {
                this.distributor = distributor;
                this.stream = stream;
            }

            public void UpdateStreamableBytes(int newStreamableBytes, bool hasFrame, int windowSize)
            {
                Debug.Assert(hasFrame || newStreamableBytes == 0, "hasFrame: " + hasFrame + " newStreamableBytes: " + newStreamableBytes);

                int delta = newStreamableBytes - streamableBytes;
                if (delta != 0)
                {
                    streamableBytes = newStreamableBytes;
                    this.distributor.totalStreamableBytes += delta;
                }
                // In addition to only enqueuing state when they have frames we enforce the following restrictions:
                // 1. If the window has gone negative. We never want to queue a state. However we also don't want to
                //    Immediately remove the item if it is already queued because removal from deque is O(n). So
                //    we allow it to stay queued and rely on the distribution loop to remove this state.
                // 2. If the window is zero we only want to queue if we are not writing. If we are writing that means
                //    we gave the state a chance to write zero length frames. We wait until updateStreamableBytes is
                //    called again before this state is allowed to write.
                windowNegative = windowSize < 0;
                if (hasFrame && (windowSize > 0 || (0u >= (uint)windowSize && !writing)))
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
                writing = true;
                try
                {
                    // Write the allocated bytes.
                    writer.Write(stream, numBytes);
                }
                catch (Exception t)
                {
                    ThrowHelper.ThrowConnectionError_ByteDistributionWriteError(t);
                }
                finally
                {
                    writing = false;
                }
            }

            void AddToQueue()
            {
                if (!enqueued)
                {
                    enqueued = true;
                    this.distributor.queue.AddToBack(this);
                }
            }

            void RemoveFromQueue()
            {
                if (enqueued)
                {
                    enqueued = false;
                    this.distributor.queue.Remove(this);
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
