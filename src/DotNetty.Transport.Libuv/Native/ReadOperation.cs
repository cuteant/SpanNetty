// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Native
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using DotNetty.Buffers;

    sealed class ReadOperation : IDisposable
    {
        private int _status;
        private bool _endOfStream;
        private IByteBuffer _buffer;
        private OperationException _error;
        private GCHandle _pin;

        internal ReadOperation()
        {
            Reset();
        }

        internal IByteBuffer Buffer => _buffer;

        internal OperationException Error => _error;

        internal int Status => _status;

        internal bool EndOfStream => _endOfStream;

        internal void Complete(int statusCode, OperationException operationException)
        {
            Release();

            _status = statusCode;
            _endOfStream = statusCode == NativeMethods.EOF;
            _error = operationException;
        }

        internal uv_buf_t GetBuffer(IByteBuffer buf)
        {
            Debug.Assert(!_pin.IsAllocated);

            // Do not pin the buffer again if it is already pinned
            IntPtr arrayHandle = IntPtr.Zero;
            if (buf.HasMemoryAddress)
            {
                arrayHandle = buf.AddressOfPinnedMemory();
            }
            int index = buf.WriterIndex;

            if (arrayHandle == IntPtr.Zero)
            {
                _pin = GCHandle.Alloc(buf.Array, GCHandleType.Pinned);
                arrayHandle = _pin.AddrOfPinnedObject();
                index += buf.ArrayOffset;
            }
            int length = buf.WritableBytes;
            _buffer = buf;

            return new uv_buf_t(arrayHandle + index, length);
        }

        internal void Reset()
        {
            _status = 0;
            _endOfStream = false;
            _buffer = Unpooled.Empty;
            _error = null;
        }

        void Release()
        {
            if (_pin.IsAllocated)
            {
                _pin.Free();
            }
        }

        public void Dispose()
        {
            Release();
            Reset();
        }
    }
}