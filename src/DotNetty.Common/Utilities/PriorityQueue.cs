// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using DotNetty.Common.Internal;

    public class PriorityQueue<T> : IPriorityQueue<T>
        where T : class, IPriorityQueueNode<T>

    {
        private static readonly T[] EmptyArray = EmptyArray<T>.Instance;

        public const int IndexNotInQueue = -1;

        private readonly IComparer<T> _comparer;
        private int _count;
        private int _capacity;
        private T[] _items;

        public PriorityQueue(IComparer<T> comparer, int initialCapacity)
        {
            if (comparer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.comparer); }

            _comparer = comparer;
            _capacity = initialCapacity;
            _items = _capacity != 0 ? new T[_capacity] : EmptyArray;
        }

        public PriorityQueue(IComparer<T> comparer)
            : this(comparer, 11)
        {
        }

        public PriorityQueue()
            : this(Comparer<T>.Default)
        {
        }

        public int Count => _count;

        public bool IsEmpty => 0u >= (uint)_count;

        public bool NonEmpty => _count > 0;

        public T Dequeue() => TryDequeue(out T item) ? item : null;

        public bool TryDequeue(out T item)
        {
            if (!TryPeek(out item) || item is null)
            {
                return false;
            }

            item.SetPriorityQueueIndex(this, IndexNotInQueue);
            int newCount = --_count;
            T lastItem = _items[newCount];
            _items[newCount] = null;
            if (newCount > 0)
            {
                TrickleDown(0, lastItem);
            }

            return true;
        }

        public T Peek() => _count > 0 ? _items[0] : null;

        public bool TryPeek(out T item)
        {
            if (0u >= (uint)_count)
            {
                item = null;
                return false;
            }
            item = _items[0];
            return true;
        }

        public void Enqueue(T item)
        {
            if (item is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.item); }

            int index = item.GetPriorityQueueIndex(this);
            if (index != IndexNotInQueue)
            {
                ThrowHelper.ThrowArgumentException_PriorityQueueIndex(index, item);
            }

            Enqueue0(item);
        }

        public bool TryEnqueue(T item)
        {
            if (item is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.item); }

            int index = item.GetPriorityQueueIndex(this);
            if (index != IndexNotInQueue) { return false; }

            Enqueue0(item);

            return true;
        }

        private void Enqueue0(T item)
        {
            int oldCount = _count;
            if (oldCount == _capacity)
            {
                GrowHeap();
            }
            _count = oldCount + 1;
            BubbleUp(oldCount, item);
        }

        public bool TryRemove(T item)
        {
            int index = item.GetPriorityQueueIndex(this);
            if (!Contains(item, index))
            {
                return false;
            }

            item.SetPriorityQueueIndex(this, IndexNotInQueue);

            _count--;
            if (index == _count)
            {
                _items[index] = default;
            }
            else
            {
                T last = _items[_count];
                _items[_count] = default;
                TrickleDown(index, last);
                if (_items[index] == last)
                {
                    BubbleUp(index, last);
                }
            }

            return true;
        }

        public bool Contains(T item)
            => Contains(item, item.GetPriorityQueueIndex(this));

        public void PriorityChanged(T node)
        {
            int i = node.GetPriorityQueueIndex(this);
            if (!Contains(node, i))
            {
                return;
            }

            // Preserve the min-heap property by comparing the new priority with parents/children in the heap.
            if (0u >= (uint)i)
            {
                TrickleDown(i, node);
            }
            else
            {
                // Get the parent to see if min-heap properties are violated.
                int parentIndex = (i - 1).RightUShift(1);
                T parent = _items[parentIndex];
                if (_comparer.Compare(node, parent) < 0)
                {
                    BubbleUp(i, node);
                }
                else
                {
                    TrickleDown(i, node);
                }
            }
        }

        void BubbleUp(int index, T item)
        {
            // index > 0 means there is a parent
            while (index > 0)
            {
                int parentIndex = (index - 1).RightUShift(1);
                T parentItem = _items[parentIndex];
                if (_comparer.Compare(item, parentItem) >= 0)
                {
                    break;
                }
                _items[index] = parentItem;
                parentItem.SetPriorityQueueIndex(this, index);
                index = parentIndex;
            }

            _items[index] = item;
            item.SetPriorityQueueIndex(this, index);
        }

        void GrowHeap()
        {
            int oldCapacity = _capacity;
            _capacity = oldCapacity + (oldCapacity <= 64 ? oldCapacity + 2 : (oldCapacity.RightUShift(1)));
            var newHeap = new T[_capacity];
            Array.Copy(_items, 0, newHeap, 0, _count);
            _items = newHeap;
        }

        void TrickleDown(int index, T item)
        {
            int middleIndex = _count.RightUShift(1);
            while (index < middleIndex)
            {
                int childIndex = (index << 1) + 1;
                T childItem = _items[childIndex];
                int rightChildIndex = childIndex + 1;
                if (rightChildIndex < _count
                    && _comparer.Compare(childItem, _items[rightChildIndex]) > 0)
                {
                    childIndex = rightChildIndex;
                    childItem = _items[rightChildIndex];
                }
                var result = _comparer.Compare(item, childItem);
                if ((uint)(result - 1) > SharedConstants.TooBigOrNegative) // <= 0
                {
                    break;
                }

                _items[index] = childItem;
                childItem.SetPriorityQueueIndex(this, index);

                index = childIndex;
            }

            _items[index] = item;
            item.SetPriorityQueueIndex(this, index);
        }

        bool Contains(T node, int i)
            => /*i >= 0 && */(uint)i < (uint)_count && node.Equals(_items[i]);


        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
            {
                yield return _items[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Clear()
        {
            for (int i = 0; i < _count; i++)
            {
                _items[i]?.SetPriorityQueueIndex(this, IndexNotInQueue);
            }

            _count = 0;
            Array.Clear(_items, 0, 0);
        }
    }
}