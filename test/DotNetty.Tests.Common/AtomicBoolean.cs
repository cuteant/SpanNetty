
namespace DotNetty.Tests.Common
{
    using System.Threading;

    public sealed class AtomicBoolean
    {
        const int False = 0;
        const int True = 1;

        int value;

        public AtomicBoolean(bool v) => this.Value = v;

        public bool Value
        {
            get => True == Volatile.Read(ref this.value);
            set => Interlocked.Exchange(ref this.value, value ? True : False);
        }
    }
}
