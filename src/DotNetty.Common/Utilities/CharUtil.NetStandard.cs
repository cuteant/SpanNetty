// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Common.Utilities
{
    using System;
    using System.Collections.Generic;

    partial class CharUtil
    {
        public static ICharSequence[] Split(ICharSequence sequence, int startIndex, params char[] delimiters)
        {
            if (sequence is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.sequence); }
            if (delimiters is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.delimiters); }
            int length = sequence.Count;
            uint uLength = (uint)length;
            if (0u >= uLength) { return new[] { sequence }; }
            if ((uint)startIndex >= uLength) { ThrowHelper.ThrowIndexOutOfRangeException(); }

            var delimitersSpan = delimiters.AsSpan();
            List<ICharSequence> result = InternalThreadLocalMap.Get().CharSequenceList();

            int i = startIndex;

            while ((uint)i < uLength)
            {
                while ((uint)i < uLength && delimitersSpan.IndexOf(sequence[i]) >= 0)
                {
                    i++;
                }

                int position = i;
                if ((uint)i < uLength)
                {
                    if (delimitersSpan.IndexOf(sequence[position]) >= 0)
                    {
                        result.Add(sequence.SubSequence(position++, i + 1));
                    }
                    else
                    {
                        ICharSequence seq = null;
                        for (position++; position < length; position++)
                        {
                            if (delimitersSpan.IndexOf(sequence[position]) >= 0)
                            {
                                seq = sequence.SubSequence(i, position);
                                break;
                            }
                        }
                        result.Add(seq ?? sequence.SubSequence(i));
                    }
                    i = position;
                }
            }

            return result.Count == 0 ? new[] { sequence } : result.ToArray();
        }
    }
}

#endif