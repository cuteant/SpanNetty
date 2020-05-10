// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    class HpackHeaderField
    {
        // Section 4.1. Calculating Table Size
        // The additional 32 octets account for an estimated
        // overhead associated with the structure.
        internal const int HeaderEntryOverhead = 32;

        internal static long SizeOf(ICharSequence name, ICharSequence value)
        {
            return name.Count + value.Count + HeaderEntryOverhead;
        }

        internal readonly ICharSequence name;
        internal readonly ICharSequence value;

        /// <summary>
        /// This constructor can only be used if name and value are ISO-8859-1 encoded.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        internal HpackHeaderField(ICharSequence name, ICharSequence value)
        {
            if (name is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.name); }
            if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }

            this.name = name;
            this.value = value;
        }

        internal int Size()
        {
            return this.name.Count + this.value.Count + HeaderEntryOverhead;
        }

        public sealed override int GetHashCode()
        {
            // TODO(nmittler): Netty's build rules require this. Probably need a better implementation.
            return base.GetHashCode();
        }

        public sealed override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) { return true; }

            var other = obj as HpackHeaderField;
            if (other is null) { return false; }

            // To avoid short circuit behavior a bitwise operator is used instead of a bool operator.
            return (HpackUtil.EqualsConstantTime(this.name, other.name) & HpackUtil.EqualsConstantTime(this.value, other.value)) != 0;
        }

        public override string ToString()
        {
            return this.name + ": " + this.value;
        }
    }
}