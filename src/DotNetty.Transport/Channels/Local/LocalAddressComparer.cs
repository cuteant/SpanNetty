using System.Collections.Generic;

namespace DotNetty.Transport.Channels.Local
{
    public sealed class LocalAddressComparer : IEqualityComparer<LocalAddress>
    {
        public static readonly IEqualityComparer<LocalAddress> Default = new LocalAddressComparer();

        private LocalAddressComparer() { }

        public bool Equals(LocalAddress x, LocalAddress y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(LocalAddress obj)
        {
            //if (null == obj) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.obj); }

            return obj.GetHashCode();
        }
    }
}
