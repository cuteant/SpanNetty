namespace DotNetty
{
    public static class SharedConstants
    {
        public const int True = 1;

        public const int False = 0;

        public const int Zero = 0;

        public const byte Zero8 = 0;

        public const long Zero64 = 0L;

        public const int IndexNotFound = -1;
        public const uint uIndexNotFound = unchecked((uint)IndexNotFound);

        public const uint TooBigOrNegative = int.MaxValue;
        public const ulong TooBigOrNegative64 = long.MaxValue;

        public const uint uStackallocThreshold = 256u;
        public const int StackallocThreshold = 256;
    }
}
