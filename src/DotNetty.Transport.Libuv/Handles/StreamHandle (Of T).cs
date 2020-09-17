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

namespace DotNetty.Transport.Libuv.Handles
{
    using System;
    using System.Diagnostics;
    using DotNetty.Buffers;
    using DotNetty.Transport.Libuv.Native;
    using DotNetty.Transport.Libuv.Requests;

    public abstract class StreamHandle<THandle> : ScheduleHandle<THandle>, IInternalStreamHandle
        where THandle : StreamHandle<THandle>
    {
        private readonly Pipeline<THandle> _pipeline;

        internal StreamHandle(
            LoopContext loop,
            uv_handle_type handleType,
            params object[] args)
            : base(loop, handleType, args)
        {
            _pipeline = new Pipeline<THandle>((THandle)this);
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

        public virtual void OnRead(
            Action<THandle, ReadableBuffer> onAccept,
            Action<THandle, Exception> onError,
            Action<THandle> onCompleted = null)
        {
            if (onAccept is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onAccept); }
            if (onError is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onError); }

            var consumer = new StreamConsumer<THandle>(onAccept, onError, onCompleted);
            _pipeline.Consumer(consumer);
        }

        public virtual void OnRead(Action<THandle, IStreamReadCompletion> onRead)
        {
            if (onRead is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onRead); }

            var consumer = new ReadStreamConsumer<THandle>(onRead);
            _pipeline.Consumer(consumer);
        }

        public void Shutdown(Action<THandle, Exception> completion = null)
        {
            if (!IsValid)
            {
                return;
            }

            StreamShutdown<THandle> streamShutdown = null;
            try
            {
                streamShutdown = new StreamShutdown<THandle>((THandle)this, completion);
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

                StreamShutdown<THandle>.Completed(completion, (THandle)this, error);
                streamShutdown?.Dispose();
            }
        }

        public void QueueWriteStream(WritableBuffer writableBuffer,
            Action<THandle, Exception> completion)
        {
            if (completion is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.completion); }

            IByteBuffer buffer = writableBuffer.GetBuffer();
            if (buffer is null || !buffer.IsReadable()) { return; }

            _pipeline.QueueWrite(buffer, completion);
        }

        public void QueueWriteStream(WritableBuffer writableBuffer, THandle sendHandle,
            Action<THandle, Exception> completion)
        {
            if (completion is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.completion); }
            if (sendHandle is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.sendHandle); }

            IByteBuffer buffer = writableBuffer.GetBuffer();
            if (buffer is null || !buffer.IsReadable()) { return; }

            _pipeline.QueueWrite(buffer, sendHandle, completion);
        }

        public void QueueWriteStream(byte[] array, Action<THandle, Exception> completion)
        {
            if (array is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array); }

            QueueWriteStream(array, 0, array.Length, completion);
        }

        public void QueueWriteStream(byte[] array, int offset, int count,
            Action<THandle, Exception> completion)
        {
            if (array is null || 0u >= (uint)array.Length) { return; }
            if ((uint)count > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count); }
            if ((uint)(offset + count) > (uint)array.Length) { ThrowHelper.ThrowArgumentException_InvalidOffLen(); }

            IByteBuffer buffer = Unpooled.WrappedBuffer(array, offset, count);
            _pipeline.QueueWrite(buffer, completion);
        }

        public void QueueWriteStream(byte[] array, THandle sendHandle, Action<THandle, Exception> completion)
        {
            if (array is null) { return; }

            QueueWriteStream(array, 0, array.Length, sendHandle, completion);
        }

        public void QueueWriteStream(byte[] array, int offset, int count,
            THandle sendHandle, Action<THandle, Exception> completion)
        {
            if (array is null || 0u >= (uint)array.Length) { return; }
            if ((uint)count > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count); }
            if ((uint)(offset + count) > (uint)array.Length) { ThrowHelper.ThrowArgumentException_InvalidOffLen(); }

            IByteBuffer buffer = Unpooled.WrappedBuffer(array, offset, count);
            _pipeline.QueueWrite(buffer, sendHandle, completion);
        }

        unsafe void IInternalStreamHandle.WriteStream(WriteRequest request)
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

        unsafe void IInternalStreamHandle.WriteStream(WriteRequest request, IInternalStreamHandle sendHandle)
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
#if DEBUG
            catch (Exception exception)
            {
                if (Log.DebugEnabled) { Log.Debug($"{HandleType} Trying to write data failed.", exception); }
#else
            catch (Exception)
            {
#endif
                throw;
            }
        }

        IByteBuffer IInternalStreamHandle.GetBuffer(ref uv_buf_t buf) => _pipeline.GetBuffer(ref buf);

        void IInternalStreamHandle.ReadStart() => ReadStart();

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

        void IInternalStreamHandle.OnReadCallback(IByteBuffer byteBuffer, int status)
        {
            //
            //  nread is > 0 if there is data available or < 0 on error.
            //  When we’ve reached EOF, nread will be set to UV_EOF.
            //  When nread < 0, the buf parameter might not point to a valid buffer; 
            //  in that case buf.len and buf.base are both set to 0
            //

            Debug.Assert(byteBuffer is object);

            // For status = 0 (Nothing to read)
            if (SharedConstants.TooBigOrNegative >= (uint)status) // status >= 0
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
            if (status != NativeMethods.EOF) // Stream end is not an error
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

        void IInternalStreamHandle.OnAllocateCallback(out uv_buf_t buf)
        {
            buf = _pipeline.AllocateReadBuffer();
        }
    }
}
