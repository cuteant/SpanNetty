// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    public interface IScheduledTask
    {
        long Id { get; }

        long DelayNanos { get; }

        long DeadlineNanos { get; }

        PreciseTimeSpan Deadline { get; }

        Task Completion { get; }

        bool Cancel();

        TaskAwaiter GetAwaiter();
    }
}