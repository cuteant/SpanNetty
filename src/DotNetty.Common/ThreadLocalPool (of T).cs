// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System;
    using Thread = DotNetty.Common.Concurrency.XThread;

    public sealed class ThreadLocalPool<T> : ThreadLocalPool
        where T : class
    {
        private readonly ThreadLocalStack _threadLocal;
        private readonly bool _preCreate;
        private readonly Func<Handle, T> _valueFactory;

        public ThreadLocalPool(Func<Handle, T> valueFactory)
            : this(valueFactory, DefaultMaxCapacityPerThread)
        {
        }

        public ThreadLocalPool(Func<Handle, T> valueFactory, int maxCapacityPerThread)
            : this(valueFactory, maxCapacityPerThread, DefaultMaxCapacityPerThread, DefaultRatio, DefaultMaxCapacityPerThread, false)
        {
        }

        public ThreadLocalPool(Func<Handle, T> valueFactory, int maxCapacityPerThread, bool preCreate)
            : this(valueFactory, maxCapacityPerThread, DefaultMaxCapacityPerThread, DefaultRatio, DefaultMaxCapacityPerThread, preCreate)
        {
        }

        public ThreadLocalPool(Func<Handle, T> valueFactory, int maxCapacityPerThread, int maxSharedCapacityFactor)
            : this(valueFactory, maxCapacityPerThread, maxSharedCapacityFactor, DefaultRatio, DefaultMaxCapacityPerThread, false)
        {
        }

        public ThreadLocalPool(Func<Handle, T> valueFactory, int maxCapacityPerThread, int maxSharedCapacityFactor,
                       int ratio, int maxDelayedQueuesPerThread, bool preCreate = false)
            : base(maxCapacityPerThread, maxSharedCapacityFactor, ratio, maxDelayedQueuesPerThread)
        {
            if (valueFactory is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.valueFactory); }

            _preCreate = preCreate;

            _threadLocal = new ThreadLocalStack(this);
            _valueFactory = valueFactory;
        }

        public T Take()
        {
            if (0u >= (uint)_maxCapacityPerThread)
            {
                return _valueFactory(NoopHandle.Instance);
            }

            Stack stack = _threadLocal.Value;
            if (!stack.TryPop(out DefaultHandle handle))
            {
                handle = CreateValue(stack);
            }
            return (T)handle.Value;
        }

        DefaultHandle CreateValue(Stack stack)
        {
            var handle = stack.NewHandle();
            handle.Value = _valueFactory(handle);
            return handle;
        }

        internal int ThreadLocalCapacity => _threadLocal.Value._elements.Length;

        internal int ThreadLocalSize => _threadLocal.Value._size;

        sealed class ThreadLocalStack : FastThreadLocal<Stack>
        {
            readonly ThreadLocalPool<T> _owner;

            public ThreadLocalStack(ThreadLocalPool<T> owner)
            {
                _owner = owner;
            }

            protected override Stack GetInitialValue()
            {
                var stack = new Stack(_owner, Thread.CurrentThread, _owner._maxCapacityPerThread,
                        _owner._maxSharedCapacityFactor, _owner._ratioMask, _owner._maxDelayedQueuesPerThread);
                if (_owner._preCreate)
                {
                    for (int i = 0; i < _owner._maxCapacityPerThread; i++)
                    {
                        stack.Push(_owner.CreateValue(stack));
                    }
                }
                return stack;
            }

            protected override void OnRemoval(Stack value)
            {
                // Let us remove the WeakOrderQueue from the WeakHashMap directly if its safe to remove some overhead
                if (value._threadRef.TryGetTarget(out Thread valueThread) && valueThread == Thread.CurrentThread)
                {
                    if (DelayedPool.IsSet())
                    {
                        DelayedPool.Value.WeakTable.Remove(value);
                    }
                }
            }
        }
    }
}