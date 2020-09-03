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

    public abstract class ServerStream : StreamHandle
    {
        internal const int DefaultBacklog = 128;

        internal static readonly uv_watcher_cb ConnectionCallback = (h, s) => OnConnectionCallback(h, s);

        private Action<StreamHandle, Exception> _connectionHandler;

        internal ServerStream(
            LoopContext loop,
            uv_handle_type handleType,
            params object[] args)
            : base(loop, handleType, args)
        { }

        protected internal abstract StreamHandle NewStream();

        public void StreamListen(Action<StreamHandle, Exception> onConnection, int backlog = DefaultBacklog)
        {
            if (onConnection is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onConnection); }
            if ((uint)(backlog - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(backlog, ExceptionArgument.backlog); }

            Validate();
            _connectionHandler = onConnection;
            try
            {
                NativeMethods.StreamListen(InternalHandle, backlog);
#if DEBUG
                if (Log.DebugEnabled)
                {
                    Log.Debug("Stream {} {} listening, backlog = {}", HandleType, InternalHandle, backlog);
                }
#endif
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        protected override void Close()
        {
            _connectionHandler = null;
            base.Close();
        }

        private static void OnConnectionCallback(IntPtr handle, int status)
        {
            var server = HandleContext.GetTarget<ServerStream>(handle);
            if (server is null) { return; }

            StreamHandle client = null;
            Exception error = null;
            try
            {
                if ((uint)status > SharedConstants.TooBigOrNegative) // < 0
                {
                    error = NativeMethods.CreateError((uv_err_code)status);
                }
                else
                {
                    client = server.NewStream();
                }

                server._connectionHandler(client, error);
            }
            catch
            {
                client?.Dispose();
                throw;
            }
        }
    }
}
