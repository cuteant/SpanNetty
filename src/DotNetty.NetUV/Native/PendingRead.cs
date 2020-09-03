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

namespace DotNetty.NetUV.Native
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using DotNetty.Buffers;

    internal sealed class PendingRead : IDisposable
    {
        private IByteBuffer _buffer;
        private GCHandle _pin;

        internal PendingRead()
        {
            Reset();
        }

        internal IByteBuffer Buffer => _buffer;

        internal uv_buf_t GetBuffer(IByteBuffer buf)
        {
            Debug.Assert(!_pin.IsAllocated);

            // Do not pin the buffer again if it is already pinned
            IntPtr arrayHandle = buf.AddressOfPinnedMemory();
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
            _buffer = Unpooled.Empty;
        }

        private void Release()
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
