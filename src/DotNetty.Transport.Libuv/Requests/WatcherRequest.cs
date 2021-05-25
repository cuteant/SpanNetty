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

namespace DotNetty.Transport.Libuv.Requests
{
    using System;
    using System.Diagnostics;
    using DotNetty.Transport.Libuv.Native;

    public sealed class WatcherRequest : ScheduleRequest
    {
        internal static readonly uv_watcher_cb WatcherCallback = (h, s) => OnWatcherCallback(h, s);

        private readonly RequestContext _handle;
        private readonly bool _closeOnCallback;
        private Action<WatcherRequest, OperationException> _watcherCallback;

        internal WatcherRequest(uv_req_type requestType,
            Action<WatcherRequest, OperationException> watcherCallback,
            int size = 0, bool closeOnCallback = false)
            : base(requestType)
        {
            Debug.Assert(size >= 0);

            _watcherCallback = watcherCallback;
            _closeOnCallback = closeOnCallback;
            _handle = new RequestContext(requestType, size, this);
        }

        internal WatcherRequest(uv_req_type requestType,
            Action<WatcherRequest, OperationException> watcherCallback,
            Action<IntPtr> initializer, bool closeOnCallback = false)
            : base(requestType)
        {
            Debug.Assert(initializer is object);

            _watcherCallback = watcherCallback;
            _closeOnCallback = closeOnCallback;
            _handle = new RequestContext(requestType, initializer, this);
        }

        internal override IntPtr InternalHandle => _handle.Handle;

        private void OnWatcherCallback(OperationException error)
        {
            try
            {
                if (error is object)
                {
                    Log.RequestType_OnWatcherCallback_error(RequestType, InternalHandle, error);
                }

                _watcherCallback?.Invoke(this, error);

                if (_closeOnCallback)
                {
                    Dispose();
                }
            }
            catch (Exception exception)
            {
                Log.RequestType_OnWatcherCallback_error(RequestType, exception);
                throw;
            }
        }

        private static void OnWatcherCallback(IntPtr handle, int status)
        {
            if (handle == IntPtr.Zero) { return; }

            var request = RequestContext.GetTarget<WatcherRequest>(handle);
            OperationException error = null;
            if ((uint)status > SharedConstants.TooBigOrNegative) // < 0
            {
                error = NativeMethods.CreateError((uv_err_code)status);
            }

            request?.OnWatcherCallback(error);
        }

        protected override void Close()
        {
            _watcherCallback = null;
            _handle.Dispose();
        }
    }
}
