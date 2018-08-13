namespace DotNetty.Common.Utilities
{
    using System.Runtime.CompilerServices;

    partial class StringUtil
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int ThrowInvalidEscapedCsvFieldException(ICharSequence value, int index)
        {
            throw NewInvalidEscapedCsvFieldException(value, index);
        }
    }
}