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

    public abstract class WorkHandle<THandle> : ScheduleHandle<THandle>, IWorkHandle
        where THandle : WorkHandle<THandle>
    {
        protected Action<THandle, object> Callback;
        protected object State;

        internal WorkHandle(LoopContext loop, uv_handle_type handleType, params object[] args)
            : base(loop, handleType, args)
        { }

        protected void ScheduleStart(Action<THandle> callback)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            ScheduleStart((h, s) => callback(h), null);
        }

        protected void ScheduleStart(Action<THandle, object> callback, object state)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            Validate();
            Callback = callback;
            State = state;
            NativeMethods.Start(HandleType, InternalHandle);
        }

        protected override void Close()
        {
            Callback = null;
            State = null;
        }

        void IWorkHandle.OnWorkCallback()
        {
#if DEBUG
            if (Log.TraceEnabled)
            {
                Log.Trace("{} {} callback", HandleType, InternalHandle);
            }
#endif

            try
            {
                Callback?.Invoke((THandle)this, State);
            }
            catch (Exception exception)
            {
                Log.Handle_callback_error(HandleType, InternalHandle, exception);
                throw;
            }
        }
    }
}
