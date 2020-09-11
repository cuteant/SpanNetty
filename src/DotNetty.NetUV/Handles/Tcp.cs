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
    using System.Net;
    using DotNetty.Buffers;
    using DotNetty.NetUV.Native;

    public sealed class Tcp : ServerStream
    {
        internal Tcp(LoopContext loop)
            : base(loop, uv_handle_type.UV_TCP)
        { }

        public int GetSendBufferSize() => SendBufferSize(0);

        public int SetSendBufferSize(int value)
        {
            if ((uint)(value - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(value, ExceptionArgument.value); }

            return SendBufferSize(value);
        }

        public int GetReceiveBufferSize() => ReceiveBufferSize(0);


        public int SetReceiveBufferSize(int value)
        {
            if ((uint)(value - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(value, ExceptionArgument.value); }

            return ReceiveBufferSize(value);
        }

        public void Shutdown(Action<Tcp, Exception> completedAction = null) =>
            base.Shutdown((state, error) => completedAction?.Invoke((Tcp)state, error));

        public void QueueWrite(byte[] array, Action<Tcp, Exception> completion = null)
        {
            if (array is null) { return; }
            QueueWrite(array, 0, array.Length, completion);
        }

        public void QueueWrite(byte[] array, int offset, int count, Action<Tcp, Exception> completion = null)
        {
            if (array is null || 0u >= (uint)array.Length) { return; }
            if ((uint)count > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count); }
            if ((uint)(offset + count) > (uint)array.Length) { ThrowHelper.ThrowArgumentException_InvalidOffLen(); }

            QueueWriteStream(array, offset, count,
                (state, error) => completion?.Invoke((Tcp)state, error));
        }

        public void QueueWriteStream(WritableBuffer writableBuffer, Action<Tcp, Exception> completion) =>
            base.QueueWriteStream(writableBuffer, (streamHandle, exception) => completion((Tcp)streamHandle, exception));

        public Tcp OnRead(
            Action<Tcp, ReadableBuffer> onAccept,
            Action<Tcp, Exception> onError,
            Action<Tcp> onCompleted = null)
        {
            if (onAccept is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onAccept); }
            if (onError is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onError); }

            base.OnRead(
                (stream, buffer) => onAccept((Tcp)stream, buffer),
                (stream, error) => onError((Tcp)stream, error),
                stream => onCompleted?.Invoke((Tcp)stream));

            return this;
        }

        public Tcp OnRead(Action<Tcp, IStreamReadCompletion> onRead)
        {
            if (onRead is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onRead); }

            base.OnRead((stream, completion) => onRead((Tcp)stream, completion));
            return this;
        }

        public Tcp Bind(IPEndPoint endPoint, bool dualStack = false)
        {
            if (endPoint is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.endPoint); }

            Validate();
            NativeMethods.TcpBind(InternalHandle, endPoint, dualStack);

            return this;
        }

        public IPEndPoint GetLocalEndPoint()
        {
            Validate();
            return NativeMethods.TcpGetSocketName(InternalHandle);
        }

        public IPEndPoint GetPeerEndPoint()
        {
            Validate();
            return NativeMethods.TcpGetPeerName(InternalHandle);
        }

        public Tcp NoDelay(bool value)
        {
            Validate();
            NativeMethods.TcpSetNoDelay(InternalHandle, value);

            return this;
        }

        public Tcp KeepAlive(bool value, int delay)
        {
            Validate();
            NativeMethods.TcpSetKeepAlive(InternalHandle, value, delay);

            return this;
        }

        public Tcp SimultaneousAccepts(bool value)
        {
            Validate();
            NativeMethods.TcpSimultaneousAccepts(InternalHandle, value);

            return this;
        }

        protected internal override unsafe StreamHandle NewStream()
        {
            IntPtr loopHandle = ((uv_stream_t*)InternalHandle)->loop;
            var loop = HandleContext.GetTarget<LoopContext>(loopHandle);

            var client = new Tcp(loop);
            NativeMethods.StreamAccept(InternalHandle, client.InternalHandle);
            client.ReadStart();

#if DEBUG
            if (Log.DebugEnabled)
            {
                Log.Debug("{} {} client {} accepted",
                    HandleType, InternalHandle, client.InternalHandle);
            }
#endif

            return client;
        }

        public Tcp Listen(Action<Tcp, Exception> onConnection, int backlog = DefaultBacklog)
        {
            if (onConnection is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onConnection); }
            if ((uint)(backlog - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(backlog, ExceptionArgument.backlog); }

            StreamListen((handle, exception) => onConnection((Tcp)handle, exception), backlog);
            return this;
        }

        public void CloseHandle(Action<Tcp> onClosed = null)
        {
            Action<ScheduleHandle> handler = null;
            if (onClosed is object)
            {
                handler = state => onClosed((Tcp)state);
            }

            base.CloseHandle(handler);
        }
    }
}
