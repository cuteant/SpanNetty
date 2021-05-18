﻿/*
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
    using DotNetty.Transport.Libuv.Concurrency;
    using DotNetty.Transport.Libuv.Native;

    /// <summary>
    /// Async handles allow the user to “wakeup” the event loop and get 
    /// a callback called from another thread.
    /// </summary>
    public sealed class Async : WorkHandle<Async>
    {
        private readonly Gate _gate;
        private volatile bool _closeScheduled;

        internal Async(LoopContext loop, Action<Async> callback)
            : this(loop, callback is object ? (h, s) => callback(h) : null, null)
        {
        }

        internal Async(LoopContext loop, Action<Async, object> callback, object state)
            : base(loop, uv_handle_type.UV_ASYNC)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            Callback = callback;
            State = state;
            _gate = new Gate();
            _closeScheduled = false;
        }

        public Async Send()
        {
            IDisposable guard = null;
            try
            {
                guard = _gate.TryAquire();
                if (guard is object && !_closeScheduled)
                {
                    NativeMethods.Send(InternalHandle);
                }
            }
            finally
            {
                guard?.Dispose();
            }

            return this;
        }

        protected override void ScheduleClose(Action<Async> handler = null)
        {
            using (_gate.Aquire())
            {
                _closeScheduled = true;
                base.ScheduleClose(handler);
            }
        }
    }
}
