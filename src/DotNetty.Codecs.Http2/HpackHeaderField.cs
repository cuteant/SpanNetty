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

        internal readonly ICharSequence _name;
        internal readonly ICharSequence _value;

        /// <summary>
        /// This constructor can only be used if name and value are ISO-8859-1 encoded.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        internal HpackHeaderField(ICharSequence name, ICharSequence value)
        {
            if (name is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.name); }
            if (value is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.value); }

            _name = name;
            _value = value;
        }

        internal int Size()
        {
            return _name.Count + _value.Count + HeaderEntryOverhead;
        }

        public bool EqualsForTest(HpackHeaderField other)
        {
            return HpackUtil.EqualsVariableTime(_name, other._name) && HpackUtil.EqualsVariableTime(_value, other._value);
        }

        public override string ToString()
        {
            return _name + ": " + _value;
        }
    }
}