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
    /// Prepare handles will run the given callback once per loop iteration, 
    /// right before polling for i/o.
    /// </summary>
    public sealed class Prepare : WorkHandle<Prepare>
    {
        internal Prepare(LoopContext loop)
            : base(loop, uv_handle_type.UV_PREPARE)
        { }

        public Prepare Start(Action<Prepare> callback)
        {
            ScheduleStart(callback);
            return this;
        }

        public Prepare Start(Action<Prepare, object> callback, object state)
        {
            ScheduleStart(callback, state);
            return this;
        }

        public void Stop() => StopHandle();
    }
}
