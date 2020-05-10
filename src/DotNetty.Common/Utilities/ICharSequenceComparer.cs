using System.Collections.Generic;

namespace DotNetty.Common.Utilities
{
    public sealed class CharSequenceComparer : IEqualityComparer<ICharSequence>
    {
        public static readonly IEqualityComparer<ICharSequence> Default = new CharSequenceComparer();

        public static readonly IEqualityComparer<ICharSequence> IgnoreCase = new ICharSequenceIgnoreCaseComparer();

        private CharSequenceComparer() { }

        public bool Equals(ICharSequence x, ICharSequence y)
        {
            if (x is null) { return false; }

            return x.Equals(y);
        }

        public int GetHashCode(ICharSequence obj)
        {
            //if (obj is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.obj); }
            return obj.GetHashCode();
        }
    }

    sealed class ICharSequenceIgnoreCaseComparer : IEqualityComparer<ICharSequence>
    {
        public bool Equals(ICharSequence x, ICharSequence y)
        {
            if (x is null) { return false; }

            return x.ContentEqualsIgnoreCase(y);
        }

        public int GetHashCode(ICharSequence obj)
        {
            //if (obj is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.obj); }
            return obj.HashCode(true);
        }
    }
}
