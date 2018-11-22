// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System.Collections.Generic;
    using DotNetty.Common.Internal;

    public interface IPriorityQueue<T> : IQueue<T>, IEnumerable<T>
        where T : class, IPriorityQueueNode<T>
    {
        bool TryRemove(T item);

        bool Contains(T item);

        void PriorityChanged(T item);
    }
}