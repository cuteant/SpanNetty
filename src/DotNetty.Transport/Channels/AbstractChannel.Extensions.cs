// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels.Sockets;

    partial class AbstractChannel<TChannel, TUnsafe> : DefaultAttributeMap, IChannel
        where TChannel : AbstractChannel<TChannel, TUnsafe>
        where TUnsafe : IChannelUnsafe, new()
    {
        public bool Equals(IChannel other) => ReferenceEquals(this, other);

        partial class AbstractUnsafe
        {
            static readonly Action<object, object> RegisterAction = OnRegister;

            private static void OnRegister(object u, object p)
            {
                ((AbstractUnsafe)u).Register0((IPromise)p);
            }

            /// <summary>
            /// Shutdown the output portion of the corresponding <see cref="IChannel"/>.
            /// For example this will clean up the <see cref="ChannelOutboundBuffer"/> and not allow any more writes.
            /// </summary>
            /// <param name="promise"></param>
            public void ShutdownOutput(IPromise promise)
            {
                this.AssertEventLoop();
                ShutdownOutput(promise, null);
            }

            /// <summary>
            /// Shutdown the output portion of the corresponding <see cref="IChannel"/>.
            /// For example this will clean up the <see cref="ChannelOutboundBuffer"/> and not allow any more writes.
            /// </summary>
            /// <param name="cause">The cause which may provide rational for the shutdown.</param>
            /// <param name="promise"></param>
            public void ShutdownOutput(IPromise promise, Exception cause)
            {
                if (!promise.SetUncancellable()) { return; }

                var outboundBuffer = Interlocked.Exchange(ref this.outboundBuffer, null); // Disallow adding any messages and flushes to outboundBuffer.
                if (outboundBuffer == null)
                {
                    promise.TrySetException(CloseClosedChannelException);
                    return;
                }

                var shutdownCause = ThrowHelper.GetChannelOutputShutdownException(cause);
                var closeExecutor = this.PrepareToClose();
                if (closeExecutor is object)
                {
                    closeExecutor.Execute(() =>
                    {
                        try
                        {
                            // Execute the shutdown.
                            this.channel.DoShutdownOutput();
                            promise.TryComplete();
                        }
                        catch (Exception err)
                        {
                            promise.TrySetException(err);
                        }
                        finally
                        {
                            // Dispatch to the EventLoop
                            this.channel.EventLoop.Execute(() => this.CloseOutboundBufferForShutdown(this.channel.pipeline, outboundBuffer, shutdownCause));
                        }
                    });
                }
                else
                {
                    try
                    {
                        // Execute the shutdown.
                        this.channel.DoShutdownOutput();
                        promise.TryComplete();
                    }
                    catch (Exception err)
                    {
                        promise.TrySetException(err);
                    }
                    finally
                    {
                        this.CloseOutboundBufferForShutdown(this.channel.pipeline, outboundBuffer, shutdownCause);
                    }
                }
            }

            private void CloseOutboundBufferForShutdown(IChannelPipeline pipeline, ChannelOutboundBuffer buffer, Exception cause)
            {
                buffer.FailFlushed(cause, false);
                buffer.Close(cause, true);
                pipeline.FireUserEventTriggered(ChannelOutputShutdownEvent.Instance);
            }
        }

        /// <summary>
        /// Called when conditions justify shutting down the output portion of the channel. This may happen if a write
        /// operation throws an exception.
        /// </summary>
        protected virtual void DoShutdownOutput() => DoClose();
    }
}