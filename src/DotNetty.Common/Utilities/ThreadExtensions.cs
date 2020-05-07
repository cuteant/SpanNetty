// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using Thread = DotNetty.Common.Concurrency.XThread;

    public static class ThreadExtensions
    {
        public static bool Join(this Thread thread, TimeSpan timeout)
        {
            long tm = (long)timeout.TotalMilliseconds;
            if (/*tm < 0 ||*/ (ulong)tm > int.MaxValue) { ThrowHelper.ThrowIndexOutOfRangeException(); }

            return thread.Join((int)tm);
        }
    }
}