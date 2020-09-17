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

    public readonly struct FSPollStatus
    {
        internal FSPollStatus(FileStatus previous, FileStatus current, Exception error)
        {
            Previous = previous;
            Current = current;
            Error = error;
        }

        public FileStatus Previous { get; }

        public FileStatus Current { get; }

        public Exception Error { get; }
    }

    public sealed class FSPoll : ScheduleHandle<FSPoll>
    {
        internal static readonly uv_fs_poll_cb FSPollCallback = OnFSPollCallback;
        private Action<FSPoll, FSPollStatus> _pollCallback;

        internal FSPoll(LoopContext loop)
            : base(loop, uv_handle_type.UV_FS_POLL)
        { }

        public FSPoll Start(string path, int interval, Action<FSPoll, FSPollStatus> callback)
        {
            if (string.IsNullOrEmpty(path)) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.path); }
            if ((uint)(interval - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(interval, ExceptionArgument.interval); }
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            Validate();
            _pollCallback = callback;
            NativeMethods.FSPollStart(InternalHandle, path, interval);

            return this;
        }

        public string GetPath()
        {
            Validate();
            return NativeMethods.FSPollGetPath(InternalHandle);
        }

        private void OnFSPollCallback(int status, ref uv_stat_t prev, ref uv_stat_t curr)
        {
#if DEBUG
            if (Log.TraceEnabled)
            {
                Log.Trace("{} {} callback", HandleType, InternalHandle);
            }
#endif
            try
            {
                FileStatus previous = null;
                FileStatus current = null;
                OperationException error = null;
                if (SharedConstants.TooBigOrNegative >= (uint)status)
                {
                    previous = (FileStatus)prev;
                    current = (FileStatus)curr;
                }
                else
                {
                    error = NativeMethods.CreateError((uv_err_code)status);
                }

                _pollCallback?.Invoke(this, new FSPollStatus(previous, current, error));
            }
            catch (Exception exception)
            {
                Log.Handle_callback_error(HandleType, InternalHandle, exception);
                throw;
            }
        }

        private static void OnFSPollCallback(IntPtr handle, int status, ref uv_stat_t prev, ref uv_stat_t curr)
        {
            var fsPoll = HandleContext.GetTarget<FSPoll>(handle);
            fsPoll?.OnFSPollCallback(status, ref prev, ref curr);
        }

        public void Stop() => StopHandle();

        protected override void Close() => _pollCallback = null;
    }
}
