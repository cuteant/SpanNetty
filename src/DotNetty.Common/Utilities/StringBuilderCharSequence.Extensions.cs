// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;

    partial class StringBuilderCharSequence
    {
        public virtual void Dispose() { }

        bool IEquatable<ICharSequence>.Equals(ICharSequence other)
        {
            if (ReferenceEquals(this, other)) { return true; }

            if (other is StringBuilderCharSequence comparand)
            {
                return this.Count == comparand.Count && string.Equals(this.builder.ToString(this.offset, this.Count), comparand.builder.ToString(comparand.offset, this.Count), StringComparison.Ordinal);
            }

            return other is ICharSequence seq && this.ContentEquals(seq);
        }
    }
}
