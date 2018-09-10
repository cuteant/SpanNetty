// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Net.Sockets;
    using System.Threading;

    /// <summary>
    /// The default <see cref="IServerSocketChannelConfiguration"/> implementation.
    /// </summary>
    public class DefaultServerSocketChannelConfig : DefaultChannelConfiguration, IServerSocketChannelConfiguration
    {
        protected readonly Socket Socket;
        int backlog = 200; //todo: NetUtil.SOMAXCONN;

        /// <summary>
        ///     Creates a new instance.
        /// </summary>
        public DefaultServerSocketChannelConfig(IServerSocketChannel channel, Socket socket)
            : base(channel)
        {
            if (null == socket) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.socket); }

            this.Socket = socket;
        }

        public override T GetOption<T>(ChannelOption<T> option)
        {
            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                return (T)(object)this.ReceiveBufferSize;
            }
            if (ChannelOption.SoReuseaddr.Equals(option))
            {
                return (T)(object)this.ReuseAddress;
            }
            if (ChannelOption.SoBacklog.Equals(option))
            {
                return (T)(object)this.Backlog;
            }
            if (ChannelOption.SoLinger.Equals(option))
            {
                return (T)(object)this.Linger;
            }

            return base.GetOption(option);
        }

        public override bool SetOption<T>(ChannelOption<T> option, T value)
        {
            this.Validate(option, value);

            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                this.ReceiveBufferSize = (int)(object)value;
            }
            else if (ChannelOption.SoReuseaddr.Equals(option))
            {
                this.ReuseAddress = (bool)(object)value;
            }
            else if (ChannelOption.SoBacklog.Equals(option))
            {
                this.Backlog = (int)(object)value;
            }
            else if (ChannelOption.SoLinger.Equals(option))
            {
                this.Linger = (int)(object)value;
            }
            else
            {
                return base.SetOption(option, value);
            }

            return true;
        }

        public bool ReuseAddress
        {
            get
            {
                try
                {
                    return (int)this.Socket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress) != 0;
                }
                catch (ObjectDisposedException ex)
                {
                    throw new ChannelException(ex);
                }
                catch (SocketException ex)
                {
                    throw new ChannelException(ex);
                }
            }
            set
            {
                try
                {
                    this.Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, value ? 1 : 0);
                }
                catch (ObjectDisposedException ex)
                {
                    throw new ChannelException(ex);
                }
                catch (SocketException ex)
                {
                    throw new ChannelException(ex);
                }
            }
        }

        public int ReceiveBufferSize
        {
            get
            {
                try
                {
                    return this.Socket.ReceiveBufferSize;
                }
                catch (ObjectDisposedException ex)
                {
                    throw new ChannelException(ex);
                }
                catch (SocketException ex)
                {
                    throw new ChannelException(ex);
                }
            }
            set
            {
                try
                {
                    this.Socket.ReceiveBufferSize = value;
                }
                catch (ObjectDisposedException ex)
                {
                    throw new ChannelException(ex);
                }
                catch (SocketException ex)
                {
                    throw new ChannelException(ex);
                }
            }
        }

        public int Backlog
        {
            get { return Volatile.Read(ref this.backlog); }
            set
            {
                if (value < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(value, ExceptionArgument.value); }

                Interlocked.Exchange(ref this.backlog, value);
            }
        }

        public int Linger
        {
            get
            {
                try
                {
                    LingerOption lingerState = this.Socket.LingerState;
                    return lingerState.Enabled ? lingerState.LingerTime : -1;
                }
                catch (ObjectDisposedException ex)
                {
                    throw new ChannelException(ex);
                }
                catch (SocketException ex)
                {
                    throw new ChannelException(ex);
                }
            }
            set
            {
                try
                {
                    if (value < 0)
                    {
                        this.Socket.LingerState = new LingerOption(false, 0);
                    }
                    else
                    {
                        this.Socket.LingerState = new LingerOption(true, value);
                    }
                }
                catch (ObjectDisposedException ex)
                {
                    throw new ChannelException(ex);
                }
                catch (SocketException ex)
                {
                    throw new ChannelException(ex);
                }
            }
        }
    }
}