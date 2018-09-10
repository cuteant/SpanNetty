namespace DotNetty.Transport.Channels.Sockets
{
    /// <summary>
    /// Special event which will be fired and passed to the
    /// <see cref="IChannelHandler.UserEventTriggered(IChannelHandlerContext,object)"/> methods once the output of
    /// a <see cref="ISocketChannel"/> was shutdown.
    /// </summary>
    public sealed class ChannelOutputShutdownEvent
    {
        /// <summary>Singleton instance to use.</summary>
        public static readonly ChannelOutputShutdownEvent Instance = new ChannelOutputShutdownEvent();

        ChannelOutputShutdownEvent() { }
    }
}
