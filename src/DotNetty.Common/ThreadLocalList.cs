// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System.Collections.Generic;

    public class ThreadLocalList<T> : List<T>
    {
        const int DefaultInitialCapacity = 8;

        static readonly ThreadLocalPool<ThreadLocalList<T>> Pool = new ThreadLocalPool<ThreadLocalList<T>>(handle => new ThreadLocalList<T>(handle));

        readonly ThreadLocalPool.Handle returnHandle;

        ThreadLocalList(ThreadLocalPool.Handle returnHandle)
        {
            this.returnHandle = returnHandle;
        }

        public static ThreadLocalList<T> NewInstance() => Pool.Take();

        public static ThreadLocalList<T> NewInstance(int minCapacity)
        {
            var ret = Pool.Take();
            if (ret.Capacity < minCapacity)
            {
                ret.Capacity = minCapacity;
            }
            return ret;

        }

        public void Return()
        {
            this.Clear();
            this.returnHandle.Release(this);
        }
    }
}