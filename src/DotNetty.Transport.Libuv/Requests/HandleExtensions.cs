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
    using System.Net;
    using DotNetty.Transport.Libuv.Requests;

    public static class HandleExtensions
    {
        public static Pipe Listen(this Pipe pipe,
            string name, Action<Pipe, Exception> onConnection, int backlog = ServerStream.DefaultBacklog)
        {
            if (pipe is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.pipe); }
            if (string.IsNullOrEmpty(name)) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.name); }
            if (onConnection is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onConnection); }
            if ((uint)(backlog - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(backlog, ExceptionArgument.backlog); }

            pipe.Bind(name);
            pipe.Listen(onConnection, backlog);

            return pipe;
        }

        public static Pipe ConnectTo(this Pipe pipe,
            string remoteName, Action<Pipe, Exception> connectedAction)
        {
            if (pipe is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.pipe); }
            if (string.IsNullOrEmpty(remoteName)) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.remoteName); }
            if (connectedAction is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connectedAction); }

            PipeConnect request = null;
            try
            {
                request = new PipeConnect(pipe, remoteName, connectedAction);
            }
            catch (Exception)
            {
                request?.Dispose();
                throw;
            }

            return pipe;
        }

        public static Tcp Listen(this Tcp tcp,
            IPEndPoint localEndPoint, Action<Tcp, Exception> onConnection, int backlog = ServerStream.DefaultBacklog, bool dualStack = false)
        {
            if (tcp is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.tcp); }
            if (localEndPoint is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.localEndPoint); }
            if (onConnection is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onConnection); }

            tcp.Bind(localEndPoint, dualStack);
            tcp.Listen(onConnection, backlog);

            return tcp;
        }

        public static Tcp ConnectTo(this Tcp tcp,
            IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, Action<Tcp, Exception> connectedAction, bool dualStack = false)
        {
            if (tcp is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.tcp); }
            if (localEndPoint is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.localEndPoint); }
            if (remoteEndPoint is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.remoteEndPoint); }
            if (connectedAction is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connectedAction); }

            tcp.Bind(localEndPoint, dualStack);
            tcp.ConnectTo(remoteEndPoint, connectedAction);

            return tcp;
        }

        public static Tcp ConnectTo(this Tcp tcp,
            IPEndPoint remoteEndPoint, Action<Tcp, Exception> connectedAction, bool dualStack = false)
        {
            if (tcp is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.tcp); }
            if (remoteEndPoint is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.remoteEndPoint); }
            if (connectedAction is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.connectedAction); }

            TcpConnect request = null;
            try
            {
                request = new TcpConnect(tcp, remoteEndPoint, connectedAction);
            }
            catch (Exception)
            {
                request?.Dispose();
                throw;
            }

            return tcp;
        }

        public static Tcp Bind(this Tcp tcp,
            IPEndPoint localEndPoint, Action<Tcp, IStreamReadCompletion> onRead, bool dualStack = false)
        {
            if (tcp is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.tcp); }
            if (localEndPoint is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.localEndPoint); }
            if (onRead is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onRead); }

            tcp.Bind(localEndPoint, dualStack);
            tcp.OnRead(onRead);

            return tcp;
        }

        public static Udp ReceiveStart(this Udp udp,
            IPEndPoint localEndPoint, Action<Udp, IDatagramReadCompletion> receiveAction, bool dualStack = false)
        {
            if (udp is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.udp); }
            if (localEndPoint is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.localEndPoint); }
            if (receiveAction is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.receiveAction); }

            udp.Bind(localEndPoint, dualStack);
            udp.OnReceive(receiveAction);
            udp.ReceiveStart();

            return udp;
        }

        public static Udp ReceiveStart(this Udp udp, Action<Udp, IDatagramReadCompletion> receiveAction)
        {
            if (udp is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.udp); }
            if (receiveAction is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.receiveAction); }

            udp.OnReceive(receiveAction);
            udp.ReceiveStart();

            return udp;
        }
    }
}
