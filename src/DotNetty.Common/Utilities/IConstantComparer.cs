using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DotNetty.Common.Utilities
{
    public sealed class ConstantComparer : IEqualityComparer<IConstant>
    {
        public static readonly IEqualityComparer<IConstant> Default = new ConstantComparer();

        private ConstantComparer() { }

        public bool Equals(IConstant x, IConstant y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(IConstant obj)
        {
            //if (obj is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.obj); }
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
