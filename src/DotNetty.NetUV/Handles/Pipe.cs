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
    using DotNetty.Buffers;
    using DotNetty.NetUV.Native;

    public sealed class Pipe : ServerStream
    {
        private bool _ipc;

        internal Pipe(LoopContext loop, bool ipc = false)
            : base(loop, uv_handle_type.UV_NAMED_PIPE, ipc)
        {
            _ipc = ipc;
        }

        public int GetSendBufferSize()
        {
            if (Platform.IsWindows)
            {
                ThrowHelper.ThrowPlatformNotSupportedException_handle_type_send_buffer_size_setting_not_supported_on_Windows(HandleType);
            }

            return SendBufferSize(0);
        }

        public int SetSendBufferSize(int value)
        {
            if ((uint)value > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(value, ExceptionArgument.value); }

            if (Platform.IsWindows)
            {
                ThrowHelper.ThrowPlatformNotSupportedException_handle_type_send_buffer_size_setting_not_supported_on_Windows(HandleType);
            }

            return SendBufferSize(value);
        }

        public int GetReceiveBufferSize()
        {
            if (Platform.IsWindows)
            {
                ThrowHelper.ThrowPlatformNotSupportedException_handle_type_send_buffer_size_setting_not_supported_on_Windows(HandleType);
            }

            return ReceiveBufferSize(0);
        }

        public int SetReceiveBufferSize(int value)
        {
            if ((uint)value > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_PositiveOrZero(value, ExceptionArgument.value); }

            if (Platform.IsWindows)
            {
                ThrowHelper.ThrowPlatformNotSupportedException_handle_type_send_buffer_size_setting_not_supported_on_Windows(HandleType);
            }

            return ReceiveBufferSize(value);
        }

        public Pipe OnRead(
            Action<Pipe, ReadableBuffer> onAccept,
            Action<Pipe, Exception> onError,
            Action<Pipe> onCompleted = null)
        {
            if (onAccept is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onAccept); }
            if (onError is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onError); }

            base.OnRead(
                (stream, buffer) => onAccept((Pipe)stream, buffer),
                (stream, error) => onError((Pipe)stream, error),
                stream => onCompleted?.Invoke((Pipe)stream));

            return this;
        }

        public Pipe OnRead(Action<Pipe, IStreamReadCompletion> onRead)
        {
            if (onRead is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onRead); }

            base.OnRead((stream, completion) => onRead((Pipe)stream, completion));
            return this;
        }

        public void QueueWriteStream(WritableBuffer writableBuffer, Action<Pipe, Exception> completion) =>
            base.QueueWriteStream(writableBuffer,
                (streamHandle, exception) => completion((Pipe)streamHandle, exception));

        public void QueueWriteStream(WritableBuffer writableBuffer, Tcp sendHandle, Action<Pipe, Exception> completion) =>
            base.QueueWriteStream(writableBuffer, sendHandle,
                (streamHandle, exception) => completion((Pipe)streamHandle, exception));

        public Pipe Bind(string name)
        {
            if (string.IsNullOrEmpty(name)) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.name); }

            Validate();
            NativeMethods.PipeBind(InternalHandle, name);

            return this;
        }

        public string GetSocketName()
        {
            Validate();
            return NativeMethods.PipeGetSocketName(InternalHandle);
        }

        public string GetPeerName()
        {
            Validate();
            return NativeMethods.PipeGetPeerName(InternalHandle);
        }

        public void PendingInstances(int count)
        {
            if ((uint)(count - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(count, ExceptionArgument.count); }

            Validate();
            NativeMethods.PipePendingInstances(InternalHandle, count);
        }

        public int PendingCount()
        {
            Validate();
            return NativeMethods.PipePendingCount(InternalHandle);
        }

        public unsafe StreamHandle CreatePendingType()
        {
            Validate();

            StreamHandle handle = null;
            int count = PendingCount();
            if (count > 0)
            {
                IntPtr loopHandle = ((uv_stream_t*)InternalHandle)->loop;
                var loop = HandleContext.GetTarget<LoopContext>(loopHandle);
                uv_handle_type handleType = NativeMethods.PipePendingType(InternalHandle);

                switch (handleType)
                {
                    case uv_handle_type.UV_TCP:
                        handle = new Tcp(loop);
                        break;
                    case uv_handle_type.UV_NAMED_PIPE:
                        handle = new Pipe(loop);
                        break;
                    default:
                        throw ThrowHelper.GetInvalidOperationException_uv_handle_type_not_supported_or_IPC_over_Pipe_is_disabled(handleType);
                }

                NativeMethods.StreamAccept(InternalHandle, handle.InternalHandle);
                handle.ReadStart();
            }

            return handle;
        }

        protected internal override unsafe StreamHandle NewStream()
        {
            IntPtr loopHandle = ((uv_stream_t*)InternalHandle)->loop;
            var loop = HandleContext.GetTarget<LoopContext>(loopHandle);
            uv_handle_type type = ((uv_stream_t*)InternalHandle)->type;

            StreamHandle client;
            switch (type)
            {
                case uv_handle_type.UV_NAMED_PIPE:
                    client = new Pipe(loop, _ipc);
                    break;
                case uv_handle_type.UV_TCP:
                    client = new Tcp(loop);
                    break;
                default:
                    throw ThrowHelper.GetInvalidOperationException_Pipe_IPC_handle_not_supported(type);
            }

            NativeMethods.StreamAccept(InternalHandle, client.InternalHandle);
            if (!_ipc)
            {
                client.ReadStart();
            }

#if DEBUG
            if (Log.DebugEnabled)
            {
                Log.Debug("{} {1} client {} accepted. (IPC : {})", HandleType, InternalHandle, client.InternalHandle, _ipc);
            }
#endif

            return client;
        }

        public Pipe Listen(Action<Pipe, Exception> onConnection, bool useIpc) => Listen(onConnection, DefaultBacklog, useIpc);

        public Pipe Listen(Action<Pipe, Exception> onConnection, int backlog = DefaultBacklog, bool useIpc = false)
        {
            if (onConnection is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onConnection); }
            if ((uint)(backlog - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(backlog, ExceptionArgument.backlog); }

            _ipc = useIpc;
            StreamListen((handle, exception) => onConnection((Pipe)handle, exception), backlog);

            return this;
        }

        public void Shutdown(Action<Pipe, Exception> completedAction = null) =>
            base.Shutdown((state, error) => completedAction?.Invoke((Pipe)state, error));

        public void CloseHandle(Action<Pipe> onClosed = null)
        {
            Action<ScheduleHandle> handler = null;
            if (onClosed is object)
            {
                handler = state => onClosed((Pipe)state);
            }

            base.CloseHandle(handler);
        }
    }
}
