using System.Collections.Generic;

namespace DotNetty.Transport.Channels.Local
{
    public sealed class LocalAddressComparer : IEqualityComparer<LocalAddress>
    {
        public static readonly IEqualityComparer<LocalAddress> Default = new LocalAddressComparer();

        private LocalAddressComparer() { }

        public bool Equals(LocalAddress x, LocalAddress y)
        {
            return x is object ? x.Equals(y) : y is null;
        }

        public int GetHashCode(LocalAddress obj)
        {
            //if (obj is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.obj); }

            return obj.GetHashCode();
        }
    }
}
