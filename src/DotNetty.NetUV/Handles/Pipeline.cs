/*
 * Copyright (c) Johnny Z. All rights reserved.
 *
 *   https://github.com/StormHub/NetUV
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com)
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.NetUV.Handles
{
    using System;
    using System.Diagnostics;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.NetUV.Channels;
    using DotNetty.NetUV.Native;
    using DotNetty.NetUV.Requests;

    internal sealed class Pipeline : IDisposable
    {
        private static readonly IInternalLogger Log = InternalLoggerFactory.GetInstance<Pipeline>();

        private readonly StreamHandle _streamHandle;
        private readonly PooledByteBufferAllocator _allocator;
        private readonly ReceiveBufferSizeEstimate _receiveBufferSizeEstimate;
        private readonly PendingRead _pendingRead;
        private IStreamConsumer<StreamHandle> _streamConsumer;

        internal Pipeline(StreamHandle streamHandle)
            : this(streamHandle, PooledByteBufferAllocator.Default)
        { }

        internal Pipeline(StreamHandle streamHandle, PooledByteBufferAllocator allocator)
        {
            Debug.Assert(streamHandle is object);
            Debug.Assert(allocator is object);

            _streamHandle = streamHandle;
            _allocator = allocator;
            _receiveBufferSizeEstimate = new ReceiveBufferSizeEstimate();
            _pendingRead = new PendingRead();
        }

        internal void Consumer(IStreamConsumer<StreamHandle> consumer)
        {
            Debug.Assert(consumer is object);
            _streamConsumer = consumer;
        }

        internal WritableBuffer Allocate() => new WritableBuffer(_allocator.Buffer());

        internal uv_buf_t AllocateReadBuffer()
        {
            IByteBuffer buffer = _receiveBufferSizeEstimate.Allocate(_allocator);
#if DEBUG
            if (Log.TraceEnabled)
            {
                Log.Trace("{} receive buffer allocated size = {}", nameof(Pipeline), buffer.Capacity);
            }
#endif

            return _pendingRead.GetBuffer(buffer);
        }

        internal IByteBuffer GetBuffer(ref uv_buf_t buf)
        {
            IByteBuffer byteBuffer = _pendingRead.Buffer;
            _pendingRead.Reset();
            return byteBuffer;
        }

        internal void OnReadCompleted(IByteBuffer byteBuffer, Exception error) => InvokeRead(byteBuffer, 0, error, true);

        internal void OnReadCompleted(IByteBuffer byteBuffer, int size)
        {
            Debug.Assert(byteBuffer is object && size >= 0);

            _receiveBufferSizeEstimate.Record(size);
            InvokeRead(byteBuffer, size);
        }

        private void InvokeRead(IByteBuffer byteBuffer, int size, Exception error = null, bool completed = false)
        {
            if ((uint)(size - 1) > SharedConstants.TooBigOrNegative) // <= 0
            {
                byteBuffer.Release();
            }

            ReadableBuffer buffer = size > 0
                ? new ReadableBuffer(byteBuffer, size)
                : ReadableBuffer.Empty;

            var completion = new StreamReadCompletion(ref buffer, error, completed);
            try
            {
                _streamConsumer?.Consume(_streamHandle, completion);
            }
            catch (Exception exception)
            {
                Log.Pipeline_Exception_whilst_invoking_read_callback(exception);
            }
            finally
            {
                completion.Dispose();
            }
        }

        internal void QueueWrite(IByteBuffer buf, Action<StreamHandle, Exception> completion)
        {
            Debug.Assert(buf is object);

            WriteRequest request = Loop.WriteRequestPool.Take();
            try
            {
                request.Prepare(buf,
                    (writeRequest, exception) => completion?.Invoke(_streamHandle, exception));

                _streamHandle.WriteStream(request);
            }
            catch (Exception exception)
            {
                Log.Pipeline_Handle_faulted(_streamHandle.HandleType, exception);
                request.Release();
                throw;
            }
        }

        internal void QueueWrite(IByteBuffer bufferRef, StreamHandle sendHandle, Action<StreamHandle, Exception> completion)
        {
            Debug.Assert(bufferRef is object && sendHandle is object);

            WriteRequest request = Loop.WriteRequestPool.Take();
            try
            {
                request.Prepare(bufferRef,
                    (writeRequest, exception) => completion?.Invoke(_streamHandle, exception));

                _streamHandle.WriteStream(request, sendHandle);
            }
            catch (Exception exception)
            {
                Log.Pipeline_Handle_faulted(_streamHandle.HandleType, exception);
                request.Release();
                throw;
            }
        }

        public void Dispose()
        {
            _pendingRead.Dispose();
            _streamConsumer = null;
        }

        private sealed class StreamReadCompletion : ReadCompletion, IStreamReadCompletion
        {
            private readonly bool _completed;

            internal StreamReadCompletion(ref ReadableBuffer data, Exception error, bool completed)
                : base(ref data, error)
            {
                _completed = completed;
            }

            public bool Completed => _completed;
        }
    }
}
