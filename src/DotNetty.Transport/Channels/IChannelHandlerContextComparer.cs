using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DotNetty.Transport.Channels
{
    public sealed class ChannelHandlerContextComparer : IEqualityComparer<IChannelHandlerContext>
    {
        public static readonly IEqualityComparer<IChannelHandlerContext> Default = new ChannelHandlerContextComparer();

        private ChannelHandlerContextComparer() { }

        public bool Equals(IChannelHandlerContext x, IChannelHandlerContext y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(IChannelHandlerContext obj)
        {
            //if (null == obj) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.obj); }
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
