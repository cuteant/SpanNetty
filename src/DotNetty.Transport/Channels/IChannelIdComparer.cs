using System.Collections.Generic;

namespace DotNetty.Transport.Channels
{
    public sealed class ChannelIdComparer : IEqualityComparer<IChannelId>
    {
        public static readonly IEqualityComparer<IChannelId> Default = new ChannelIdComparer();

        private ChannelIdComparer() { }

        public bool Equals(IChannelId x, IChannelId y)
        {
            if (x is null) { return false; }

            return x.Equals(y);
        }

        public int GetHashCode(IChannelId obj)
        {
            //if (obj is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.obj); }
            return obj.GetHashCode();
        }
    }
}
