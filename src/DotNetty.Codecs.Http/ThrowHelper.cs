// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;

    static class ThrowHelper
    {
        internal static void ThrowArgumentException(string message) => throw new ArgumentException(message);

        internal static void ThrowTooLongFrameException(string message) => throw new TooLongFrameException(message);
    }
}
