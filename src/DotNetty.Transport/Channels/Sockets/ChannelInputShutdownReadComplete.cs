namespace DotNetty.Transport.Channels.Sockets
{
    /// <summary>
    /// User event that signifies the channel's input side is shutdown, and we tried to shut it down again. This typically
    /// indicates that there is no more data to read.
    /// </summary>
    public sealed class ChannelInputShutdownReadComplete
    {
        /// <summary>Singleton instance to use.</summary>
        public static readonly ChannelInputShutdownReadComplete Instance = new ChannelInputShutdownReadComplete();

        ChannelInputShutdownReadComplete() { }
    }
}
