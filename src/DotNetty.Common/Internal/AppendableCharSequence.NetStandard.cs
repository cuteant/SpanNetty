// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using DotNetty.Common.Utilities;

    partial class AppendableCharSequence : IHasAsciiSpan
    {
        public ReadOnlySpan<byte> AsciiSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var count = _pos;
                if (0u >= (uint)count) { return ReadOnlySpan<byte>.Empty; }
                return new ReadOnlySpan<byte>(_chars, 0, count);
            }
        }

        public bool Equals(AppendableCharSequence other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other is object && _pos == other._pos
                && SpanHelpers.SequenceEqual(ref MemoryMarshal.GetReference(AsciiSpan), ref MemoryMarshal.GetReference(other.AsciiSpan), _pos);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj)) { return true; }

            switch (obj)
            {
                case AppendableCharSequence other:
                    return _pos == other._pos
                        && SpanHelpers.SequenceEqual(ref MemoryMarshal.GetReference(AsciiSpan), ref MemoryMarshal.GetReference(other.AsciiSpan), _pos);

                case IHasAsciiSpan hasAscii:
                    return AsciiSpan.SequenceEqual(hasAscii.AsciiSpan);

                case ICharSequence seq:
                    return ContentEquals(seq);

                default:
                    return false;
            }
        }

        bool IEquatable<ICharSequence>.Equals(ICharSequence other)
        {
            if (ReferenceEquals(this, other)) { return true; }

            switch (other)
            {
                case null:
                    return false;

                case AppendableCharSequence comparand:
                    return _pos == comparand._pos
                        && SpanHelpers.SequenceEqual(ref MemoryMarshal.GetReference(AsciiSpan), ref MemoryMarshal.GetReference(comparand.AsciiSpan), _pos);

                case IHasAsciiSpan hasAscii:
                    return AsciiSpan.SequenceEqual(hasAscii.AsciiSpan);

                default:
                    return false;
            }
        }
    }
}
