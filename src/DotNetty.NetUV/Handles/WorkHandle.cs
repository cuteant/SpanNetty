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

    public class WorkHandle : ScheduleHandle
    {
        internal static readonly uv_work_cb WorkCallback = h => OnWorkCallback(h);
        protected Action<WorkHandle> Callback;

        internal WorkHandle(
            LoopContext loop,
            uv_handle_type handleType,
            params object[] args)
            : base(loop, handleType, args)
        { }

        protected void ScheduleStart(Action<WorkHandle> callback)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            Validate();
            Callback = callback;
            NativeMethods.Start(HandleType, InternalHandle);
        }

        protected override void Close() => Callback = null;

        private void OnWorkCallback()
        {
#if DEBUG
            if (Log.TraceEnabled)
            {
                Log.Trace("{} {} callback", HandleType, InternalHandle);
            }
#endif

            try
            {
                Callback?.Invoke(this);
            }
            catch (Exception exception)
            {
                Log.Handle_callback_error(HandleType, InternalHandle, exception);
                throw;
            }
        }

        private static void OnWorkCallback(IntPtr handle)
        {
            var workHandle = HandleContext.GetTarget<WorkHandle>(handle);
            workHandle?.OnWorkCallback();
        }

        protected void CloseHandle<T>(Action<T> onClosed = null)
            where T : WorkHandle
        {
            Action<ScheduleHandle> handler = null;
            if (onClosed is object)
            {
                handler = state => onClosed((T)state);
            }

            base.CloseHandle(handler);
        }
    }
}
