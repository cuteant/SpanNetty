// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System;
    using DotNetty.Common.Utilities;

    partial class AppendableCharSequence
    {
        public void Dispose() { }

        bool IEquatable<ICharSequence>.Equals(ICharSequence other)
        {
            if (ReferenceEquals(this, other)) { return true; }

            if (other is AppendableCharSequence comparand)
            {
                return this.pos == comparand.pos && PlatformDependent.ByteArrayEquals(this.chars, 0, comparand.chars, 0, this.pos);
            }

            return other is ICharSequence seq && this.ContentEquals(seq);
        }
    }
}
