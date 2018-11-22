// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using DotNetty.Common.Concurrency;
using DotNetty.Common.Internal.Logging;

namespace DotNetty.Buffers
{
    internal static class BuffersLoggingExtensions
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void FreedThreadLocalBufferFromThread(this IInternalLogger logger, int numFreed, XThread deathWatchThread)
        {
            logger.Debug("Freed {} thread-local buffer(s) from thread: {}", numFreed, deathWatchThread.Name);
        }
    }
}
