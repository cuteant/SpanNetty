using System;
using CuteAnt.Pool;

namespace DotNetty.Common
{
    public interface IThreadLocalPooledObjectRecycling
    {
        void Recycle();
    }

    public interface IThreadLocalPooledObjectPolicy<TPoolItem> : IPooledObjectPolicy<TPoolItem>
    {
        bool PreCreate { get; }

        Func<ThreadLocalPool.Handle, TPoolItem> ValueFactory { get; }
    }

    public class ThreadLocalObjectPool<TPoolItem> : ObjectPool<TPoolItem>
        where TPoolItem : class
    {
        private readonly ThreadLocalPool<TPoolItem> _innerPool;
        private readonly IThreadLocalPooledObjectPolicy<TPoolItem> _policy;

        public ThreadLocalObjectPool(IThreadLocalPooledObjectPolicy<TPoolItem> policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _innerPool = new ThreadLocalPool<TPoolItem>(_policy.ValueFactory, ThreadLocalPool.DefaultMaxCapacity, _policy.PreCreate);
        }

        public ThreadLocalObjectPool(IThreadLocalPooledObjectPolicy<TPoolItem> policy, int maxCapacity)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _innerPool = new ThreadLocalPool<TPoolItem>(_policy.ValueFactory, maxCapacity, _policy.PreCreate);
        }

        public override TPoolItem Get()
        {
            var item = _innerPool.Take();
            return _policy.PreGetting(item);
        }

        public override TPoolItem Take() => _innerPool.Take();

        public override void Return(TPoolItem item)
        {
            if (_policy.Return(item)) { (item as IThreadLocalPooledObjectRecycling)?.Recycle(); }
        }

        public sealed override void Clear() { }
    }
}
