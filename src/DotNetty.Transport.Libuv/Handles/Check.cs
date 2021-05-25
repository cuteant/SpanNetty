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

    /// <summary>
    /// Check handles will run the given callback once per loop iteration, 
    /// right after polling for i/o.
    /// </summary>
    public sealed class Check : WorkHandle<Check>
    {
        internal Check(LoopContext loop)
            : base(loop, uv_handle_type.UV_CHECK)
        { }

        public Check Start(Action<Check> callback)
        {
            ScheduleStart(callback);
            return this;
        }

        public Check Start(Action<Check, object> callback, object state)
        {
            ScheduleStart(callback, state);
            return this;
        }

        public void Stop() => StopHandle();
    }
}
