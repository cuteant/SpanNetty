// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;

    partial class StringBuilderCharSequence
    {
        bool IEquatable<ICharSequence>.Equals(ICharSequence other)
        {
            if (ReferenceEquals(this, other)) { return true; }

            if (other is StringBuilderCharSequence comparand)
            {
                return this.size == comparand.size && string.Equals(this.builder.ToString(this.offset, this.size), comparand.builder.ToString(comparand.offset, this.size), StringComparison.Ordinal);
            }

            return other is ICharSequence seq && this.ContentEquals(seq);
        }

#if NETCOREAPP
        public void Append(ReadOnlySpan<char> value)
        {
            this.builder.Append(value);
            this.size += value.Length;
        }
#endif

#if !NET40
        public ReadOnlySpan<char> Span => this.builder.ToString(this.offset, this.size).AsSpan();
#endif
    }
}
