
namespace DotNetty.Common.Utilities
{
    using System.Threading;

    public sealed class AtomicBoolean
    {
        const int False = 0;
        const int True = 1;

        int value;

        public AtomicBoolean() => this.Value = false;

        public AtomicBoolean(bool v) => this.Value = v;

        public bool Value
        {
            get => True == Volatile.Read(ref this.value);
            set => Interlocked.Exchange(ref this.value, value ? True : False);
        }

        public static implicit operator bool(AtomicBoolean aBool) => aBool.Value;

        public static implicit operator AtomicBoolean(bool newValue) => new AtomicBoolean(newValue);
    }
}
