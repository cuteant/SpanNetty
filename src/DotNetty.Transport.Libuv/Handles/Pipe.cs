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
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Native;

    public sealed class Pipe : ServerStream<Pipe>
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

        public unsafe IStreamHandle CreatePendingType()
        {
            Validate();

            IInternalStreamHandle handle = null;
            int count = PendingCount();
            if (count > 0)
            {
                IntPtr loopHandle = ((uv_stream_t*)InternalHandle)->loop;
                var loop = HandleContext.GetTarget<LoopContext>(loopHandle);
                uv_handle_type handleType = NativeMethods.PipePendingType(InternalHandle);

                handle = handleType switch
                {
                    uv_handle_type.UV_TCP => new Tcp(loop),
                    uv_handle_type.UV_NAMED_PIPE => new Pipe(loop),
                    _ => throw ThrowHelper.GetInvalidOperationException_uv_handle_type_not_supported_or_IPC_over_Pipe_is_disabled(handleType),
                };
                NativeMethods.StreamAccept(InternalHandle, handle.InternalHandle);
                handle.ReadStart();
            }

            return handle;
        }

        internal override unsafe IInternalStreamHandle NewStream()
        {
            IntPtr loopHandle = ((uv_stream_t*)InternalHandle)->loop;
            var loop = HandleContext.GetTarget<LoopContext>(loopHandle);
            uv_handle_type type = ((uv_stream_t*)InternalHandle)->type;

            IInternalStreamHandle client = type switch
            {
                uv_handle_type.UV_NAMED_PIPE => new Pipe(loop, _ipc),
                uv_handle_type.UV_TCP => new Tcp(loop),
                _ => throw ThrowHelper.GetInvalidOperationException_Pipe_IPC_handle_not_supported(type),
            };
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
            _ipc = useIpc;
            StreamListen(onConnection, backlog);

            return this;
        }
    }
}
