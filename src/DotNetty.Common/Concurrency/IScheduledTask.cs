// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
  using System.Threading.Tasks;
#if NET_4_0_GREATER
  using System.Runtime.CompilerServices;
#else
  using Microsoft.Runtime.CompilerServices;
#endif

  public interface IScheduledTask
  {
    bool Cancel();

    PreciseTimeSpan Deadline { get; }

    Task Completion { get; }

    TaskAwaiter GetAwaiter();
  }
}