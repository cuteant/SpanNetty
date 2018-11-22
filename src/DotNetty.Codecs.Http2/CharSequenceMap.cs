// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    public sealed class CharSequenceMap<T> : DefaultHeaders<ICharSequence, T>
    {
        public CharSequenceMap()
            : this(true)
        {
        }

        public CharSequenceMap(bool caseSensitive)
            : this(caseSensitive, UnsupportedValueConverter<T>.Instance)
        {
        }

        public CharSequenceMap(bool caseSensitive, IValueConverter<T> valueConverter)
            : base(
                caseSensitive ? AsciiString.CaseSensitiveHasher : AsciiString.CaseInsensitiveHasher,
                valueConverter,
                NullNameValidator<ICharSequence>.Instance)
        {
        }

        public CharSequenceMap(bool caseSensitive, IValueConverter<T> valueConverter, int arraySizeHint)
            : base(
                caseSensitive ? AsciiString.CaseSensitiveHasher : AsciiString.CaseInsensitiveHasher,
                valueConverter,
                NullNameValidator<ICharSequence>.Instance,
                arraySizeHint)
        {
        }
    }
}