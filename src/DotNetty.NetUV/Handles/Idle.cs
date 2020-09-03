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
    /// Idle handles will run the given callback once per loop iteration, 
    /// right before the uv_prepare_t handles
    /// </summary>
    public sealed class Idle : WorkHandle
    {
        internal Idle(LoopContext loop)
            : base(loop, uv_handle_type.UV_IDLE)
        { }

        public Idle Start(Action<Idle> callback)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            ScheduleStart(state => callback((Idle)state));

            return this;
        }

        public void Stop() => StopHandle();

        public void CloseHandle(Action<Idle> onClosed = null) =>
            base.CloseHandle(onClosed);
    }
}
