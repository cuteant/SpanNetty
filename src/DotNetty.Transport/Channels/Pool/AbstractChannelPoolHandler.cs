namespace DotNetty.Transport.Channels.Pool
{
    /// <summary>
    /// A skeletal <see cref="IChannelPoolHandler"/> implementation.
    /// </summary>
    public abstract class AbstractChannelPoolHandler : IChannelPoolHandler
    {
        /// <inheritdoc />
        public virtual void ChannelAcquired(IChannel channel)
        {
            // NOOP implementation, sub-classes may override this.
        }

        /// <inheritdoc />
        public virtual void ChannelReleased(IChannel channel)
        {
            // NOOP implementation, sub-classes may override this.
        }

        /// <inheritdoc />
        public abstract void ChannelCreated(IChannel channel);
    }
}
