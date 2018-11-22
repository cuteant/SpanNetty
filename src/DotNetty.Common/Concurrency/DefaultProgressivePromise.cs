// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class DefaultProgressivePromise : TaskCompletionSource, IProgressivePromise
    {

        public DefaultProgressivePromise() : base() { }

        public DefaultProgressivePromise(object state) : base(state) { }


        public void SetProgress(long progress, long total)
        {
            if (total < 0)
            {
                // total unknown
                total = -1; // normalize
                if (progress < 0)
                {
                    throw new ArgumentException("progress: " + progress + " (expected: >= 0)");
                }
            }
            else if (progress < 0 || progress > total)
            {
                throw new ArgumentException(
                        "progress: " + progress + " (expected: 0 <= progress <= total (" + total + "))");
            }

            if (this.IsCompleted)
            {
                throw new InvalidOperationException("complete already");
            }

            //notifyProgressiveListeners(progress, total);
        }

        public bool TryProgress(long progress, long total)
        {
            if (total < 0)
            {
                total = -1;
                if (progress < 0 || this.IsCompleted)
                {
                    return false;
                }
            }
            else if (progress < 0 || progress > total || this.IsCompleted)
            {
                return false;
            }

            //notifyProgressiveListeners(progress, total);
            return true;
        }
    }
}
