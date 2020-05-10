namespace DotNetty.Transport.Channels
{
    public abstract class PendingBytesTracker : IMessageSizeEstimatorHandle
    {
        readonly IMessageSizeEstimatorHandle estimatorHandle;

        protected PendingBytesTracker(IMessageSizeEstimatorHandle estimatorHandle)
        {
            if (estimatorHandle is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.estimatorHandle); }
            this.estimatorHandle = estimatorHandle;
        }

        public static PendingBytesTracker NewTracker(IChannel channel)
        {
            if (channel.Pipeline is DefaultChannelPipeline defaultPipeline)
            {
                return new DefaultChannelPipelinePendingBytesTracker(defaultPipeline);
            }
            else
            {
                var buffer = channel.Unsafe.OutboundBuffer;
                var handle = channel.Configuration.MessageSizeEstimator.NewHandle();
                // We need to guard against null as channel.unsafe().outboundBuffer() may returned null
                // if the channel was already closed when constructing the PendingBytesTracker.
                // See https://github.com/netty/netty/issues/3967
                return buffer is null 
                    ? (PendingBytesTracker)new NoopPendingBytesTracker(handle) 
                    : new ChannelOutboundBufferPendingBytesTracker(buffer, handle);
            }
        }

        public virtual int Size(object msg) => this.estimatorHandle.Size(msg);

        public abstract void IncrementPendingOutboundBytes(long bytes);

        public abstract void DecrementPendingOutboundBytes(long bytes);
    }

    sealed class DefaultChannelPipelinePendingBytesTracker : PendingBytesTracker
    {
        readonly DefaultChannelPipeline pipeline;

        public DefaultChannelPipelinePendingBytesTracker(DefaultChannelPipeline pipeline)
            : base(pipeline.EstimatorHandle)
        {
            this.pipeline = pipeline;
        }

        public override void IncrementPendingOutboundBytes(long bytes)
        {
            this.pipeline.IncrementPendingOutboundBytes(bytes);
        }

        public override void DecrementPendingOutboundBytes(long bytes)
        {
            this.pipeline.DecrementPendingOutboundBytes(bytes);
        }
    }

    sealed class ChannelOutboundBufferPendingBytesTracker : PendingBytesTracker
    {
        readonly ChannelOutboundBuffer buffer;

        public ChannelOutboundBufferPendingBytesTracker(ChannelOutboundBuffer buffer, IMessageSizeEstimatorHandle estimatorHandle)
            : base(estimatorHandle)
        {
            this.buffer = buffer;
        }

        public override void IncrementPendingOutboundBytes(long bytes)
        {
            this.buffer.IncrementPendingOutboundBytes(bytes);
        }

        public override void DecrementPendingOutboundBytes(long bytes)
        {
            this.buffer.DecrementPendingOutboundBytes(bytes);
        }
    }

    sealed class NoopPendingBytesTracker : PendingBytesTracker
    {
        public NoopPendingBytesTracker(IMessageSizeEstimatorHandle estimatorHandle) : base(estimatorHandle) { }

        public override void IncrementPendingOutboundBytes(long bytes)
        {
            // Noop
        }

        public override void DecrementPendingOutboundBytes(long bytes)
        {
            // Noop
        }
    }
}
