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
    using System.Diagnostics.Contracts;
    using DotNetty.Transport.Libuv.Native;

    /// <summary>
    /// Timer handles are used to schedule callbacks to be called in the future.
    /// </summary>
    public sealed class Timer : WorkHandle<Timer>
    {
        internal Timer(LoopContext loop)
            : base(loop, uv_handle_type.UV_TIMER)
        { }

        internal Timer(LoopContext loop, Action<Timer> callback)
            : base(loop, uv_handle_type.UV_TIMER)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }
            Callback = (h, s) => callback(h);
            State = null;
        }

        internal Timer(LoopContext loop, Action<Timer, object> callback, object state)
            : base(loop, uv_handle_type.UV_TIMER)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }
            Callback = callback;
            State = state;
        }

        public Timer Start(long timeout, long repeat)
        {
            if ((ulong)repeat > SharedConstants.TooBigOrNegative64) { ThrowHelper.ThrowArgumentException_PositiveOrZero(repeat, ExceptionArgument.repeat); }
            if ((ulong)timeout > SharedConstants.TooBigOrNegative64) { ThrowHelper.ThrowArgumentException_PositiveOrZero(timeout, ExceptionArgument.timeout); }

            Validate();
            NativeMethods.Start(InternalHandle, timeout, repeat);

            return this;
        }

        public Timer Start(Action<Timer> callback, long timeout, long repeat)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            Callback = (h, s) => callback(h);
            State = null;

            return Start(timeout, repeat);
        }

        public Timer Start(Action<Timer, object> callback, object state, long timeout, long repeat)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            Callback = callback;
            State = state;

            return Start(timeout, repeat);
        }

        public Timer SetRepeat(long repeat)
        {
            if ((ulong)repeat > SharedConstants.TooBigOrNegative64) { ThrowHelper.ThrowArgumentException_PositiveOrZero(repeat, ExceptionArgument.repeat); }

            Validate();
            NativeMethods.SetTimerRepeat(InternalHandle, repeat);

            return this;
        }

        public long GetRepeat()
        {
            Validate();
            return NativeMethods.GetTimerRepeat(InternalHandle);
        }

        public Timer Again()
        {
            Validate();
            NativeMethods.Again(InternalHandle);

            return this;
        }

        public void Stop() => StopHandle();
    }
}
