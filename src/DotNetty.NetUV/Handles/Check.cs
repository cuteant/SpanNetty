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
    using DotNetty.NetUV.Native;

    /// <summary>
    /// Check handles will run the given callback once per loop iteration, 
    /// right after polling for i/o.
    /// </summary>
    public sealed class Check : WorkHandle
    {
        internal Check(LoopContext loop)
            : base(loop, uv_handle_type.UV_CHECK)
        { }

        public Check Start(Action<Check> callback)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            ScheduleStart(state => callback((Check)state));
            return this;
        }

        public void Stop() => StopHandle();

        public void CloseHandle(Action<Check> onClosed = null) =>
            base.CloseHandle(onClosed);
    }
}
