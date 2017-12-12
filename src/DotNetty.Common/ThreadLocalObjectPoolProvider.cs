using System;
using CuteAnt.Pool;

namespace DotNetty.Common
{
    public class ThreadLocalObjectPoolProvider : ObjectPoolProvider
    {
        public static readonly ThreadLocalObjectPoolProvider Default = new ThreadLocalObjectPoolProvider();

        public int MaximumRetained { get; set; } = ThreadLocalPool.DefaultMaxCapacity;

        public override ObjectPool<T> Create<T>(IPooledObjectPolicy<T> policy)
        {
            var threadLocalPolicy = policy as IThreadLocalPooledObjectPolicy<T>;
            if (threadLocalPolicy == null)
            {
                var msg = $"Type of policy requires pooled object policy of type {typeof(IThreadLocalPooledObjectPolicy<T>)}.";
                throw new InvalidCastException(msg);
            }
            return new ThreadLocalObjectPool<T>(threadLocalPolicy, MaximumRetained);
        }

        public override ObjectPool<T> Create<T>(IPooledObjectPolicy<T> policy, int maximumRetained)
        {
            var threadLocalPolicy = policy as IThreadLocalPooledObjectPolicy<T>;
            if (threadLocalPolicy == null)
            {
                var msg = $"Type of policy requires pooled object policy of type {typeof(IThreadLocalPooledObjectPolicy<T>)}.";
                throw new InvalidCastException(msg);
            }
            return new ThreadLocalObjectPool<T>(threadLocalPolicy, maximumRetained);
        }
    }
}
