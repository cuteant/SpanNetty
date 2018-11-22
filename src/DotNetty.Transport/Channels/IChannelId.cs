// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;

    public interface IChannelId : IEquatable<IChannelId>, IComparable<IChannelId>
    {
        /// <summary>
        /// Returns the short but globally non-unique string representation of the <see cref="IChannelId"/>.
        /// </summary>
        string AsShortText();

        /// <summary>
        /// Returns the long yet globally unique string representation of the <see cref="IChannelId"/>.
        /// </summary>
        string AsLongText();
    }
}