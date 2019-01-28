// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System.Collections.Concurrent;
    using System.Threading;

    //Workaround for: https://github.com/dotnet/corefx/issues/29759
    //As from mono 5.2 and .NET Core 2.0, ConcurrentQueue.Count has knowingly been made slower (in some cases, O(N)) in favor of faster Enqueue() and Dequeue() operations (See: https://github.com/dotnet/corefx/issues/29759#issuecomment-390435245)
    //This wrapper implements a workaround by maintaining the queue count at the expense of a slightly slower Enqueue()/Dequeue()
    //Interlocked.Increment/Decrement operations are of the order of 36-90 cycles (similar to a division operation) which is good enough in our case.
    public class CompatibleConcurrentQueue<T> : ConcurrentQueue<T>, IQueue<T>
    {
        private int _queueCount = 0;

        public bool TryEnqueue(T element)
        {
            this.Enqueue(element);
            return true;
        }

        void IQueue<T>.Clear()
        {
            //T item;
            while (this.TryDequeue(out _)) { }
        }

        public new bool IsEmpty => Volatile.Read(ref _queueCount) == 0;

        public bool NonEmpty => Volatile.Read(ref _queueCount) > 0;

        public new int Count => Volatile.Read(ref _queueCount);

        //Note: The count is not updated atomically together with dequeueing.
        //This means that the count will be eventually consistent but it's not an issue since between the Count call and any other operation items may be enqueued or dequeued anyway and the count will no longer be valid.
        public new bool TryDequeue(out T result)
        {
            var dequeued = base.TryDequeue(out result);
            if (dequeued) { Interlocked.Decrement(ref _queueCount); }
            return dequeued;
        }

        //Note: The count is not updated atomically together with dequeueing.
        //This means that the count will be eventually consistent but it's not an issue since between the Count call and any other operation items may be enqueued or dequeued anyway and the count will no longer be valid.
        public new void Enqueue(T item)
        {
            base.Enqueue(item);
            Interlocked.Increment(ref _queueCount);
        }
    }
}