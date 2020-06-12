// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Groups
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public class ChannelGroupException : ChannelException, IEnumerable<KeyValuePair<IChannel, Exception>>
    {
        readonly IReadOnlyCollection<KeyValuePair<IChannel, Exception>> _failed;

        public ChannelGroupException(IList<KeyValuePair<IChannel, Exception>> exceptions)
        {
            if (exceptions is null)
            {
                ThrowHelper.ThrowArgumentNullException(ExceptionArgument.exceptions);
            }
            if (0u >= (uint)exceptions.Count)
            {
                ThrowHelper.ThrowArgumentException_Excs();
            }
            _failed = new ReadOnlyCollection<KeyValuePair<IChannel, Exception>>(exceptions);
        }

        public IEnumerator<KeyValuePair<IChannel, Exception>> GetEnumerator() => _failed.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _failed.GetEnumerator();
    }
}