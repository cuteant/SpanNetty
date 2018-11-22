// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    public interface IHeaderEntry<TKey, TValue>
    {
        TKey Key { get; }

        TValue Value { get; }
    }
}
