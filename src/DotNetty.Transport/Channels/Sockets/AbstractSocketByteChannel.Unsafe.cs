// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Net.Sockets;
    using DotNetty.Buffers;

    partial class AbstractSocketByteChannel<TChannel, TUnsafe>
    {
        public partial class SocketByteChannelUnsafe : AbstractSocketUnsafe
        {
            public SocketByteChannelUnsafe() //(AbstractSocketByteChannel channel)
                : base() //channel)
            {
            }

            //new AbstractSocketByteChannel Channel => (AbstractSocketByteChannel)this.channel;

            void CloseOnRead()
            {
                var ch = _channel;
                ch.ShutdownInput();
                if (ch.Open)
                {
                    // todo: support half-closure
                    //if (bool.TrueString.Equals(this.channel.Configuration.getOption(ChannelOption.ALLOW_HALF_CLOSURE))) {
                    //    key.interestOps(key.interestOps() & ~readInterestOp);
                    //    this.channel.Pipeline.FireUserEventTriggered(ChannelInputShutdownEvent.INSTANCE);
                    //} else {
                    Close(VoidPromise());
                    //}
                }
            }

            void HandleReadException(IChannelPipeline pipeline, IByteBuffer byteBuf, Exception cause, bool close,
                IRecvByteBufAllocatorHandle allocHandle)
            {
                if (byteBuf is object)
                {
                    if (byteBuf.IsReadable())
                    {
                        _channel.ReadPending = false;
                        pipeline.FireChannelRead(byteBuf);
                    }
                    else
                    {
                        byteBuf.Release();
                    }
                }
                allocHandle.ReadComplete();
                pipeline.FireChannelReadComplete();
                pipeline.FireExceptionCaught(cause);
                if (close || cause is SocketException)
                {
                    CloseOnRead();
                }
            }

            public override void FinishRead(SocketChannelAsyncOperation<TChannel, TUnsafe> operation)
            {
                var ch = _channel;
                if (0u >= (uint)(ch.ResetState(StateFlags.ReadScheduled) & StateFlags.Active))
                {
                    return; // read was signaled as a result of channel closure
                }
                IChannelConfiguration config = ch.Configuration;
                IChannelPipeline pipeline = ch.Pipeline;
                IByteBufferAllocator allocator = config.Allocator;
                IRecvByteBufAllocatorHandle allocHandle = RecvBufAllocHandle;
                allocHandle.Reset(config);

                IByteBuffer byteBuf = null;
                bool close = false;
                try
                {
                    operation.Validate();

                    do
                    {
                        byteBuf = allocHandle.Allocate(allocator);
                        //int writable = byteBuf.WritableBytes;
                        allocHandle.LastBytesRead = ch.DoReadBytes(byteBuf);
                        if (allocHandle.LastBytesRead <= 0)
                        {
                            // nothing was read -> release the buffer.
                            byteBuf.Release();
                            byteBuf = null;
                            close = allocHandle.LastBytesRead < 0;
                            if (close)
                            {
                                // There is nothing left to read as we received an EOF.
                                ch.ReadPending = false;
                            }
                            break;
                        }

                        allocHandle.IncMessagesRead(1);
                        ch.ReadPending = false;

                        pipeline.FireChannelRead(byteBuf);
                        byteBuf = null;
                    }
                    while (allocHandle.ContinueReading());

                    allocHandle.ReadComplete();
                    pipeline.FireChannelReadComplete();

                    if (close)
                    {
                        CloseOnRead();
                    }
                }
                catch (Exception t)
                {
                    HandleReadException(pipeline, byteBuf, t, close, allocHandle);
                }
                finally
                {
                    // Check if there is a readPending which was not processed yet.
                    // This could be for two reasons:
                    // /// The user called Channel.read() or ChannelHandlerContext.read() input channelRead(...) method
                    // /// The user called Channel.read() or ChannelHandlerContext.read() input channelReadComplete(...) method
                    //
                    // See https://github.com/netty/netty/issues/2254
                    if (!close && (ch.ReadPending || config.AutoRead))
                    {
                        ch.DoBeginRead();
                    }
                }
            }

            internal void InternalFlush0() => Flush0();
        }
    }
}