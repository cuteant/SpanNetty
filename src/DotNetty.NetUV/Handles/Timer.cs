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
    using System.Diagnostics.Contracts;
    using DotNetty.NetUV.Native;

    /// <summary>
    /// Timer handles are used to schedule callbacks to be called in the future.
    /// </summary>
    public sealed class Timer : WorkHandle
    {
        internal Timer(LoopContext loop)
            : base(loop, uv_handle_type.UV_TIMER)
        { }

        public Timer Start(Action<Timer> callback, long timeout, long repeat)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }
            if ((ulong)repeat > SharedConstants.TooBigOrNegative64) { ThrowHelper.ThrowArgumentException_PositiveOrZero(repeat, ExceptionArgument.repeat); }
            if ((ulong)timeout > SharedConstants.TooBigOrNegative64) { ThrowHelper.ThrowArgumentException_PositiveOrZero(timeout, ExceptionArgument.timeout); }

            Validate();
            Callback = state => callback((Timer)state);
            NativeMethods.Start(InternalHandle, timeout, repeat);

            return this;
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

        public void CloseHandle(Action<Timer> onClosed = null) =>
            base.CloseHandle(onClosed);
    }
}
