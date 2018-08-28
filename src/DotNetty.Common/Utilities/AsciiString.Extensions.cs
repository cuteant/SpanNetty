// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using DotNetty.Common.Internal;

    partial class AsciiString
    {
        public void Dispose() { }

        bool IEquatable<ICharSequence>.Equals(ICharSequence other)
        {
            if (ReferenceEquals(this, other)) { return true; }

            if (other is AsciiString comparand)
            {
                return this.length == comparand.length
                    && this.GetHashCode() == comparand.GetHashCode()
                    && PlatformDependent.ByteArrayEquals(this.value, this.offset, comparand.value, comparand.offset, this.length);
            }

            return other is ICharSequence seq && this.ContentEquals(seq);
        }
    }
}
