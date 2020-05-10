// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET40

namespace DotNetty.Common.Internal
{
    using System;
    using DotNetty.Common.Utilities;

    partial class AppendableCharSequence
    {
        public bool Equals(AppendableCharSequence other)
        {
            //if (other is null)
            //{
            //    return false;
            //}
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other is object && this.pos == other.pos 
                && PlatformDependent.ByteArrayEquals(this.chars, 0, other.chars, 0, this.pos);
        }

        public override bool Equals(object obj)
        {
            //if (obj is null)
            //{
            //    return false;
            //}
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is AppendableCharSequence other)
            {
                return this.pos == other.pos && PlatformDependent.ByteArrayEquals(this.chars, 0, other.chars, 0, this.pos);
            }
            if (obj is ICharSequence seq)
            {
                return this.ContentEquals(seq);
            }

            return false;
        }

        bool IEquatable<ICharSequence>.Equals(ICharSequence other)
        {
            if (ReferenceEquals(this, other)) { return true; }

            if (other is null) { return false; }

            if (other is AppendableCharSequence comparand)
            {
                return this.pos == comparand.pos && PlatformDependent.ByteArrayEquals(this.chars, 0, comparand.chars, 0, this.pos);
            }

            return this.ContentEquals(other);
        }
    }
}

#endif
