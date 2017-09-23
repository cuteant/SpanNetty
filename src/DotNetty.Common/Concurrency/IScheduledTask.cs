// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
#if !NET40
  using System.Runtime.CompilerServices;
#else
  using Microsoft.Runtime.CompilerServices;
#endif
    using System.Threading.Tasks;

    public interface IScheduledTask
    {
        bool Cancel();

        PreciseTimeSpan Deadline { get; }

        Task Completion { get; }

        TaskAwaiter GetAwaiter();
    }
}