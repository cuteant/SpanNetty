// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;

    /// <summary>
    /// Raised by <see cref="MessageAggregator{TMessage, TStart, TContent, TOutput}"/> when aggregation fails due to an unexpected message sequence.
    /// </summary>
    public class MessageAggregationException : InvalidOperationException
    {
        public MessageAggregationException(string message)
            : base(message)
        {
        }

        public MessageAggregationException(string message, Exception cause)
            : base(message, cause)
        {
        }
    }
}
