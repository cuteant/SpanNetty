// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40

namespace DotNetty.Common.Internal
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Utilities;

    partial class AppendableCharSequence : IHasAsciiSpan
    {
        public ReadOnlySpan<byte> AsciiSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var count = this.pos;
                if (0u >= (uint)count) { return ReadOnlySpan<byte>.Empty; }
                return new ReadOnlySpan<byte>(this.chars, 0, count);
            }
        }

        public bool Equals(AppendableCharSequence other)
        {
            //if (other == null)
            //{
            //    return false;
            //}
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other != null && this.pos == other.pos
                && this.AsciiSpan.SequenceEqual(other.AsciiSpan);
        }

        public override bool Equals(object obj)
        {
            //if (obj == null)
            //{
            //    return false;
            //}
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is AppendableCharSequence other)
            {
                return this.pos == other.pos && this.AsciiSpan.SequenceEqual(other.AsciiSpan);
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

            if (null == other) { return false; }

            if (other is AppendableCharSequence comparand)
            {
                return this.pos == comparand.pos && this.AsciiSpan.SequenceEqual(comparand.AsciiSpan);
            }

            return this.ContentEquals(other);
        }
    }
}

#endif
