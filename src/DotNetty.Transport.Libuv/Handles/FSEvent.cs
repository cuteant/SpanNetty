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

    [Flags]
    public enum FSEventType
    {
        Rename = 1,
        Change = 2
    }

    [Flags]
    public enum FSEventMask
    {
        None = 0,

        /*
        * By default, if the fs event watcher is given a directory name, we will
        * watch for all events in that directory. This flags overrides this behavior
        * and makes fs_event report only changes to the directory entry itself. This
        * flag does not affect individual files watched.
        * This flag is currently not implemented yet on any backend.
        */
        Watchentry = 1,

        /*
        * By default uv_fs_event will try to use a kernel interface such as inotify
        * or kqueue to detect events. This may not work on remote filesystems such
        * as NFS mounts. This flag makes fs_event fall back to calling stat() on a
        * regular interval.
        * This flag is currently not implemented yet on any backend.
        */
        Status = 2,

        /*
        * By default, event watcher, when watching directory, is not registering
        * (is ignoring) changes in it's subdirectories.
        * This flag will override this behaviour on platforms that support it.
        */
        Recursive = 4
    };

    public readonly struct FileSystemEvent
    {
        internal FileSystemEvent(string fileName, FSEventType eventType, Exception error)
        {
            FileName = fileName;
            EventType = eventType;
            Error = error;
        }

        public string FileName { get; }

        public FSEventType EventType { get; }

        public Exception Error { get; }
    }

    public sealed class FSEvent : ScheduleHandle<FSEvent>
    {
        internal static readonly uv_fs_event_cb FSEventCallback = (h, f, e, s) => OnFSEventCallback(h, f, e, s);

        private Action<FSEvent, FileSystemEvent> _eventCallback;

        internal FSEvent(LoopContext loop)
            : base(loop, uv_handle_type.UV_FS_EVENT)
        { }

        public FSEvent Start(string path,
            Action<FSEvent, FileSystemEvent> callback,
            FSEventMask mask = FSEventMask.None)
        {
            if (string.IsNullOrEmpty(path)) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.path); }
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            Validate();
            _eventCallback = callback;
            NativeMethods.FSEventStart(InternalHandle, path, mask);

            return this;
        }

        public string GetPath()
        {
            Validate();
            return NativeMethods.FSEventGetPath(InternalHandle);
        }

        private void OnFSEventCallback(string fileName, int events, int status)
        {
#if DEBUG
            if (Log.TraceEnabled)
            {
                Log.Trace("{} {} callback", HandleType, InternalHandle);
            }
#endif
            try
            {
                OperationException error = null;
                if ((uint)status > SharedConstants.TooBigOrNegative)
                {
                    error = NativeMethods.CreateError((uv_err_code)status);
                }

                var fileSystemEvent = new FileSystemEvent(fileName, (FSEventType)events, error);
                _eventCallback?.Invoke(this, fileSystemEvent);
            }
            catch (Exception exception)
            {
                Log.Handle_callback_error(HandleType, InternalHandle, exception);
                throw;
            }
        }

        private static void OnFSEventCallback(IntPtr handle, string fileName, int events, int status)
        {
            var fsEvent = HandleContext.GetTarget<FSEvent>(handle);
            fsEvent?.OnFSEventCallback(fileName, events, status);
        }

        public void Stop() => StopHandle();

        protected override void Close() => _eventCallback = null;
    }
}
