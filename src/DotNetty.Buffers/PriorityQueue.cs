using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

// Largely based from https://github.com/creativitRy/CSharpPriorityQueue/blob/master/PriorityQueue.cs

namespace DotNetty.Buffers
{
    [DebuggerDisplay("Count = {Count}")]
    sealed class PriorityQueue<T> : ICollection<T>
    {
        private readonly List<T> _list;
        private readonly IComparer<T> _comparer;

        public int Count => _list.Count;
        public bool IsReadOnly => false;

        public PriorityQueue()
            : this(Comparer<T>.Default)
        {
        }

        public PriorityQueue(IComparer<T> comparer)
        {
            _list = new List<T>();
            _comparer = comparer;
        }

        public void Add(T item)
        {
            _list.Add(item);
            HeapUp(Count - 1);
        }

        public T Peek()
        {
            if (0u >= (uint)_list.Count) { return default; }

            return _list[0];
        }

        public T Poll()
        {
            if (0u >= (uint)_list.Count) { return default; }

            var root = _list[0];
            _list[0] = _list[Count - 1];
            _list.RemoveAt(Count - 1);
            HeapDown(0);
            return root;
        }

        public void Clear()
        {
            _list.Clear();
        }

        public bool Contains(T item)
        {
            return IndexOf(item) >= 0;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            var index = IndexOf(item);
            if (index < 0) { return false; }

            _list[index] = _list[Count - 1];
            _list.RemoveAt(Count - 1);
            HeapDown(index);

            return true;
        }

        public int IndexOf(T item)
        {
            if (0u >= (uint)Count) { return -1; }

            var queue = new Queue<int>();
            queue.Enqueue(0);
            while (queue.Count > 0)
            {
                var index = queue.Dequeue();

                if (_list[index].Equals(item)) { return index; }

                if (index >= Count - 1 || _comparer.Compare(_list[index], item) > 0)
                {
                    continue;
                }

                var left = LeftChild(index);
                if (left < Count) { queue.Enqueue(left); }
                var right = RightChild(index);
                if (right < Count) { queue.Enqueue(right); }
            }

            return -1;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void HeapUp(int i)
        {
            var moving = _list[i];
            while (i > 0)
            {
                var parent = Parent(i);
                if (_comparer.Compare(_list[parent], moving) < 0) { break; }
                Swap(i, parent);
                i = parent;
            }
        }

        private void HeapDown(int i)
        {
            while (true)
            {
                var left = LeftChild(i);
                if (left >= Count) { break; }
                var right = RightChild(i);

                var child = right >= Count
                    ? left
                    : _comparer.Compare(_list[left], _list[right]) < 0
                        ? left
                        : right;

                if (_comparer.Compare(_list[child], _list[i]) > 0) { break; }
                Swap(i, child);
                i = child;
            }
        }

        private static int Parent(int i)
        {
            return (i - 1) / 2;
        }

        private static int RightChild(int i)
        {
            return 2 * i + 2;
        }

        private static int LeftChild(int i)
        {
            return 2 * i + 1;
        }

        private void Swap(int i, int j)
        {
            var temp = _list[i];
            _list[i] = _list[j];
            _list[j] = temp;
        }
    }
}