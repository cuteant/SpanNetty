
namespace DotNetty.Common.Utilities
{
    using System.Threading;

    public sealed class AtomicInteger
    {
        int value;

        public AtomicInteger() { }
        public AtomicInteger(int value) => this.value = value;

        public int Value
        {
            get => Volatile.Read(ref this.value);
            set => Interlocked.Exchange(ref this.value, value);
        }

        public int Increment()
        {
            return Interlocked.Increment(ref this.value);
        }

        public int GetAndIncrement()
        {
            var v = Volatile.Read(ref this.value);
            Interlocked.Increment(ref this.value);
            return v;
        }

        public int Decrement()
        {
            return Interlocked.Decrement(ref this.value);
        }

        public int AddAndGet(int v)
        {
            return Interlocked.Add(ref this.value, v);
        }

        public bool CompareAndSet(int current, int next)
        {
            return Interlocked.CompareExchange(ref this.value, next, current) == current;
        }

        public static implicit operator int(AtomicInteger aInt) => aInt.Value;

        public static implicit operator AtomicInteger(int newValue) => new AtomicInteger(newValue);
    }
}
