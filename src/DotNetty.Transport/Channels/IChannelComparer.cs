using System.Collections.Generic;

namespace DotNetty.Transport.Channels
{
    public sealed class ChannelComparer : IEqualityComparer<IChannel>
    {
        public static readonly IEqualityComparer<IChannel> Default = new ChannelComparer();

        private ChannelComparer() { }

        public bool Equals(IChannel x, IChannel y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(IChannel obj)
        {
            //if (null == obj) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.obj); }
            return obj.Id.GetHashCode();
        }
    }
}
