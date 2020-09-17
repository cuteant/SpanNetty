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
    using DotNetty.Transport.Libuv.Native;

    internal static class WorkHandle
    {
        internal static readonly uv_work_cb WorkCallback = h => OnWorkCallback(h);

        private static void OnWorkCallback(IntPtr handle)
        {
            var workHandle = HandleContext.GetTarget<IWorkHandle>(handle);
            workHandle?.OnWorkCallback();
        }
    }
}
