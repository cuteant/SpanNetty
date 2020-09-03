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

    internal sealed class StreamShutdown : IDisposable
    {
        private readonly WatcherRequest _watcherRequest;
        private StreamHandle _streamHandle;
        private Action<StreamHandle, Exception> _completedAction;

        internal StreamShutdown(StreamHandle streamHandle, Action<StreamHandle, Exception> completedAction)
        {
            if (streamHandle is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.streamHandle); }

            streamHandle.Validate();

            _streamHandle = streamHandle;
            _completedAction = completedAction;

            _watcherRequest = new WatcherRequest(
                uv_req_type.UV_SHUTDOWN,
                (r, e) => OnCompleted(e),
                h => NativeMethods.Shutdown(h, _streamHandle.InternalHandle),
                closeOnCallback: true);
        }

        internal static void Completed(Action<StreamHandle, Exception> completion, StreamHandle handle, Exception error)
        {
            if (handle is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.handle); }

            try
            {
                completion?.Invoke(handle, error);
            }
            catch (Exception exception)
            {
                ScheduleRequest.Log.UV_SHUTDOWN_callback_error(exception);
            }
        }

        private void OnCompleted(/*WatcherRequest request, */Exception error)
        {
            Completed(_completedAction, _streamHandle, error);
            Dispose();
        }

        public void Dispose()
        {
            _streamHandle = null;
            _completedAction = null;
            _watcherRequest.Dispose();
        }
    }
}
