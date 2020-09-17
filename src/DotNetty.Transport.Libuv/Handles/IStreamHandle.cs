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
    using DotNetty.Buffers;
    using DotNetty.Transport.Libuv.Native;
    using DotNetty.Transport.Libuv.Requests;

    public interface IStreamHandle : IScheduleHandle
    {
        bool IsReadable { get; }

        bool IsWritable { get; }

        void GetFileDescriptor(ref IntPtr value);

        long GetWriteQueueSize();

        WritableBuffer Allocate();

        void TryWrite(byte[] array);
    }

    internal interface IInternalStreamHandle : IStreamHandle, IInternalScheduleHandle
    {
        IByteBuffer GetBuffer(ref uv_buf_t buf);

        void WriteStream(WriteRequest request);
        void WriteStream(WriteRequest request, IInternalStreamHandle sendHandle);

        void ReadStart();

        void OnReadCallback(IByteBuffer byteBuffer, int status);

        void OnAllocateCallback(out uv_buf_t buf);
    }
}
