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

    internal static class StreamHandle
    {
        internal static readonly uv_alloc_cb AllocateCallback = OnAllocateCallback;
        internal static readonly uv_read_cb ReadCallback = OnReadCallback;

        private static void OnReadCallback(IntPtr handle, IntPtr nread, ref uv_buf_t buf)
        {
            var stream = HandleContext.GetTarget<IInternalStreamHandle>(handle);
            IByteBuffer byteBuffer = stream.GetBuffer(ref buf);
            stream.OnReadCallback(byteBuffer, (int)nread.ToInt64());
        }

        private static void OnAllocateCallback(IntPtr handle, IntPtr suggestedSize, out uv_buf_t buf)
        {
            var stream = HandleContext.GetTarget<IInternalStreamHandle>(handle);
            stream.OnAllocateCallback(out buf);
        }
    }
}
