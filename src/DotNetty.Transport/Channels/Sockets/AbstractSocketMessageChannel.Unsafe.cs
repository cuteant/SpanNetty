// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Sockets;

    partial class AbstractSocketMessageChannel<TChannel, TUnsafe>
    {
        public class SocketMessageUnsafe : AbstractSocketUnsafe
        {
            readonly List<object> _readBuf = new List<object>();

            public SocketMessageUnsafe() // (AbstractSocketMessageChannel channel)
                : base() //channel)
            {
            }

            //new AbstractSocketMessageChannel Channel => (AbstractSocketMessageChannel)this.channel;

            public override void FinishRead(SocketChannelAsyncOperation<TChannel, TUnsafe> operation)
            {
                Debug.Assert(_channel.EventLoop.InEventLoop);

                var ch = _channel;
                if (0u >= (uint)(ch.ResetState(StateFlags.ReadScheduled) & StateFlags.Active))
                {
                    return; // read was signaled as a result of channel closure
                }
                IChannelConfiguration config = ch.Configuration;

                IChannelPipeline pipeline = ch.Pipeline;
                IRecvByteBufAllocatorHandle allocHandle = ch.Unsafe.RecvBufAllocHandle;
                allocHandle.Reset(config);

                bool closed = false;
                Exception exception = null;
                try
                {
                    try
                    {
                        do
                        {
                            int localRead = ch.DoReadMessages(_readBuf);
                            uint uLocalRead = (uint)localRead;
                            if (0u >= uLocalRead)
                            {
                                break;
                            }
                            if (uLocalRead > SharedConstants.TooBigOrNegative) // localRead < 0
                            {
                                closed = true;
                                break;
                            }

                            allocHandle.IncMessagesRead(localRead);
                        }
                        while (allocHandle.ContinueReading());
                    }
                    catch (Exception t)
                    {
                        exception = t;
                    }
                    int size = _readBuf.Count;
                    for (int i = 0; i < size; i++)
                    {
                        ch.ReadPending = false;
                        pipeline.FireChannelRead(_readBuf[i]);
                    }

                    _readBuf.Clear();
                    allocHandle.ReadComplete();
                    pipeline.FireChannelReadComplete();

                    if (exception is object)
                    {
                        if (exception is SocketException asSocketException && asSocketException.SocketErrorCode != SocketError.TryAgain) // todo: other conditions for not closing message-based socket?
                        {
                            // ServerChannel should not be closed even on SocketException because it can often continue
                            // accepting incoming connections. (e.g. too many open files)
                            closed = !(ch is IServerChannel);
                        }

                        pipeline.FireExceptionCaught(exception);
                    }

                    if (closed)
                    {
                        if (ch.Open)
                        {
                            Close(VoidPromise());
                        }
                    }
                }
                finally
                {
                    // Check if there is a readPending which was not processed yet.
                    // This could be for two reasons:
                    // /// The user called Channel.read() or ChannelHandlerContext.read() in channelRead(...) method
                    // /// The user called Channel.read() or ChannelHandlerContext.read() in channelReadComplete(...) method
                    //
                    // See https://github.com/netty/netty/issues/2254
                    if (!closed && (ch.ReadPending || config.AutoRead))
                    {
                        ch.DoBeginRead();
                    }
                }
            }
        }
    }
}