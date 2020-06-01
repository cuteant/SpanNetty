// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv
{
    using System;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv.Native;

    internal interface IServerNativeUnsafe
    {
        void Accept(RemoteConnection connection);

        void Accept(NativeHandle handle);
    }

    partial class TcpServerChannel<TServerChannel, TChannelFactory>
    {
        public sealed class TcpServerChannelUnsafe : NativeChannelUnsafe, IServerNativeUnsafe
        {
            static readonly Action<object, object> AcceptAction = OnAccept;

            public TcpServerChannelUnsafe() : base() // TcpServerChannel channel) : base(channel)
            {
            }

            public override IntPtr UnsafeHandle => _channel._tcpListener.Handle;

            // Connection callback from Libuv thread
            void IServerNativeUnsafe.Accept(RemoteConnection connection)
            {
                var ch = _channel;
                NativeHandle client = connection.Client;

                var connError = connection.Error;
                // If the AutoRead is false, reject the connection
                if (!ch._config.AutoRead || connError is object)
                {
                    if (connError is object)
                    {
                        if (Logger.InfoEnabled) Logger.AcceptClientConnectionFailed(connError);
                        _channel.Pipeline.FireExceptionCaught(connError);
                    }
                    try
                    {
                        client?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        if (Logger.WarnEnabled) Logger.FailedToDisposeAClientConnection(ex);
                    }
                    finally
                    {
                        client = null;
                    }
                }
                if (client is null)
                {
                    return;
                }

                if (ch.EventLoop is DispatcherEventLoop dispatcher)
                {
                    // Dispatch handle to other Libuv loop/thread
                    dispatcher.Dispatch(client);
                }
                else
                {
                    Accept((Tcp)client);
                }
            }

            // Called from other Libuv loop/thread received tcp handle from pipe
            void IServerNativeUnsafe.Accept(NativeHandle handle)
            {
                var ch = _channel;
                if (ch.EventLoop.InEventLoop)
                {
                    Accept((Tcp)handle);
                }
                else
                {
                    _channel.EventLoop.Execute(AcceptAction, this, handle);
                }
            }

            void Accept(Tcp tcp)
            {
                var ch = _channel;
                IChannelPipeline pipeline = ch.Pipeline;
                IRecvByteBufAllocatorHandle allocHandle = RecvBufAllocHandle;

                bool closed = false;
                Exception exception = null;
                try
                {
                    var tcpChannel = ch._channelFactory.CreateChannel(ch, tcp); // ## 苦竹 修改 ## new TcpChannel(ch, tcp);
                    ch.Pipeline.FireChannelRead(tcpChannel);
                    allocHandle.IncMessagesRead(1);
                }
                catch (ObjectDisposedException)
                {
                    closed = true;
                }
                catch (Exception ex)
                {
                    exception = ex;
                }

                allocHandle.ReadComplete();
                pipeline.FireChannelReadComplete();

                if (exception is object)
                {
                    pipeline.FireExceptionCaught(exception);
                }

                if (closed && ch.Open)
                {
                    CloseSafe();
                }
            }

            private static void OnAccept(object u, object e)
            {
                ((TcpServerChannelUnsafe)u).Accept((Tcp)e);
            }
        }
    }
}
