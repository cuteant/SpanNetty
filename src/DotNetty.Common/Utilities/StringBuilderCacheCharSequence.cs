using CuteAnt.Pool;

namespace DotNetty.Common.Utilities
{
    public sealed class StringBuilderCacheCharSequence : StringBuilderCharSequence
    {
        public StringBuilderCacheCharSequence() : base(StringBuilderManager.Allocate(), 0, 0) { }

        public StringBuilderCacheCharSequence(int capacity) : base(StringBuilderManager.Allocate(capacity), 0, 0) { }

        public override void Dispose()
        {
            var sb = this.builder;
            if (sb != null)
            {
                this.builder = null;
                StringBuilderManager.Free(sb);
            }
        }
    }
}
