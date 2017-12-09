namespace System.Threading
{
    public static class TimeoutShim
  {
#if NET40
    public static readonly TimeSpan InfiniteTimeSpan = new TimeSpan(0, 0, 0, 0, Timeout.Infinite);
#else
    public static readonly TimeSpan InfiniteTimeSpan = Timeout.InfiniteTimeSpan;
#endif
  }
}
