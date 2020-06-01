// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.Net.Sockets;
    using System.Threading;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// <see cref="AbstractSocketChannel{TChannel, TUnsafe}"/> base class for <see cref="IChannel"/>s that operate on bytes.
    /// </summary>
    public abstract partial class AbstractSocketByteChannel<TChannel, TUnsafe> : AbstractSocketChannel<TChannel, TUnsafe>
        where TChannel : AbstractSocketByteChannel<TChannel, TUnsafe>
        where TUnsafe : AbstractSocketByteChannel<TChannel, TUnsafe>.SocketByteChannelUnsafe, new()
    {
        private static readonly string ExpectedTypes =
            $" (expected: {StringUtil.SimpleClassName<IByteBuffer>()})"; //+ ", " +

        // todo: FileRegion support        
        //typeof(FileRegion).Name + ')';

        private static readonly Action<object> FlushAction = OnFlushSync;
        private static readonly Action<object, object> ReadCompletedSyncCallback = OnReadCompletedSync;

        /// <summary>Create a new instance</summary>
        /// <param name="parent">the parent <see cref="IChannel"/> by which this instance was created. May be <c>null</c></param>
        /// <param name="socket">the underlying <see cref="Socket"/> on which it operates</param>
        protected AbstractSocketByteChannel(IChannel parent, Socket socket)
            : base(parent, socket)
        {
        }

        //protected override IChannelUnsafe NewUnsafe() => new SocketByteChannelUnsafe(this); ## 苦竹 屏蔽 ##

        protected override void ScheduleSocketRead()
        {
            var operation = ReadOperation;
            bool pending;
#if NETCOREAPP || NETSTANDARD
            pending = Socket.ReceiveAsync(operation);
#else
            if (ExecutionContext.IsFlowSuppressed())
            {
                pending = Socket.ReceiveAsync(operation);
            }
            else
            {
                using (ExecutionContext.SuppressFlow())
                {
                    pending = Socket.ReceiveAsync(operation);
                }
            }
#endif
            if (!pending)
            {
                // todo: potential allocation / non-static field?
                EventLoop.Execute(ReadCompletedSyncCallback, Unsafe, operation);
            }
        }

        static void OnReadCompletedSync(object u, object e) => ((TUnsafe)u).FinishRead((SocketChannelAsyncOperation<TChannel, TUnsafe>)e);

        private static void OnFlushSync(object channel)
        {
            ((TChannel)channel).Unsafe.InternalFlush0();
        }

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            int writeSpinCount = -1;

            while (true)
            {
                object msg = input.Current;
                if (msg is null)
                {
                    // Wrote all messages.
                    break;
                }

                if (msg is IByteBuffer buf)
                {
                    int readableBytes = buf.ReadableBytes;
                    if (0u >= (uint)readableBytes)
                    {
                        input.Remove();
                        continue;
                    }

                    bool scheduleAsync = false;
                    bool done = false;
                    long flushedAmount = 0;
                    if (writeSpinCount == -1)
                    {
                        writeSpinCount = Configuration.WriteSpinCount;
                    }
                    for (int i = writeSpinCount - 1; i >= 0; i--)
                    {
                        int localFlushedAmount = DoWriteBytes(buf);
                        if (0u >= (uint)localFlushedAmount) // todo: check for "sent less than attempted bytes" to avoid unnecessary extra doWriteBytes call?
                        {
                            scheduleAsync = true;
                            break;
                        }

                        flushedAmount += localFlushedAmount;
                        if (!buf.IsReadable())
                        {
                            done = true;
                            break;
                        }
                    }

                    input.Progress(flushedAmount);

                    if (done)
                    {
                        input.Remove();
                    }
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
                    else if (IncompleteWrite(scheduleAsync, PrepareWriteOperation(buf.UnreadMemory)))
#else
                    else if (IncompleteWrite(scheduleAsync, PrepareWriteOperation(buf.GetIoBuffer())))
#endif
                    {
                        break;
                    }
                } /*else if (msg is FileRegion) { todo: FileRegion support
                FileRegion region = (FileRegion) msg;
                bool done = region.transfered() >= region.count();
                bool scheduleAsync = false;

                if (!done) {
                    long flushedAmount = 0;
                    if (writeSpinCount == -1) {
                        writeSpinCount = config().getWriteSpinCount();
                    }

                    for (int i = writeSpinCount - 1; i >= 0; i--) {
                        long localFlushedAmount = doWriteFileRegion(region);
                        if (localFlushedAmount == 0) {
                            scheduleAsync = true;
                            break;
                        }

                        flushedAmount += localFlushedAmount;
                        if (region.transfered() >= region.count()) {
                            done = true;
                            break;
                        }
                    }

                    input.progress(flushedAmount);
                }

                if (done) {
                    input.remove();
                } else {
                    incompleteWrite(scheduleAsync);
                    break;
                }
            }*/
                else
                {
                    // Should not reach here.
                    ThrowHelper.ThrowInvalidOperationException();
                }
            }
        }

        protected override object FilterOutboundMessage(object msg)
        {
            if (msg is IByteBuffer)
            {
                return msg;
                //IByteBuffer buf = (IByteBuffer) msg;
                //if (buf.isDirect()) {
                //    return msg;
                //}

                //return newDirectBuffer(buf);
            }

            // todo: FileRegion support
            //if (msg is FileRegion) {
            //    return msg;
            //}

            return ThrowHelper.ThrowNotSupportedException_UnsupportedMsgType(msg);
        }

        protected bool IncompleteWrite(bool scheduleAsync, SocketChannelAsyncOperation<TChannel, TUnsafe> operation)
        {
            // Did not write completely.
            if (scheduleAsync)
            {
                SetState(StateFlags.WriteScheduled);
                bool pending;

#if NETCOREAPP || NETSTANDARD
                pending = Socket.SendAsync(operation);
#else
                if (ExecutionContext.IsFlowSuppressed())
                {
                    pending = Socket.SendAsync(operation);
                }
                else
                {
                    using (ExecutionContext.SuppressFlow())
                    {
                        pending = Socket.SendAsync(operation);
                    }
                }
#endif

                if (!pending)
                {
                    Unsafe.FinishWrite(operation); // ## 苦竹 修改 ## ((ISocketChannelUnsafe)this.Unsafe).FinishWrite(operation);
                }

                return pending;
            }
            else
            {
                // Schedule flush again later so other tasks can be picked up input the meantime
                EventLoop.Execute(FlushAction, this);

                return true;
            }
        }

        // todo: support FileRegion
        ///// <summary>
        // /// Write a {@link FileRegion}
        // *
        // /// @param region        the {@link FileRegion} from which the bytes should be written
        // /// @return amount       the amount of written bytes
        // /// </summary>
        //protected abstract long doWriteFileRegion(FileRegion region);

        /// <summary>
        /// Reads bytes into the given <see cref="IByteBuffer"/> and returns the number of bytes that were read.
        /// </summary>
        /// <param name="buf">The <see cref="IByteBuffer"/> to read bytes into.</param>
        /// <returns>The number of bytes that were read into the buffer.</returns>
        protected abstract int DoReadBytes(IByteBuffer buf);

        /// <summary>
        /// Writes bytes from the given <see cref="IByteBuffer"/> to the underlying <see cref="IChannel"/>.
        /// </summary>
        /// <param name="buf">The <see cref="IByteBuffer"/> from which the bytes should be written.</param>
        /// <returns>The number of bytes that were written from the buffer.</returns>
        protected abstract int DoWriteBytes(IByteBuffer buf);
    }
}