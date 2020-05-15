// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace DotNetty.Common.Internal
{
    internal sealed class QueueDebugView<T>
    {
        private readonly QueueX<T> _queue;

        public QueueDebugView(QueueX<T> queue)
        {
            if (queue == null)
            {
                throw new ArgumentNullException(nameof(queue));
            }

            _queue = queue;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items
        {
            get
            {
                return _queue.ToArray();
            }
        }
    }
}
