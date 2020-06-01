// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Text;
    using DotNetty.Common;
    using Xunit;

    public class AdvancedLeakAwareByteBufferTests : SimpleLeakAwareByteBufferTests
    {
        protected override Type ByteBufferType => typeof(AdvancedLeakAwareByteBuffer);

        protected override IByteBuffer Wrap(IByteBuffer buffer, IResourceLeakTracker tracker) => new AdvancedLeakAwareByteBuffer(buffer, tracker);

        [Fact]
        public void AddComponentWithLeakAwareByteBuf()
        {
            NoopResourceLeakTracker tracker = new NoopResourceLeakTracker();

            var buffer = Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes("hello world")).Slice(6, 5);
            var leakAwareBuf = Wrap(buffer, tracker);

            var composite = Unpooled.CompositeBuffer();
            composite.AddComponent(true, leakAwareBuf);
            byte[] result = new byte[5];
            IByteBuffer bb = composite[0];
            //System.out.println(bb);
            bb.ReadBytes(result);
            Assert.Equal(Encoding.ASCII.GetBytes("world"), result);
            composite.Release();
        }
    }
}
