namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Internal;

    sealed class MaxCapacityQueue<T>
    {
        private readonly QueueX<T> _queue;
        private readonly int _maxCapacity;

        public MaxCapacityQueue(int maxCapacity)
        {
            _queue = new QueueX<T>(8);
            _maxCapacity = maxCapacity;
        }

        public bool TryEnqueue(T item)
        {
            var queue = _queue;
            if ((uint)_maxCapacity > (uint)queue.Count)
            {
                queue.Enqueue(item);
                return true;
            }
            return false;
        }

        public bool TryDequeue(out T result) => _queue.TryDequeue(out result);

        public void Clear() => _queue.Clear();
    }
}
