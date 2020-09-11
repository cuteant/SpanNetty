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
    using DotNetty.NetUV.Channels;
    using DotNetty.NetUV.Native;
    using DotNetty.NetUV.Requests;

    public abstract class StreamHandle : ScheduleHandle
    {
        internal static readonly uv_alloc_cb AllocateCallback = OnAllocateCallback;
        internal static readonly uv_read_cb ReadCallback = OnReadCallback;

        private readonly Pipeline _pipeline;

        internal StreamHandle(
            LoopContext loop,
            uv_handle_type handleType,
            params object[] args)
            : base(loop, handleType, args)
        {
            _pipeline = new Pipeline(this);
        }

        public bool IsReadable => NativeMethods.IsStreamReadable(InternalHandle);

        public bool IsWritable => NativeMethods.IsStreamWritable(InternalHandle);

        protected int SendBufferSize(int value)
        {
            if ((uint)value > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(value, ExceptionArgument.value); }

            Validate();
            return NativeMethods.SendBufferSize(InternalHandle, value);
        }

        protected int ReceiveBufferSize(int value)
        {
            if ((uint)value > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(value, ExceptionArgument.value); }

            Validate();
            return NativeMethods.ReceiveBufferSize(InternalHandle, value);
        }

        public void GetFileDescriptor(ref IntPtr value)
        {
            Validate();
            NativeMethods.GetFileDescriptor(InternalHandle, ref value);
        }

        public unsafe long GetWriteQueueSize()
        {
            Validate();
            return (((uv_stream_t*)InternalHandle)->write_queue_size).ToInt64();
        }

        public WritableBuffer Allocate() => _pipeline.Allocate();

        public void OnRead(
            Action<StreamHandle, ReadableBuffer> onAccept,
            Action<StreamHandle, Exception> onError,
            Action<StreamHandle> onCompleted = null)
        {
            if (onAccept is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onAccept); }
            if (onError is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onError); }

            var consumer = new StreamConsumer<StreamHandle>(onAccept, onError, onCompleted);
            _pipeline.Consumer(consumer);
        }

        public void OnRead(Action<StreamHandle, IStreamReadCompletion> onRead)
        {
            if (onRead is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onRead); }

            var consumer = new ReadStreamConsumer<StreamHandle>(onRead);
            _pipeline.Consumer(consumer);
        }

        public void Shutdown(Action<StreamHandle, Exception> completion = null)
        {
            if (!IsValid)
            {
                return;
            }

            StreamShutdown streamShutdown = null;
            try
            {
                streamShutdown = new StreamShutdown(this, completion);
            }
            catch (Exception exception)
            {
                Exception error = exception;

                ErrorCode? errorCode = (error as OperationException)?.ErrorCode;
                if (errorCode == ErrorCode.EPIPE)
                {
                    // It is ok if the stream is already down
                    error = null;
                }
                if (error is object)
                {
                    Log.Handle_failed_to_shutdown(HandleType, InternalHandle, error);
                }

                StreamShutdown.Completed(completion, this, error);
                streamShutdown?.Dispose();
            }
        }

        public void CloseHandle(Action<StreamHandle> callback = null)
        {
            Action<ScheduleHandle> handler = null;
            if (callback is object)
            {
                handler = state => callback((StreamHandle)state);
            }

            base.CloseHandle(handler);
        }

        public void QueueWriteStream(WritableBuffer writableBuffer,
            Action<StreamHandle, Exception> completion)
        {
            if (completion is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.completion); }

            IByteBuffer buffer = writableBuffer.GetBuffer();
            if (buffer is null || !buffer.IsReadable()) { return; }

            _pipeline.QueueWrite(buffer, completion);
        }

        public void QueueWriteStream(WritableBuffer writableBuffer, StreamHandle sendHandle,
            Action<StreamHandle, Exception> completion)
        {
            if (completion is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.completion); }
            if (sendHandle is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.sendHandle); }

            IByteBuffer buffer = writableBuffer.GetBuffer();
            if (buffer is null || !buffer.IsReadable()) { return; }

            _pipeline.QueueWrite(buffer, sendHandle, completion);
        }

        public void QueueWriteStream(byte[] array, Action<StreamHandle, Exception> completion)
        {
            if (array is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array); }

            QueueWriteStream(array, 0, array.Length, completion);
        }

        public void QueueWriteStream(byte[] array, int offset, int count,
            Action<StreamHandle, Exception> completion)
        {
            if (array is null || 0u >= (uint)array.Length) { return; }
            if ((uint)count > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count); }
            if ((uint)(offset + count) > (uint)array.Length) { ThrowHelper.ThrowArgumentException_InvalidOffLen(); }

            IByteBuffer buffer = Unpooled.WrappedBuffer(array, offset, count);
            _pipeline.QueueWrite(buffer, completion);
        }

        public void QueueWriteStream(byte[] array, StreamHandle sendHandle,
            Action<StreamHandle, Exception> completion)
        {
            if (array is null) { return; }

            QueueWriteStream(array, 0, array.Length, sendHandle, completion);
        }

        public void QueueWriteStream(byte[] array, int offset, int count,
            StreamHandle sendHandle,
            Action<StreamHandle, Exception> completion)
        {
            if (array is null || 0u >= (uint)array.Length) { return; }
            if ((uint)count > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count); }
            if ((uint)(offset + count) > (uint)array.Length) { ThrowHelper.ThrowArgumentException_InvalidOffLen(); }

            IByteBuffer buffer = Unpooled.WrappedBuffer(array, offset, count);
            _pipeline.QueueWrite(buffer, sendHandle, completion);
        }

        internal unsafe void WriteStream(WriteRequest request)
        {
            Debug.Assert(request is object);

            Validate();
            try
            {
                NativeMethods.WriteStream(
                    request.InternalHandle,
                    InternalHandle,
                    request.Bufs,
                    ref request.Size);
            }
            catch (Exception exception)
            {
                Log.Failed_to_write_data(HandleType, request, exception);
                throw;
            }
        }

        internal unsafe void WriteStream(WriteRequest request, StreamHandle sendHandle)
        {
            Debug.Assert(request is object);
            Debug.Assert(sendHandle is object);

            Validate();
            try
            {
                NativeMethods.WriteStream(
                    request.InternalHandle,
                    InternalHandle,
                    request.Bufs,
                    ref request.Size,
                    sendHandle.InternalHandle);
            }
            catch (Exception exception)
            {
                Log.Failed_to_write_data(HandleType, request, exception);
                throw;
            }
        }

        public void TryWrite(byte[] array)
        {
            if (array is null) { return; }
            TryWrite(array, 0, array.Length);
        }

        internal unsafe void TryWrite(byte[] array, int offset, int count)
        {
            if (array is null || 0u >= (uint)array.Length) { return; }
            if ((uint)count > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count); }
            if ((uint)array.Length < (uint)(offset + count)) { ThrowHelper.ThrowArgumentException_InvalidOffLen(); }

            Validate();
            try
            {
                fixed (byte* memory = array)
                {
                    var buf = new uv_buf_t((IntPtr)memory + offset, count);
                    NativeMethods.TryWriteStream(InternalHandle, ref buf);
                }
            }
            catch (Exception exception)
            {
#if DEBUG
                if (Log.DebugEnabled) { Log.Debug($"{HandleType} Trying to write data failed.", exception); }
#endif
                throw;
            }
        }

        internal void ReadStart()
        {
            Validate();
            NativeMethods.StreamReadStart(InternalHandle);
#if DEBUG
            if (Log.TraceEnabled)
            {
                Log.Trace("{} {} Read started.", HandleType, InternalHandle);
            }
#endif
        }

        internal void ReadStop()
        {
            if (!IsValid) { return; }

            // This function is idempotent and may be safely called on a stopped stream.
            NativeMethods.StreamReadStop(InternalHandle);
#if DEBUG
            if (Log.TraceEnabled)
            {
                Log.Trace("{} {} Read stopped.", HandleType, InternalHandle);
            }
#endif
        }

        protected override void Close() => _pipeline.Dispose();

        private void OnReadCallback(IByteBuffer byteBuffer, int status)
        {
            //
            //  nread is > 0 if there is data available or < 0 on error.
            //  When we’ve reached EOF, nread will be set to UV_EOF.
            //  When nread < 0, the buf parameter might not point to a valid buffer; 
            //  in that case buf.len and buf.base are both set to 0
            //

            Debug.Assert(byteBuffer is object);

            // For status = 0 (Nothing to read)
            if (status >= 0)
            {
#if DEBUG
                if (Log.DebugEnabled)
                {
                    Log.Debug("{} {} read, buffer length = {} status = {}.", HandleType, InternalHandle, byteBuffer.Capacity, status);
                }
#endif

                _pipeline.OnReadCompleted(byteBuffer, status);
                return;
            }

            Exception exception = null;
            if (status != (int)uv_err_code.UV_EOF) // Stream end is not an error
            {
                exception = NativeMethods.CreateError((uv_err_code)status);
                Log.Handle_read_error(HandleType, InternalHandle, status, exception);
            }
#if DEBUG
            if (Log.DebugEnabled)
            {
                Log.Debug("{} {} read completed.", HandleType, InternalHandle);
            }
#endif

            _pipeline.OnReadCompleted(byteBuffer, exception);
            ReadStop();
        }

        private static void OnReadCallback(IntPtr handle, IntPtr nread, ref uv_buf_t buf)
        {
            var stream = HandleContext.GetTarget<StreamHandle>(handle);
            IByteBuffer byteBuffer = stream._pipeline.GetBuffer(ref buf);
            stream.OnReadCallback(byteBuffer, (int)nread.ToInt64());
        }

        private void OnAllocateCallback(out uv_buf_t buf)
        {
            buf = _pipeline.AllocateReadBuffer();
        }

        private static void OnAllocateCallback(IntPtr handle, IntPtr suggestedSize, out uv_buf_t buf)
        {
            var stream = HandleContext.GetTarget<StreamHandle>(handle);
            stream.OnAllocateCallback(out buf);
        }
    }
}
