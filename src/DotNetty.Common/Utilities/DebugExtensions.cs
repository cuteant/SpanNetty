// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System.Collections.Generic;
    using DotNetty.Common.Internal;

    public static class DebugExtensions
    {
        public static string ToDebugString<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            var sb = StringBuilderManager.Allocate();
            bool first = true;
            foreach (KeyValuePair<TKey, TValue> pair in dictionary)
            {
                if (first)
                {
                    first = false;
                    _ = sb.Append('{');
                }
                else
                {
                    _ = sb.Append(", ");
                }

                _ = sb.Append("{`").Append(pair.Key).Append("`: ").Append(pair.Value).Append('}');
            }
            _ = sb.Append('}');
            return StringBuilderManager.ReturnAndFree(sb);
        }
    }
}