
namespace DotNetty.TestSuite
{
    using System.Threading;

    static class CountdownEventExtensions
    {
        public static bool SafeSignal(this CountdownEvent @event)
        {
            if (@event.IsSet) { return true; }
            return @event.Signal();
        }
    }
}
