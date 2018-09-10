namespace DotNetty.Transport.Channels.Sockets
{
    using System;
    using System.IO;

    public class ChannelOutputShutdownException : IOException
    {
        public ChannelOutputShutdownException(string msg) : base(msg) { }

        public ChannelOutputShutdownException(string msg, Exception ex) : base(msg, ex) { }
    }
}
