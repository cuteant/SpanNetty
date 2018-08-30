// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Concurrency
{
    using System;
    using System.Collections.Generic;

    partial class SingleThreadEventExecutor
    {
        static readonly Action<object, object> AddShutdownHookAction = OnAddShutdownHook;
        static readonly Action<object, object> RemoveShutdownHookAction = OnRemoveShutdownHook;

        static void OnAddShutdownHook(object s, object a)
        {
            ((ISet<Action>)s).Add((Action)a);
        }

        static void OnRemoveShutdownHook(object s, object a)
        {
            ((ISet<Action>)s).Remove((Action)a);
        }
    }
}