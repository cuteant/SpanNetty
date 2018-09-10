using System.Collections.Generic;

namespace DotNetty.Common.Utilities
{
    public sealed class AsciiStringComparer : IEqualityComparer<AsciiString>
    {
        public static readonly AsciiStringComparer Default = new AsciiStringComparer();

        public static readonly IEqualityComparer<AsciiString> IgnoreCase = new AsciiStringIgnoreCaseComparer();

        private AsciiStringComparer() { }

        public bool Equals(AsciiString x, AsciiString y)
        {
            if (null == x) { return false; }

            return x.Equals(y);
        }

        public int GetHashCode(AsciiString obj)
        {
            //if (null == obj) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.obj); }
            return obj.GetHashCode();
        }
    }

    sealed class AsciiStringIgnoreCaseComparer : IEqualityComparer<AsciiString>
    {
        public bool Equals(AsciiString x, AsciiString y)
        {
            if (null == x) { return false; }

            return x.ContentEqualsIgnoreCase(y);
        }

        public int GetHashCode(AsciiString obj)
        {
            //if (null == obj) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.obj); }
            return obj.HashCode(true);
        }
    }
}
