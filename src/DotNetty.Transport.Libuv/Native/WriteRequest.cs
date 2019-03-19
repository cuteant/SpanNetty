// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ForCanBeConvertedToForeach
namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Transport.Channels;

    sealed class WriteRequest : NativeRequest, ChannelOutboundBuffer.IMessageProcessor
    {
        static readonly int BufferSize;
        static readonly uv_watcher_cb WriteCallback = OnWriteCallback;

        const int MaximumBytes = int.MaxValue;
        const int MaximumLimit = 64;

        static WriteRequest()
        {
            BufferSize = Marshal.SizeOf<uv_buf_t>();
        }

        readonly int maxBytes;
        readonly ThreadLocalPool.Handle recyclerHandle;
        readonly List<MemoryHandle> handles;

        IntPtr bufs;
        GCHandle pin;
        int count;
        int size;

        INativeUnsafe nativeUnsafe;

        public WriteRequest(ThreadLocalPool.Handle recyclerHandle)
            : base(uv_req_type.UV_WRITE, BufferSize * MaximumLimit)
        {
            this.recyclerHandle = recyclerHandle;

            int offset = NativeMethods.GetSize(uv_req_type.UV_WRITE);
            IntPtr addr = this.Handle;

            this.maxBytes = MaximumBytes;
            this.bufs = addr + offset;
            this.pin = GCHandle.Alloc(addr, GCHandleType.Pinned);
            this.handles = new List<MemoryHandle>(MaximumLimit + 1);
        }

        internal void DoWrite(INativeUnsafe channelUnsafe, ChannelOutboundBuffer input)
        {
            Debug.Assert(this.nativeUnsafe == null);

            this.nativeUnsafe = channelUnsafe;
            input.ForEachFlushedMessage(this);
            this.DoWrite();
        }

        bool Add(IByteBuffer buf)
        {
            if (this.count == MaximumLimit) { return false; }

            int len = buf.ReadableBytes;
            if (0u >= (uint)len) { return true; }

            if (this.maxBytes - len < this.size && this.count > 0) { return false; }

            if (buf.IoBufferCount == 1)
            {
                var memory = buf.GetReadableMemory();
                this.Add(memory.Pin(), memory.Length);
                return true;
            }

            return AddMany(buf);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        bool AddMany(IByteBuffer buf)
        {
            if (MaximumLimit - buf.IoBufferCount < this.count) { return false; }

            var segments = buf.GetSequence();
            foreach (var memory in segments)
            {
                this.Add(memory.Pin(), memory.Length);
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe void Add(MemoryHandle memoryHandle, int len)
        {
            this.handles.Add(memoryHandle);
            IntPtr baseOffset = this.MemoryAddress(this.count);
            this.size += len;
            ++this.count;
            uv_buf_t.InitMemory(baseOffset, (IntPtr)memoryHandle.Pointer, len);
        }

        unsafe void DoWrite()
        {
            int result = NativeMethods.uv_write(
                this.Handle,
                this.nativeUnsafe.UnsafeHandle,
                (uv_buf_t*)this.bufs,
                this.count,
                WriteCallback);

            if (result < 0)
            {
                this.Release();
                NativeMethods.ThrowOperationException((uv_err_code)result);
            }
        }

        public bool ProcessMessage(object msg) => msg is IByteBuffer buf && this.Add(buf);

        void Release()
        {
            var handleCount = this.handles.Count;
            if (handleCount > 0)
            {
                for (int i = 0; i < handleCount; i++)
                {
                    this.handles[i].Dispose();
                }
                this.handles.Clear();
            }

            this.nativeUnsafe = null;
            this.count = 0;
            this.size = 0;
            this.recyclerHandle.Release(this);
        }

        void OnWriteCallback(int status)
        {
            INativeUnsafe @unsafe = this.nativeUnsafe;
            int bytesWritten = this.size;
            this.Release();

            OperationException error = null;
            if (status < 0)
            {
                error = NativeMethods.CreateError((uv_err_code)status);
            }
            @unsafe.FinishWrite(bytesWritten, error);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IntPtr MemoryAddress(int offset) => this.bufs + BufferSize * offset;

        static void OnWriteCallback(IntPtr handle, int status)
        {
            var request = GetTarget<WriteRequest>(handle);
            request.OnWriteCallback(status);
        }

        void Free()
        {
            this.Release();
            if (this.pin.IsAllocated)
            {
                this.pin.Free();
            }
            this.bufs = IntPtr.Zero;
        }

        protected override void Dispose(bool disposing)
        {
            if (this.bufs != IntPtr.Zero)
            {
                this.Free();
            }
            base.Dispose(disposing);
        }
    }
}