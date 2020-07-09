
namespace DotNetty.Common.Utilities
{
    using System.Threading;

    public sealed class AtomicLong
    {
        long _value;

        public AtomicLong() { }
        public AtomicLong(long value) => _value = value;

        public long Value
        {
            get => Volatile.Read(ref _value);
            set => Interlocked.Exchange(ref _value, value);
        }

        public long Increment()
        {
            return Interlocked.Increment(ref _value);
        }

        public long GetAndIncrement()
        {
            var v = Volatile.Read(ref _value);
            _ = Interlocked.Increment(ref _value);
            return v;
        }

        public long Decrement()
        {
            return Interlocked.Decrement(ref _value);
        }

        public long AddAndGet(long v)
        {
            return Interlocked.Add(ref _value, v);
        }

        public bool CompareAndSet(long current, long next)
        {
            return Interlocked.CompareExchange(ref _value, next, current) == current;
        }

        public static implicit operator long(AtomicLong aInt) => aInt.Value;

        public static implicit operator AtomicLong(long newValue) => new AtomicLong(newValue);
    }
}
