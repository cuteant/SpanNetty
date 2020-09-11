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

namespace DotNetty.NetUV.Requests
{
    using System;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;

    public sealed class Work : ScheduleRequest
    {
        internal static readonly uv_work_cb WorkCallback = h => OnWorkCallback(h);
        internal static readonly uv_watcher_cb AfterWorkCallback = (h, s) => OnAfterWorkCallback(h, s);

        private readonly RequestContext _handle;
        private Action<Work> _workCallback;
        private Action<Work> _afterWorkCallback;

        internal Work(
            LoopContext loop,
            Action<Work> workCallback,
            Action<Work> afterWorkCallback)
            : base(uv_req_type.UV_WORK)
        {
            if (loop is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.loop); }
            if (workCallback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.workCallback); }

            _workCallback = workCallback;
            _afterWorkCallback = afterWorkCallback;

            try
            {
                _handle = new RequestContext(uv_req_type.UV_WORK, 0, this);
                NativeMethods.QueueWork(loop.Handle, _handle.Handle);
            }
            catch
            {
                _handle.Dispose();
                throw;
            }
        }

        public bool TryCancel() => Cancel();

        internal override IntPtr InternalHandle => _handle.Handle;

        private void OnWorkCallback()
        {
            try
            {
                _workCallback?.Invoke(this);
            }
            catch (Exception exception)
            {
                Log.RequestType_work_callback_error(RequestType, exception);
                throw;
            }
        }

        private static void OnWorkCallback(IntPtr handle)
        {
            var request = RequestContext.GetTarget<Work>(handle);
            request?.OnWorkCallback();
        }

        private void OnAfterWorkCallback()
        {
            try
            {
                _afterWorkCallback?.Invoke(this);
            }
            catch (Exception exception)
            {
                Log.RequestType_after_callback_error(RequestType, exception);
                throw;
            }
        }

        private static void OnAfterWorkCallback(IntPtr handle, int status)
        {
            var request = RequestContext.GetTarget<Work>(handle);
            request?.OnAfterWorkCallback();
        }

        protected override void Close()
        {
            _workCallback = null;
            _afterWorkCallback = null;
            _handle.Dispose();
        }
    }
}
