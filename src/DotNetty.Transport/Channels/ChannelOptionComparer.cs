using System.Collections.Generic;

namespace DotNetty.Transport.Channels
{
    public sealed class ChannelOptionComparer : IEqualityComparer<ChannelOption>
    {
        public static readonly IEqualityComparer<ChannelOption> Default = new ChannelOptionComparer();

        private ChannelOptionComparer() { }

        public bool Equals(ChannelOption x, ChannelOption y)
        {
            return ReferenceEquals(x, y);
            //if (ReferenceEquals(x, y)) { return true; }
            //if (x is null || y is null) { return false; }
            //return x.Equals(y);
        }

        public int GetHashCode(ChannelOption obj)
        {
            //if (obj is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.obj); }

            return obj.GetHashCode();
        }
    }
}
