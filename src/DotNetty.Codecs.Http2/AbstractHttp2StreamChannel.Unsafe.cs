namespace DotNetty.Codecs.Http2
{
    using System;
    using System.IO;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    partial class AbstractHttp2StreamChannel
    {
        sealed class Http2ChannelUnsafe : IChannelUnsafe
        {
            private readonly AbstractHttp2StreamChannel _channel;
            private readonly IPromise _unsafeVoidPromise;
            private IRecvByteBufAllocatorHandle _recvHandle;
            private bool _writeDoneAndNoFlush;

            private int _closeInitiated = SharedConstants.False;
            private bool CloseInitiated
            {
                get => SharedConstants.False < (uint)Volatile.Read(ref _closeInitiated);
                set => Interlocked.Exchange(ref _closeInitiated, value ? SharedConstants.True : SharedConstants.False);
            }

            private int v_readEOS = SharedConstants.False;
            internal bool ReadEOS
            {
                get => SharedConstants.False < (uint)Volatile.Read(ref v_readEOS);
                set => Interlocked.Exchange(ref v_readEOS, value ? SharedConstants.True : SharedConstants.False);
            }

            public Http2ChannelUnsafe(AbstractHttp2StreamChannel channel)
            {
                _channel = channel;
                _unsafeVoidPromise = new VoidChannelPromise(channel, false);
            }

            public void Initialize(IChannel channel)
            {
            }

            public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
            {
                return TaskUtil.FromException(new NotSupportedException());
            }

            public IRecvByteBufAllocatorHandle RecvBufAllocHandle
            {
                get
                {
                    if (_recvHandle is null)
                    {
                        var config = _channel.Configuration;
                        _recvHandle = config.RecvByteBufAllocator.NewHandle();
                        _recvHandle.Reset(config);

                    }
                    return _recvHandle;
                }
            }

            public Task RegisterAsync(IEventLoop eventLoop)
            {
                var ch = _channel;
                if (ch.InternalRegistered)
                {
                    ThrowHelper.ThrowNotSupportedException_Re_register_is_not_supported();
                }

                ch.InternalRegistered = true;

                var pipeline = ch.Pipeline;

                _ = pipeline.FireChannelRegistered();
                if (ch.IsActive)
                {
                    _ = pipeline.FireChannelActive();
                }

                return TaskUtil.Completed;
            }

            public Task BindAsync(EndPoint localAddress)
            {
                return TaskUtil.FromException(new NotSupportedException());
            }

            public void Disconnect(IPromise promise) => Close(promise);

            public void CloseForcibly() => Close(VoidPromise());

            public void Close(IPromise promise)
            {
                if (!promise.SetUncancellable()) { return; }

                var ch = _channel;
                if (CloseInitiated)
                {
                    var closeCompletion = ch.CloseCompletion;
                    if (closeCompletion.IsCompleted)
                    {
                        // Closed already.
                        promise.Complete();
                    }
                    else if (!promise.IsVoid) // Only needed if no VoidChannelPromise.
                    {
                        // This means close() was called before so we just register a listener and return
                        closeCompletion.LinkOutcome(promise);
                    }
                    return;
                }
                CloseInitiated = true;
                // Just set to false as removing from an underlying queue would even be more expensive.
                ch._readCompletePending = false;

                bool wasActive = ch.IsActive;

                // There is no need to update the local window as once the stream is closed all the pending bytes will be
                // given back to the connection window by the controller itself.

                // Only ever send a reset frame if the connection is still alive and if the stream was created before
                // as otherwise we may send a RST on a stream in an invalid state and cause a connection error.
                if (ch.Parent.IsActive && !ReadEOS && Http2CodecUtil.IsStreamIdValid(ch._stream.Id))
                {
                    IHttp2StreamFrame resetFrame = new DefaultHttp2ResetFrame(Http2Error.Cancel) { Stream = ch._stream };
                    Write(resetFrame, VoidPromise());
                    Flush();
                }

                var inboundBuffer = ch._inboundBuffer;
                if (inboundBuffer is object)
                {
                    while (inboundBuffer.TryRemoveFirst(out var msg))
                    {
                        _ = ReferenceCountUtil.Release(msg);
                    }
                }

                // The promise should be notified before we call fireChannelInactive().
                ch.OutboundClosed = true;
                ch._closePromise.Complete();
                promise.Complete();

                FireChannelInactiveAndDeregister(VoidPromise(), wasActive);
            }

            public void Deregister(IPromise promise)
            {
                FireChannelInactiveAndDeregister(promise, false);
            }

            private void FireChannelInactiveAndDeregister(IPromise promise, bool fireChannelInactive)
            {
                if (!promise.SetUncancellable()) { return; }

                var ch = _channel;
                if (!ch.InternalRegistered)
                {
                    promise.Complete();
                    return;
                }

                // As a user may call deregister() from within any method while doing processing in the ChannelPipeline,
                // we need to ensure we do the actual deregister operation later. This is necessary to preserve the
                // behavior of the AbstractChannel, which always invokes channelUnregistered and channelInactive
                // events 'later' to ensure the current events in the handler are completed before these events.
                //
                // See:
                // https://github.com/netty/netty/issues/4435
                InvokeLater(() =>
                {
                    if (fireChannelInactive)
                    {
                        _ = ch._pipeline.FireChannelInactive();
                    }
                    // The user can fire `deregister` events multiple times but we only want to fire the pipeline
                    // event if the channel was actually registered.
                    if (ch.InternalRegistered)
                    {
                        ch.InternalRegistered = false;
                        _ = ch._pipeline.FireChannelUnregistered();
                    }
                    Util.SafeSetSuccess(promise, Logger);
                });
            }

            private void InvokeLater(Action task)
            {
                try
                {
                    // This method is used by outbound operation implementations to trigger an inbound event later.
                    // They do not trigger an inbound event immediately because an outbound operation might have been
                    // triggered by another inbound event handler method.  If fired immediately, the call stack
                    // will look like this for example:
                    //
                    //   handlerA.inboundBufferUpdated() - (1) an inbound handler method closes a connection.
                    //   -> handlerA.ctx.close()
                    //     -> channel.unsafe.close()
                    //       -> handlerA.channelInactive() - (2) another inbound handler method called while in (1) yet
                    //
                    // which means the execution of two inbound handler methods of the same handler overlap undesirably.
                    _channel.EventLoop.Execute(task);
                }
                catch (RejectedExecutionException e)
                {
                    if (Logger.WarnEnabled) { Logger.CanotInvokeTaskLaterAsEventLoopRejectedIt(e); }
                }
            }

            public void BeginRead()
            {
                var ch = _channel;
                if (!ch.IsActive) { return; }

                UpdateLocalWindowIfNeeded();

                switch (ch._readStatus)
                {
                    case ReadStatus.Idle:
                        ch._readStatus = ReadStatus.InProgress;
                        DoBeginRead();
                        break;
                    case ReadStatus.InProgress:
                        ch._readStatus = ReadStatus.Requested;
                        break;
                    default:
                        break;
                }
            }

            internal void DoBeginRead()
            {
                var ch = _channel;
                // Process messages until there are none left (or the user stopped requesting) and also handle EOS.
                while (ch._readStatus != ReadStatus.Idle)
                {
                    var inboundBuffer = ch._inboundBuffer;
                    if (inboundBuffer is null || (!inboundBuffer.TryRemoveFirst(out var message)))
                    {
                        if (ReadEOS)
                        {
                            ch.Unsafe.CloseForcibly();
                        }
                        // We need to double check that there is nothing left to flush such as a
                        // window update frame.
                        Flush();
                        break;
                    }

                    var allocHandle = RecvBufAllocHandle;
                    allocHandle.Reset(ch._config);
                    var continueReading = false;
                    do
                    {
                        DoRead0((IHttp2Frame)message, allocHandle);
                    } while ((ReadEOS || (continueReading = allocHandle.ContinueReading())) &&
                             inboundBuffer.TryRemoveFirst(out message));

                    if (continueReading && ch.IsParentReadInProgress && !ReadEOS)
                    {
                        // Currently the parent and child channel are on the same EventLoop thread. If the parent is
                        // currently reading it is possible that more frames will be delivered to this child channel. In
                        // the case that this child channel still wants to read we delay the channelReadComplete on this
                        // child channel until the parent is done reading.
                        ch.MaybeAddChannelToReadCompletePendingQueue();
                    }
                    else
                    {
                        NotifyReadComplete(allocHandle, true);
                    }
                }
            }

            private void UpdateLocalWindowIfNeeded()
            {
                var ch = _channel;
                int bytes = ch._flowControlledBytes;
                if (bytes != 0)
                {
                    ch._flowControlledBytes = 0;
                    var future = ch.InternalWriteAsync(ch.ParentContext, new DefaultHttp2WindowUpdateFrame(bytes) { Stream = ch._stream });
                    // window update frames are commonly swallowed by the Http2FrameCodec and the promise is synchronously
                    // completed but the flow controller _may_ have generated a wire level WINDOW_UPDATE. Therefore we need,
                    // to assume there was a write done that needs to be flushed or we risk flow control starvation.
                    _writeDoneAndNoFlush = true;
                    // Add a listener which will notify and teardown the stream
                    // when a window update fails if needed or check the result of the future directly if it was completed
                    // already.
                    // See https://github.com/netty/netty/issues/9663
                    if (future.IsCompleted)
                    {
                        WindowUpdateFrameWriteComplete(future, ch);
                    }
                    else
                    {
                        _ = future.ContinueWith(WindowUpdateFrameWriteListenerAction, ch, TaskContinuationOptions.ExecuteSynchronously);
                    }
                }
            }

            internal void NotifyReadComplete(IRecvByteBufAllocatorHandle allocHandle, bool forceReadComplete)
            {
                var ch = _channel;
                if (!ch._readCompletePending && !forceReadComplete)
                {
                    return;
                }
                // Set to false just in case we added the channel multiple times before.
                ch._readCompletePending = false;

                if (ch._readStatus == ReadStatus.Requested)
                {
                    ch._readStatus = ReadStatus.InProgress;
                }
                else
                {
                    ch._readStatus = ReadStatus.Idle;
                }

                allocHandle.ReadComplete();
                _ = ch._pipeline.FireChannelReadComplete();
                // Reading data may result in frames being written (e.g. WINDOW_UPDATE, RST, etc..). If the parent
                // channel is not currently reading we need to force a flush at the child channel, because we cannot
                // rely upon flush occurring in channelReadComplete on the parent channel.
                Flush();
                if (ReadEOS)
                {
                    ch.Unsafe.CloseForcibly();
                }
            }

            internal void DoRead0(IHttp2Frame frame, IRecvByteBufAllocatorHandle allocHandle)
            {
                var ch = _channel;
                int bytes;
                if (frame is IHttp2DataFrame dataFrame)
                {
                    bytes = dataFrame.InitialFlowControlledBytes;

                    // It is important that we increment the flowControlledBytes before we call fireChannelRead(...)
                    // as it may cause a read() that will call updateLocalWindowIfNeeded() and we need to ensure
                    // in this case that we accounted for it.
                    //
                    // See https://github.com/netty/netty/issues/9663
                    ch._flowControlledBytes += bytes;
                }
                else
                {
                    bytes = MinHttp2FrameSize;
                }
                // Update before firing event through the pipeline to be consistent with other Channel implementation.
                allocHandle.AttemptedBytesRead = bytes;
                allocHandle.LastBytesRead = bytes;
                allocHandle.IncMessagesRead(1);

                _ = ch._pipeline.FireChannelRead(frame);
            }

            public void Write(object msg, IPromise promise)
            {
                // After this point its not possible to cancel a write anymore.
                if (!promise.SetUncancellable())
                {
                    _ = ReferenceCountUtil.Release(msg);
                    return;
                }

                var ch = _channel;
                if (!ch.IsActive ||
                    // Once the outbound side was closed we should not allow header / data frames
                    ch.OutboundClosed && (msg is IHttp2HeadersFrame || msg is IHttp2DataFrame))
                {
                    _ = ReferenceCountUtil.Release(msg);
                    promise.SetException(ThrowHelper.GetClosedChannelException());
                    return;
                }

                try
                {
                    if (msg is IHttp2StreamFrame streamFrame)
                    {
                        var frame = ValidateStreamFrame(streamFrame);
                        WriteHttp2StreamFrame(frame, promise);
                    }
                    else
                    {
                        _ = ReferenceCountUtil.Release(msg);
                        promise.SetException(ThrowHelper.GetArgumentException_MsgMustBeStreamFrame(msg));
                    }
                }
                catch (Exception t)
                {
                    promise.SetException(t);
                }
            }

            private void WriteHttp2StreamFrame(IHttp2StreamFrame frame, IPromise promise)
            {
                var ch = _channel;
                var frameStream = ch._stream;
                frame.Stream = frameStream;
                if (!ch._firstFrameWritten && !Http2CodecUtil.IsStreamIdValid(frameStream.Id) && !(frame is IHttp2HeadersFrame))
                {
                    _ = ReferenceCountUtil.Release(frame);
                    promise.SetException(ThrowHelper.GetArgumentException_FirstFrameMustBeHeadersFrame(frame));
                    return;
                }

                bool firstWrite;
                if (ch._firstFrameWritten)
                {
                    firstWrite = false;
                }
                else
                {
                    firstWrite = ch._firstFrameWritten = true;
                }

                var future = ch.InternalWriteAsync(ch.ParentContext, frame);
                if (future.IsCompleted)
                {
                    InvokeWriteComplete(future, promise, firstWrite);
                }
                else
                {
                    long bytes = FlowControlledFrameSizeEstimatorHandle.Instance.Size(frame);
                    ch.IncrementPendingOutboundBytes(bytes, false);
                    _ = future.ContinueWith(InvokeWriteCompleteAfterWriteAction,
                        Tuple.Create(this, promise, bytes, firstWrite), TaskContinuationOptions.ExecuteSynchronously);
                    _writeDoneAndNoFlush = true;
                }
            }

            private static readonly Action<Task, object> InvokeWriteCompleteAfterWriteAction = InvokeWriteCompleteAfterWrite;
            private static void InvokeWriteCompleteAfterWrite(Task t, object s)
            {
                var wrapped = (Tuple<Http2ChannelUnsafe, IPromise, long, bool>)s;
                var self = wrapped.Item1;
                self.InvokeWriteComplete(t, wrapped.Item2, wrapped.Item4);
                self._channel.DecrementPendingOutboundBytes(wrapped.Item3, false);
            }

            [MethodImpl(InlineMethod.AggressiveInlining)]
            private void InvokeWriteComplete(Task future, IPromise promise, bool firstWrite)
            {
                if (firstWrite)
                {
                    FirstWriteComplete(future, promise);
                }
                else
                {
                    WriteComplete(future, promise);
                }
            }

            private void FirstWriteComplete(Task future, IPromise promise)
            {
                if (future.IsSuccess())
                {
                    promise.Complete();
                }
                else
                {
                    // If the first write fails there is not much we can do, just close
                    CloseForcibly();
                    promise.SetException(WrapStreamClosedError(future.Exception.InnerException));
                }
            }

            private void WriteComplete(Task future, IPromise promise)
            {
                if (future.IsSuccess())
                {
                    promise.Complete();
                }
                else
                {
                    var cause = future.Exception.InnerException;
                    var error = WrapStreamClosedError(cause);
                    if (error is IOException)
                    {
                        if (_channel._config.IsAutoClose)
                        {
                            // Close channel if needed.
                            CloseForcibly();
                        }
                        else
                        {
                            // TODO: Once Http2StreamChannel extends DuplexChannel we should call shutdownOutput(...)
                            _channel.OutboundClosed = true;
                        }
                    }
                    promise.SetException(error);
                }
            }

            private Exception WrapStreamClosedError(Exception cause)
            {
                // If the error was caused by STREAM_CLOSED we should use a ClosedChannelException to better
                // mimic other transports and make it easier to reason about what exceptions to expect.
                if (cause is Http2Exception http2Exception && http2Exception.Error == Http2Error.StreamClosed)
                {
                    return new ClosedChannelException(cause.Message, cause);
                }
                return cause;
            }

            private IHttp2StreamFrame ValidateStreamFrame(IHttp2StreamFrame frame)
            {
                var frameStream = frame.Stream;
                if (frameStream is object && frameStream != _channel._stream)
                {
                    _ = ReferenceCountUtil.Release(frame);
                    ThrowHelper.ThrowArgumentException_StreamMustNotBeSetOnTheFrame(frame);
                }
                return frame;
            }

            public void Flush()
            {
                var ch = _channel;
                // If we are currently in the parent channel's read loop we should just ignore the flush.
                // We will ensure we trigger ctx.flush() after we processed all Channels later on and
                // so aggregate the flushes. This is done as ctx.flush() is expensive when as it may trigger an
                // write(...) or writev(...) operation on the socket.
                if (!_writeDoneAndNoFlush || ch.IsParentReadInProgress)
                {
                    // There is nothing to flush so this is a NOOP.
                    return;
                }
                // We need to set this to false before we call flush0(...) as ChannelFutureListener may produce more data
                // that are explicit flushed.
                _writeDoneAndNoFlush = false;
                ch.Flush0(ch.ParentContext);
            }

            public IPromise VoidPromise() => _unsafeVoidPromise;

            public ChannelOutboundBuffer OutboundBuffer
            {
                // Always return null as we not use the ChannelOutboundBuffer and not even support it.
                get => null;
            }
        }
    }
}
