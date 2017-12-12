// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Tests
{
    using System;
    using System.Threading.Tasks;
    using Xunit;

    public class ThreadLocalObjectPoolTest
    {
        [Fact]
        public void MultipleReleaseTest()
        {
            RecyclableObject obj = RecyclableObject.NewInstance();
            RecyclableObject.Return(obj);
            var exception = Assert.ThrowsAny<InvalidOperationException>(() => RecyclableObject.Return(obj));
            Assert.True(exception != null);
        }

        [Fact]
        public void ReleaseTest()
        {
            RecyclableObject obj = RecyclableObject.NewInstance();
            RecyclableObject.Return(obj);
            RecyclableObject obj2 = RecyclableObject.NewInstance();
            Assert.Same(obj, obj2);
            RecyclableObject.Return(obj2);
        }

        [Fact]
        public void RecycleAtDifferentThreadTest()
        {
            RecyclableObject obj = RecyclableObject.NewInstance();

            RecyclableObject prevObject = obj;
            Task.Run(() => { RecyclableObject.Return(obj); }).Wait();
            obj = RecyclableObject.NewInstance();

            Assert.True(obj == prevObject);
            RecyclableObject.Return(obj);
        }

        class RecyclableObjectPolicy : IThreadLocalPooledObjectPolicy<RecyclableObject>
        {
            public bool PreCreate => true;

            public Func<ThreadLocalPool.Handle, RecyclableObject> ValueFactory => handle => new RecyclableObject(handle);

            public RecyclableObject Create()
            {
                throw new NotImplementedException();
            }

            public RecyclableObject PreGetting(RecyclableObject obj) => obj;

            public bool Return(RecyclableObject obj) => true;
        }

        class RecyclableObject : IThreadLocalPooledObjectRecycling
        {
            internal static readonly ThreadLocalObjectPool<RecyclableObject> pool =
                new ThreadLocalObjectPool<RecyclableObject>(new RecyclableObjectPolicy(), 1);

            readonly ThreadLocalPool.Handle handle;

            public RecyclableObject(ThreadLocalPool.Handle handle)
            {
                this.handle = handle;
            }

            public static RecyclableObject NewInstance() => pool.Take();
            public static void Return(RecyclableObject obj) => pool.Return(obj);

            public void Recycle() => handle.Release(this);
        }
    }
}