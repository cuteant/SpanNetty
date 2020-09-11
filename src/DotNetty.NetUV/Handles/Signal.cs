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

    public sealed class Signal : ScheduleHandle
    {
        internal static readonly uv_watcher_cb SignalCallback = (h, s) => OnSignalCallback(h, s);

        private Action<Signal, int> _signalCallback;

        internal Signal(LoopContext loop)
            : base(loop, uv_handle_type.UV_SIGNAL)
        { }

        public Signal Start(int signum, Action<Signal, int> callback)
        {
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            _signalCallback = callback;
            Validate();
            NativeMethods.SignalStart(InternalHandle, signum);

            return this;
        }

        private void OnSignalCallback(int signum)
        {
#if DEBUG
            if (Log.TraceEnabled)
            {
                Log.Trace("{} {} callback", HandleType, InternalHandle);
            }
#endif
            try
            {
                _signalCallback?.Invoke(this, signum);
            }
            catch (Exception exception)
            {
                Log.Handle_callback_error(HandleType, InternalHandle, exception);
                throw;
            }
        }

        private static void OnSignalCallback(IntPtr handle, int signum)
        {
            var signal = HandleContext.GetTarget<Signal>(handle);
            signal?.OnSignalCallback(signum);
        }

        public void Stop() => StopHandle();

        protected override void Close() => _signalCallback = null;

        public void CloseHandle(Action<Signal> onClosed = null)
        {
            Action<ScheduleHandle> handler = null;
            if (onClosed is object)
            {
                handler = state => onClosed((Signal)state);
            }

            base.CloseHandle(handler);
        }
    }
}
