/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) The DotNetty Project (Microsoft). All rights reserved.
 *
 *   https://github.com/azure/dotnetty
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Internal;

    sealed class TcpChannelConfig : DefaultChannelConfiguration
    {
        readonly Dictionary<ChannelOption, int> _options;

        public TcpChannelConfig(IChannel channel) : base(channel)
        {
            // 
            // Note:
            // Libuv automatically set SO_REUSEADDR by default on Unix but not on Windows after bind. 
            // For details:
            // https://github.com/libuv/libuv/blob/fd049399aa4ed8495928e375466970d98cb42e17/src/unix/tcp.c#L166
            // https://github.com/libuv/libuv/blob/2b32e77bb6f41e2786168ec0f32d1f0fcc78071b/src/win/tcp.c#L286
            // 
            // 

            _options = new Dictionary<ChannelOption, int>(5, ChannelOptionComparer.Default)
            {
                { ChannelOption.TcpNodelay, 1 } // TCP_NODELAY by default
            };
        }

        public override T GetOption<T>(ChannelOption<T> option)
        {
            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                return (T)(object)GetReceiveBufferSize();
            }
            if (ChannelOption.SoSndbuf.Equals(option))
            {
                return (T)(object)GetSendBufferSize();
            }
            if (ChannelOption.TcpNodelay.Equals(option))
            {
                return (T)(object)GetTcpNoDelay();
            }
            if (ChannelOption.SoKeepalive.Equals(option))
            {
                return (T)(object)GetKeepAlive();
            }
            if (ChannelOption.SoReuseaddr.Equals(option))
            {
                return (T)(object)GetReuseAddress();
            }

            return base.GetOption(option);
        }

        public override bool SetOption<T>(ChannelOption<T> option, T value)
        {
            if (base.SetOption(option, value))
            {
                return true;
            }

            if (ChannelOption.SoRcvbuf.Equals(option))
            {
                SetReceiveBufferSize((int)(object)value);
            }
            else if (ChannelOption.SoSndbuf.Equals(option))
            {
                SetSendBufferSize((int)(object)value);
            }
            else if (ChannelOption.TcpNodelay.Equals(option))
            {
                SetTcpNoDelay((bool)(object)value);
            }
            else if (ChannelOption.SoKeepalive.Equals(option))
            {
                SetKeepAlive((bool)(object)value);
            }
            else if (ChannelOption.SoReuseaddr.Equals(option))
            {
                SetReuseAddress((bool)(object)value);
            }
            else
            {
                return false;
            }

            return true;
        }

        int GetReceiveBufferSize()
        {
            try
            {
                var channel = (INativeChannel)Channel;
                var tcp = (Tcp)channel.GetHandle();
                return tcp.GetReceiveBufferSize();
            }
            catch (ObjectDisposedException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
            catch (OperationException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
            return -1;
        }

        void SetReceiveBufferSize(int value)
        {
            var channel = (INativeChannel)Channel;
            if (!channel.IsBound)
            {
                // Defer until bound
                if (!_options.ContainsKey(ChannelOption.SoRcvbuf))
                {
                    _options.Add(ChannelOption.SoRcvbuf, value);
                }
                else
                {
                    _options[ChannelOption.SoRcvbuf] = value;
                }
            }
            else
            {
                SetReceiveBufferSize((Tcp)channel.GetHandle(), value);
            }
        }

        static void SetReceiveBufferSize(Tcp tcp, int value)
        {
            try
            {
                _ = tcp.SetReceiveBufferSize(value);
            }
            catch (ObjectDisposedException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
            catch (OperationException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
        }

        int GetSendBufferSize()
        {
            try
            {
                var channel = (INativeChannel)Channel;
                var tcp = (Tcp)channel.GetHandle();
                return tcp.GetSendBufferSize();
            }
            catch (ObjectDisposedException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
            catch (OperationException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
            return -1;
        }

        void SetSendBufferSize(int value)
        {
            var channel = (INativeChannel)Channel;
            if (!channel.IsBound)
            {
                // Defer until bound
                if (!_options.ContainsKey(ChannelOption.SoSndbuf))
                {
                    _options.Add(ChannelOption.SoSndbuf, value);
                }
                else
                {
                    _options[ChannelOption.SoSndbuf] = value;
                }
            }
            else
            {
                SetSendBufferSize((Tcp)channel.GetHandle(), value);
            }
        }

        static void SetSendBufferSize(Tcp tcp, int value)
        {
            try
            {
                _ = tcp.SetSendBufferSize(value);
            }
            catch (ObjectDisposedException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
            catch (OperationException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
        }

        bool GetTcpNoDelay()
        {
            if (_options.TryGetValue(ChannelOption.TcpNodelay, out int value))
            {
                return value != 0;
            }
            return false;
        }

        void SetTcpNoDelay(bool value)
        {
            var channel = (INativeChannel)Channel;
            if (!channel.IsBound)
            {
                int optionValue = value ? 1 : 0;
                // Defer until bound
                if (!_options.ContainsKey(ChannelOption.TcpNodelay))
                {
                    _options.Add(ChannelOption.TcpNodelay, optionValue);
                }
                else
                {
                    _options[ChannelOption.TcpNodelay] = optionValue;
                }
            }
            else
            {
                SetTcpNoDelay((Tcp)channel.GetHandle(), value);
            }
        }

        static void SetTcpNoDelay(Tcp tcp, bool value)
        {
            try
            {
                tcp.NoDelay(value);
            }
            catch (ObjectDisposedException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
            catch (OperationException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
        }

        bool GetKeepAlive()
        {
            if (_options.TryGetValue(ChannelOption.SoKeepalive, out int value))
            {
                return value != 0;
            }
            return true;
        }

        void SetKeepAlive(bool value)
        {
            var channel = (INativeChannel)Channel;
            if (!channel.IsBound)
            {
                int optionValue = value ? 1 : 0;
                // Defer until bound
                if (!_options.ContainsKey(ChannelOption.SoKeepalive))
                {
                    _options.Add(ChannelOption.SoKeepalive, optionValue);
                }
                else
                {
                    _options[ChannelOption.SoKeepalive] = optionValue;
                }
            }
            else
            {
                SetKeepAlive((Tcp)channel.GetHandle(), value);
            }
        }

        static void SetKeepAlive(Tcp tcp, bool value)
        {
            try
            {
                tcp.KeepAlive(value, 1 /* Delay in seconds to take effect*/);
            }
            catch (ObjectDisposedException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
            catch (OperationException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
        }

        bool GetReuseAddress()
        {
            try
            {
                var channel = (INativeChannel)Channel;
                var tcpListener = (Tcp)channel.GetHandle();
                return PlatformApis.GetReuseAddress(tcpListener);
            }
            catch (ObjectDisposedException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
            catch (SocketException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
            return false;
        }

        void SetReuseAddress(bool value)
        {
            int optionValue = value ? 1 : 0;
            var channel = (INativeChannel)Channel;
            if (!channel.IsBound)
            {
                // Defer until registered
                if (!_options.ContainsKey(ChannelOption.SoReuseaddr))
                {
                    _options.Add(ChannelOption.SoReuseaddr, optionValue);
                }
                else
                {
                    _options[ChannelOption.SoReuseaddr] = optionValue;
                }
            }
            else
            {
                SetReuseAddress((Tcp)channel.GetHandle(), optionValue);
            }
        }

        static void SetReuseAddress(Tcp tcp, int value)
        {
            try
            {
                PlatformApis.SetReuseAddress(tcp, value);
            }
            catch (ObjectDisposedException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
            catch (SocketException ex)
            {
                ThrowHelper.ThrowChannelException(ex);
            }
        }

        // Libuv tcp handle requires socket to be created before
        // applying options. When SetOption is called, the socket
        // is not yet created, it is deferred until channel register.
        internal void Apply()
        {
            Debug.Assert(_options.Count <= 5);

            var channel = (INativeChannel)Channel;
            var tcp = (Tcp)channel.GetHandle();
            foreach (ChannelOption option in _options.Keys)
            {
                if (ChannelOption.SoRcvbuf.Equals(option))
                {
                    SetReceiveBufferSize(tcp, _options[ChannelOption.SoRcvbuf]);
                }
                else if (ChannelOption.SoSndbuf.Equals(option))
                {
                    SetSendBufferSize(tcp, _options[ChannelOption.SoSndbuf]);
                }
                else if (ChannelOption.TcpNodelay.Equals(option))
                {
                    SetTcpNoDelay(tcp, _options[ChannelOption.TcpNodelay] != 0);
                }
                else if (ChannelOption.SoKeepalive.Equals(option))
                {
                    SetKeepAlive(tcp, _options[ChannelOption.SoKeepalive] != 0);
                }
                else if (ChannelOption.SoReuseaddr.Equals(option))
                {
                    SetReuseAddress(tcp, _options[ChannelOption.SoReuseaddr]);
                }
                else
                {
                    ThrowHelper.ThrowChannelException(option);
                }
            }
        }
    }
}
