// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Runtime.CompilerServices;

    partial class CharUtil
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowFormatException0(ICharSequence str)
        {
            throw GetFormatException();
            FormatException GetFormatException()
            {
                return new FormatException(str.ToString());
            }
        }
    }
}
