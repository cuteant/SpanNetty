// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;

    public static class ExecutionEnvironment
    {
        [ThreadStatic]
        static IEventExecutor s_currentExecutor;

        public static bool TryGetCurrentExecutor(out IEventExecutor executor)
        {
            executor = s_currentExecutor;
            return executor is object;
        }

        internal static void SetCurrentExecutor(IEventExecutor executor) => s_currentExecutor = executor;
    }
}