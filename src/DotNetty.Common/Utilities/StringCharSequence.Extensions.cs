// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;

    partial class StringCharSequence
    {
        public void Dispose() { }

        bool IEquatable<ICharSequence>.Equals(ICharSequence other)
        {
            if (ReferenceEquals(this, other)) { return true; }

            if (other is StringCharSequence comparand)
            {
                return this.count == comparand.count && string.Compare(this.value, this.offset, comparand.value, comparand.offset, this.count, StringComparison.Ordinal) == 0;
            }

            return other is ICharSequence seq && this.ContentEquals(seq);
        }
    }
}
