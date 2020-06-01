// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics;
    using System.Net;

    public sealed class Tcp : TcpHandle
    {
        static readonly uv_alloc_cb AllocateCallback = OnAllocateCallback;
        static readonly uv_read_cb ReadCallback = OnReadCallback;

        readonly ReadOperation _pendingRead;
        INativeUnsafe _nativeUnsafe;

        internal Tcp(Loop loop, uint flags = 0 /* AF_UNSPEC */ ) : base(loop, flags)
        {
            _pendingRead = new ReadOperation();
        }

        internal void ReadStart(INativeUnsafe channel)
        {
            Debug.Assert(channel is object);

            Validate();
            int result = NativeMethods.uv_read_start(Handle, AllocateCallback, ReadCallback);
            NativeMethods.ThrowIfError(result);
            _nativeUnsafe = channel;
        }

        public void ReadStop()
        {
            if (Handle == IntPtr.Zero)
            {
                return;
            }

            // This function is idempotent and may be safely called on a stopped stream.
            NativeMethods.uv_read_stop(Handle);
        }

        void OnReadCallback(int statusCode, OperationException error)
        {
            try
            {
                _pendingRead.Complete(statusCode, error);
                _nativeUnsafe.FinishRead(_pendingRead);
            }
            catch (Exception exception)
            {
                if (Logger.WarnEnabled) Logger.TcpHandleReadCallbcakError(Handle, exception);
            }
            finally
            {
                _pendingRead.Reset();
            }
        }

        static void OnReadCallback(IntPtr handle, IntPtr nread, ref uv_buf_t buf)
        {
            var tcp = GetTarget<Tcp>(handle);
            int status = (int)nread.ToInt64();

            OperationException error = null;
            if (status < 0 && status != NativeMethods.EOF)
            {
                error = NativeMethods.CreateError((uv_err_code)status);
            }

            tcp.OnReadCallback(status, error);
        }

        protected override void OnClosed()
        {
            base.OnClosed();

            _pendingRead.Dispose();
            _nativeUnsafe = null;
        }

        void OnAllocateCallback(out uv_buf_t buf)
        {
            buf = _nativeUnsafe.PrepareRead(_pendingRead);
        }

        static void OnAllocateCallback(IntPtr handle, IntPtr suggestedSize, out uv_buf_t buf)
        {
            var tcp = GetTarget<Tcp>(handle);
            tcp.OnAllocateCallback(out buf);
        }

        public IPEndPoint GetPeerEndPoint()
        {
            Validate();
            return NativeMethods.TcpGetPeerName(Handle);
        }
    }
}
