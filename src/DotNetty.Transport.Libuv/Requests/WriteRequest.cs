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

namespace DotNetty.Transport.Libuv.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Transport.Libuv.Native;

    internal sealed class WriteRequest : ScheduleRequest
    {
        private const int MaximumLimit = 32;

        internal static readonly uv_watcher_cb WriteCallback = (h, s) => OnWriteCallback(h, s);
        private static readonly int BufferSize;

        static WriteRequest()
        {
            BufferSize = Marshal.SizeOf<uv_buf_t>();
        }

        private readonly RequestContext _requestContext;
        private readonly ThreadLocalPool.Handle _recyclerHandle;
        private readonly List<GCHandle> _handles;
        private IntPtr _bufs;
        private GCHandle _pin;
        private int _count;
        private uv_buf_t[] _bufsArray;
        private Action<WriteRequest, Exception> _completion;

        internal WriteRequest(uv_req_type requestType, ThreadLocalPool.Handle recyclerHandle)
            : base(requestType)
        {
            Debug.Assert(requestType == uv_req_type.UV_WRITE || requestType == uv_req_type.UV_UDP_SEND);

            _requestContext = new RequestContext(requestType, BufferSize * MaximumLimit, this);
            _recyclerHandle = recyclerHandle;
            _handles = new List<GCHandle>();

            IntPtr addr = _requestContext.Handle;
            _bufs = addr + _requestContext.HandleSize;
            _pin = GCHandle.Alloc(addr, GCHandleType.Pinned);
            _count = 0;
        }

        internal override IntPtr InternalHandle
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => _requestContext.Handle;
        }

        internal void Prepare(IByteBuffer buf, Action<WriteRequest, Exception> callback)
        {
            Debug.Assert(buf is object && callback is object);

            if (!_requestContext.IsValid)
            {
                ThrowInvalidOperationException_WriteRequest();
            }

            _completion = callback;
            int len = buf.ReadableBytes;

            IntPtr addr = IntPtr.Zero;
            if (buf.HasMemoryAddress)
            {
                addr = buf.AddressOfPinnedMemory();
            }

            if (addr != IntPtr.Zero)
            {
                Add(addr, buf.ReaderIndex, len);
                return;
            }

            if (buf.IsSingleIoBuffer)
            {
                ArraySegment<byte> arraySegment = buf.GetIoBuffer();

                byte[] array = arraySegment.Array;
                GCHandle handle = GCHandle.Alloc(array, GCHandleType.Pinned);
                _handles.Add(handle);

                addr = handle.AddrOfPinnedObject();
                Add(addr, arraySegment.Offset, arraySegment.Count);
                return;
            }

            ArraySegment<byte>[] segments = buf.GetIoBuffers();
            if (segments.Length <= MaximumLimit)
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    var segment = segments[i];
                    GCHandle handle = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
                    _handles.Add(handle);

                    addr = handle.AddrOfPinnedObject();
                    Add(addr, segment.Offset, segment.Count);
                }
                return;
            }

            _bufsArray = new uv_buf_t[segments.Length];
            GCHandle bufsPin = GCHandle.Alloc(_bufsArray, GCHandleType.Pinned);
            _handles.Add(bufsPin);

            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                GCHandle handle = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
                _handles.Add(handle);

                addr = handle.AddrOfPinnedObject();
                _bufsArray[i] = new uv_buf_t(addr + segment.Offset, segment.Count);
            }
            _count = segments.Length;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowInvalidOperationException_WriteRequest()
        {
            throw GetInvalidOperationException();

            static InvalidOperationException GetInvalidOperationException()
            {
                return new ObjectDisposedException("WriteRequest status is invalid.");
            }
        }

        private void Add(IntPtr addr, int offset, int len)
        {
            IntPtr baseOffset = _bufs + BufferSize * _count;
            ++_count;
            uv_buf_t.InitMemory(baseOffset, addr + offset, len);
        }

        internal unsafe uv_buf_t* Bufs
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => _bufsArray is null ? (uv_buf_t*)_bufs : (uv_buf_t*)Unsafe.AsPointer(ref _bufsArray[0]);
        }

        internal ref int Size
        {
            [MethodImpl(InlineMethod.AggressiveOptimization)]
            get => ref _count;
        }

        internal void Release()
        {
            var handlerCount = _handles.Count;
            if ((uint)handlerCount > 0u)
            {
                for (int i = 0; i < handlerCount; i++)
                {
                    var handler = _handles[i];
                    if (handler.IsAllocated)
                    {
                        handler.Free();
                    }
                }
                _handles.Clear();
            }

            _bufsArray = null;
            _completion = null;
            _count = 0;
            _recyclerHandle.Release(this);
        }

        private void Free()
        {
            Release();
            if (_pin.IsAllocated)
            {
                _pin.Free();
            }
            _bufs = IntPtr.Zero;
        }

        private void OnWriteCallback(int status)
        {
            OperationException error = null;
            if ((uint)status > SharedConstants.TooBigOrNegative) // < 0
            {
                error = NativeMethods.CreateError((uv_err_code)status);
            }

            Action<WriteRequest, Exception> callback = _completion;
            Release();
            callback?.Invoke(this, error);
        }

        private static void OnWriteCallback(IntPtr handle, int status)
        {
            var request = RequestContext.GetTarget<WriteRequest>(handle);
            request.OnWriteCallback(status);
        }

        protected override void Close()
        {
            if (_bufs != IntPtr.Zero)
            {
                Free();
            }
            _requestContext.Dispose();
        }
    }
}
