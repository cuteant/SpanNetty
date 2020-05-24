
namespace DotNetty.Common.Utilities
{
    using System.Threading;

    public sealed class AtomicBoolean
    {
        int value;

        public AtomicBoolean() => this.Value = false;

        public AtomicBoolean(bool v) => this.Value = v;

        public bool Value
        {
            get => 0u < (uint)Volatile.Read(ref this.value);
            set => Interlocked.Exchange(ref this.value, value ? SharedConstants.True : SharedConstants.False);
        }

        public static implicit operator bool(AtomicBoolean aBool) => aBool.Value;

        public static implicit operator AtomicBoolean(bool newValue) => new AtomicBoolean(newValue);
    }
}
