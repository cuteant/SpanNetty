
namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
#if NETCOREAPP || NETSTANDARD_2_0_GREATER
    using System.Runtime.InteropServices;
#endif

    partial class TlsHandler
    {
        private Task _lastContextWriteTask;

        public override void Write(IChannelHandlerContext context, object message, IPromise promise)
        {
            if (message is IByteBuffer buf)
            {
                if (_pendingUnencryptedWrites is object)
                {
                    _pendingUnencryptedWrites.Add(buf, promise);
                }
                else
                {
                    ReferenceCountUtil.SafeRelease(buf);
                    _ = promise.TrySetException(NewPendingWritesNullException());
                }
                return;
            }
            ReferenceCountUtil.SafeRelease(message);
            _ = promise.TrySetException(ThrowHelper.GetUnsupportedMessageTypeException(message));
        }

        public override void Flush(IChannelHandlerContext context)
        {
            try
            {
                WrapAndFlush(context);
            }
            catch (Exception cause)
            {
                // Fail pending writes.
                HandleFailure(cause);
                ExceptionDispatchInfo.Capture(cause).Throw();
            }
        }

        private void Flush(IChannelHandlerContext ctx, IPromise promise)
        {
            if (_pendingUnencryptedWrites is object)
            {
                _pendingUnencryptedWrites.Add(Unpooled.Empty, promise);
            }
            else
            {
                _ = promise.TrySetException(NewPendingWritesNullException());
            }
            Flush(ctx);
        }

        private void WrapAndFlush(IChannelHandlerContext context)
        {
            if (_pendingUnencryptedWrites.IsEmpty)
            {
                // It's important to NOT use a voidPromise here as the user
                // may want to add a ChannelFutureListener to the ChannelPromise later.
                //
                // See https://github.com/netty/netty/issues/3364
                _pendingUnencryptedWrites.Add(Unpooled.Empty, context.NewPromise());
            }

            if (!EnsureAuthenticated(context))
            {
                State |= TlsHandlerState.FlushedBeforeHandshake;
                return;
            }

            try
            {
                Wrap(context);
            }
            finally
            {
                // We may have written some parts of data before an exception was thrown so ensure we always flush.
                _ = context.Flush();
            }
        }

        private void Wrap(IChannelHandlerContext context)
        {
            Debug.Assert(context == CapturedContext);

            IByteBuffer buf = null;
            try
            {
                // Only continue to loop if the handler was not removed in the meantime.
                // See https://github.com/netty/netty/issues/5860
                while (!context.IsRemoved)
                {
                    List<object> messages = _pendingUnencryptedWrites.Current;
                    if (messages is null || 0u >= (uint)messages.Count)
                    {
                        break;
                    }

                    if (1u >= (uint)messages.Count) // messages.Count == 1; messages 最小数量为 1
                    {
                        buf = (IByteBuffer)messages[0];
                    }
                    else
                    {
                        buf = context.Allocator.Buffer((int)_pendingUnencryptedWrites.CurrentSize);
                        for (int idx = 0; idx < messages.Count; idx++)
                        {
                            var buffer = (IByteBuffer)messages[idx];
                            _ = buffer.ReadBytes(buf, buffer.ReadableBytes);
                            _ = buffer.Release();
                        }
                    }
                    _ = buf.ReadBytes(_sslStream, buf.ReadableBytes); // this leads to FinishWrap being called 0+ times
                    _ = buf.Release();
                    buf = null;

                    var promise = _pendingUnencryptedWrites.Remove();
                    Task task = _lastContextWriteTask;
                    if (task is object)
                    {
                        task.LinkOutcome(promise);
                        _lastContextWriteTask = null;
                    }
                    else
                    {
                        _ = promise.TryComplete();
                    }
                }
            }
            finally
            {
                // Ownership of buffer was not transferred, release it.
                buf?.Release();
            }
        }

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
        private void FinishWrap(in ReadOnlySpan<byte> buffer, IPromise promise)
        {
            IByteBuffer output;
            var capturedContext = CapturedContext;
            if (buffer.IsEmpty)
            {
                output = Unpooled.Empty;
            }
            else
            {
                var bufLen = buffer.Length;
                output = capturedContext.Allocator.Buffer(bufLen);
                buffer.CopyTo(output.FreeSpan);
                output.Advance(bufLen);
            }

            _lastContextWriteTask = capturedContext.WriteAsync(output, promise);
        }
#endif

        private void FinishWrap(byte[] buffer, int offset, int count, IPromise promise)
        {
            IByteBuffer output;
            var capturedContext = CapturedContext;
            if (0u >= (uint)count)
            {
                output = Unpooled.Empty;
            }
            else
            {
                output = capturedContext.Allocator.Buffer(count);
                _ = output.WriteBytes(buffer, offset, count);
            }

            _lastContextWriteTask = capturedContext.WriteAsync(output, promise);
        }

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
        private Task FinishWrapNonAppDataAsync(in ReadOnlyMemory<byte> buffer, IPromise promise)
        {
            var capturedContext = CapturedContext;
            Task future;
            if (MemoryMarshal.TryGetArray(buffer, out var seg))
            {
                future = capturedContext.WriteAndFlushAsync(Unpooled.WrappedBuffer(seg.Array, seg.Offset, seg.Count), promise);
            }
            else
            {
                future = capturedContext.WriteAndFlushAsync(Unpooled.WrappedBuffer(buffer.ToArray()), promise);
            }
            this.ReadIfNeeded(capturedContext);
            return future;
        }
#endif

        private Task FinishWrapNonAppDataAsync(byte[] buffer, int offset, int count, IPromise promise)
        {
            var capturedContext = CapturedContext;
            var future = capturedContext.WriteAndFlushAsync(Unpooled.WrappedBuffer(buffer, offset, count), promise);
            this.ReadIfNeeded(capturedContext);
            return future;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static InvalidOperationException NewPendingWritesNullException()
        {
            return new InvalidOperationException("pendingUnencryptedWrites is null, handlerRemoved0 called?");
        }
    }
}
