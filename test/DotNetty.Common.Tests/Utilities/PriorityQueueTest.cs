// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Tests.Utilities
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Common.Utilities;
    using Xunit;

    public class PriorityQueueTest
    {
        [Fact]
        public void PriorityQueueRemoveTest()
        {
            var queue = new PriorityQueue<TestNode>(TestNodeComparer.Instance);
            AssertEmptyQueue(queue);

            TestNode a = new TestNode(5);
            TestNode b = new TestNode(10);
            TestNode c = new TestNode(2);
            TestNode d = new TestNode(6);
            TestNode notInQueue = new TestNode(-1);

            AssertEnqueue(queue, a);
            AssertEnqueue(queue, b);
            AssertEnqueue(queue, c);
            AssertEnqueue(queue, d);

            // Remove an element that isn't in the queue.
            Assert.False(queue.TryRemove(notInQueue));
            Assert.Same(c, queue.Peek());
            Assert.Equal(4, queue.Count);

            // Remove the last element in the array, when the array is non-empty.
            Assert.True(queue.TryRemove(b));
            Assert.Same(c, queue.Peek());
            Assert.Equal(3, queue.Count);

            // Re-insert the element after removal
            AssertEnqueue(queue, b);
            Assert.Same(c, queue.Peek());
            Assert.Equal(4, queue.Count);

            // Repeat remove the last element in the array, when the array is non-empty.
            Assert.True(queue.TryRemove(b));
            Assert.Same(c, queue.Peek());
            Assert.Equal(3, queue.Count);

            // Remove the head of the queue.
            Assert.True(queue.TryRemove(c));
            Assert.Same(a, queue.Peek());
            Assert.Equal(2, queue.Count);

            Assert.True(queue.TryRemove(a));
            Assert.Same(d, queue.Peek());
            Assert.Equal(1, queue.Count);

            Assert.True(queue.TryRemove(d));
            AssertEmptyQueue(queue);
        }

        [Theory]
        [InlineData(new[] { 1, 2, 3, 4 }, new[] { 1, 2, 3, 4 })]
        [InlineData(new[] { 4, 3, 2, 1 }, new[] { 1, 2, 3, 4 })]
        [InlineData(new[] { 3, 2, 1 }, new[] { 1, 2, 3 })]
        [InlineData(new[] { 1, 3, 2 }, new[] { 1, 2, 3 })]
        [InlineData(new[] { 1, 2 }, new[] { 1, 2 })]
        [InlineData(new[] { 2, 1 }, new[] { 1, 2 })]
        public void PriorityQueueOrderTest(int[] input, int[] expectedOutput)
        {
            var queue = new PriorityQueue<TestNode>(TestNodeComparer.Instance);
            foreach (int value in input)
            {
                queue.TryEnqueue(new TestNode(value));
            }

            for (int index = 0; index < expectedOutput.Length; index++)
            {
                var item = queue.Dequeue();
                Assert.Equal(expectedOutput[index], item.Value);
            }
            Assert.Equal(0, queue.Count);
        }

        [Fact]
        public void ClearTest()
        {
            var queue = new PriorityQueue<TestNode>(TestNodeComparer.Instance);
            AssertEmptyQueue(queue);

            TestNode a = new TestNode(5);
            TestNode b = new TestNode(10);
            TestNode c = new TestNode(2);
            TestNode d = new TestNode(6);

            AssertEnqueue(queue, a);
            AssertEnqueue(queue, b);
            AssertEnqueue(queue, c);
            AssertEnqueue(queue, d);

            queue.Clear();
            AssertEmptyQueue(queue);

            // Test that elements can be re-inserted after the clear operation
            AssertEnqueue(queue, a);
            Assert.Same(a, queue.Peek());

            AssertEnqueue(queue, b);
            Assert.Same(a, queue.Peek());

            AssertEnqueue(queue, c);
            Assert.Same(c, queue.Peek());

            AssertEnqueue(queue, d);
            Assert.Same(c, queue.Peek());
        }

        [Fact]
        public void EnqueueTest()
        {
            var queue = new PriorityQueue<TestNode>(TestNodeComparer.Instance);
            AssertEmptyQueue(queue);

            TestNode a = new TestNode(5);
            TestNode b = new TestNode(10);
            TestNode c = new TestNode(2);
            TestNode d = new TestNode(7);
            TestNode e = new TestNode(6);

            AssertEnqueue(queue, a);
            AssertEnqueue(queue, b);
            AssertEnqueue(queue, c);
            AssertEnqueue(queue, d);

            // Remove the first element
            Assert.Same(c, queue.Peek());
            Assert.Same(c, queue.Dequeue());
            Assert.Equal(3, queue.Count);

            // Test that offering another element preserves the priority queue semantics.
            AssertEnqueue(queue, e);
            Assert.Equal(4, queue.Count);
            Assert.Same(a, queue.Peek());
            Assert.Same(a, queue.Dequeue());
            Assert.Equal(3, queue.Count);

            // Keep removing the remaining elements
            Assert.Same(e, queue.Peek());
            Assert.Same(e, queue.Dequeue());
            Assert.Equal(2, queue.Count);

            Assert.Same(d, queue.Peek());
            Assert.Same(d, queue.Dequeue());
            Assert.Equal(1, queue.Count);

            Assert.Same(b, queue.Peek());
            Assert.Same(b, queue.Dequeue());
            AssertEmptyQueue(queue);
        }

        [Fact]
        public void PriorityChangeTest()
        {
            var queue = new PriorityQueue<TestNode>(TestNodeComparer.Instance);
            AssertEmptyQueue(queue);
            TestNode a = new TestNode(10);
            TestNode b = new TestNode(20);
            TestNode c = new TestNode(30);
            TestNode d = new TestNode(25);
            TestNode e = new TestNode(23);
            TestNode f = new TestNode(15);
            queue.TryEnqueue(a);
            queue.TryEnqueue(b);
            queue.TryEnqueue(c);
            queue.TryEnqueue(d);
            queue.TryEnqueue(e);
            queue.TryEnqueue(f);

            e.Value = 35;
            queue.PriorityChanged(e);

            a.Value = 40;
            queue.PriorityChanged(a);

            a.Value = 31;
            queue.PriorityChanged(a);

            d.Value = 10;
            queue.PriorityChanged(d);

            f.Value = 5;
            queue.PriorityChanged(f);

            var expectedOrderList = new List<TestNode>(new[] { a, b, c, d, e, f });
            expectedOrderList.Sort(TestNodeComparer.Instance);
            Assert.Equal(expectedOrderList.Count, queue.Count);
            Assert.Equal(expectedOrderList.Count <= 0, queue.IsEmpty);
            for (var idx = 0; idx < expectedOrderList.Count; idx++)
            {
                Assert.Equal(expectedOrderList[idx], queue.Dequeue());
            }
            AssertEmptyQueue(queue);
        }

        private static void AssertEnqueue(PriorityQueue<TestNode> queue, TestNode item)
        {
            Assert.True(queue.TryEnqueue(item));
            Assert.True(queue.Contains(item));
            // An element can not be inserted more than 1 time.
            Assert.Throws<ArgumentException>(() => queue.Enqueue(item));
        }

        private static void AssertEmptyQueue(PriorityQueue<TestNode> queue)
        {
            Assert.False(queue.TryPeek(out _));
            Assert.False(queue.TryDequeue(out _));
            Assert.Equal(0, queue.Count);
            Assert.True(queue.IsEmpty);
        }

        sealed class TestNodeComparer : IComparer<TestNode>
        {
            public static readonly IComparer<TestNode> Instance = new TestNodeComparer();

            public int Compare(TestNode x, TestNode y)
            {
                return x.Value - y.Value;
            }
        }

        class TestNode : IPriorityQueueNode<TestNode>
        {
            int queueIndex = PriorityQueue<TestNode>.IndexNotInQueue;

            public TestNode(int item1)
            {
                Value = item1;
            }

            public int Value { get; set; }

            public int GetPriorityQueueIndex(IPriorityQueue<TestNode> queue) => this.queueIndex;

            public void SetPriorityQueueIndex(IPriorityQueue<TestNode> queue, int i) => this.queueIndex = i;

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(this, obj)) { return true; }
                return obj is TestNode other && this.Value == other.Value;
            }

            public override int GetHashCode()
            {
                return this.Value.GetHashCode();
            }
        }
    }
}