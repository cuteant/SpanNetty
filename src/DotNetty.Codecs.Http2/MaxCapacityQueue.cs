namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Internal;

    sealed class MaxCapacityQueue<T>
    {
        private readonly Deque<T> _queue;
        private readonly int _maxCapacity;

        public MaxCapacityQueue(int maxCapacity)
        {
            _queue = new Deque<T>(8);
            _maxCapacity = maxCapacity;
        }

        public bool TryEnqueue(T item)
        {
            var queue = _queue;
            if ((uint)_maxCapacity > (uint)queue.Count)
            {
                queue.AddLast​(item);
                return true;
            }
            return false;
        }

        public bool TryDequeue(out T result) => _queue.TryRemoveFirst(out result);

        public void Clear() => _queue.Clear();
    }
}
