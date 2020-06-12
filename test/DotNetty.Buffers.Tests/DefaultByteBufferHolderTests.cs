// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using Xunit;

    public class DefaultByteBufferHolderTests
    {
        [Fact]
        public void ConvertToString()
        {
            var holder = new DefaultByteBufferHolder(Unpooled.Buffer());
            Assert.Equal(1, holder.ReferenceCount);
            Assert.NotNull(holder.ToString());
            Assert.True(holder.Release());
            Assert.NotNull(holder.ToString());
        }

        [Fact]
        public void EqualsAndHashCode()
        {
            var holder = new DefaultByteBufferHolder(Unpooled.Empty);
            IByteBufferHolder copy = holder.Copy();
            try
            {
                Assert.Equal(holder, copy);
                Assert.Equal(holder.GetHashCode(), copy.GetHashCode());
            }
            finally
            {
                holder.Release();
                copy.Release();
            }
        }

        [Fact]
        public void DifferentClassesAreNotEqual()
        {
            // all objects here have EMPTY_BUFFER data but are instances of different classes
            // so we want to check that none of them are equal to another.
            IByteBufferHolder dflt = new DefaultByteBufferHolder(Unpooled.Empty);
            IByteBufferHolder other = new OtherByteBufHolder(Unpooled.Empty, 123);
            IByteBufferHolder constant1 = new DefaultByteBufferHolder1(Unpooled.Empty);
            IByteBufferHolder constant2 = new DefaultByteBufferHolder2(Unpooled.Empty);
            try
            {
                // not using 'assertNotEquals' to be explicit about which object we are calling .equals() on
                Assert.False(dflt.Equals(other));
                Assert.False(dflt.Equals(constant1));
                Assert.False(constant1.Equals(dflt));
                Assert.False(constant1.Equals(other));
                Assert.False(constant1.Equals(constant2));
            }
            finally
            {
                dflt.Release();
                other.Release();
                constant1.Release();
                constant2.Release();
            }
        }

        sealed class DefaultByteBufferHolder1 : DefaultByteBufferHolder
        {
            public DefaultByteBufferHolder1(IByteBuffer data) : base(data) { }
        }

        sealed class DefaultByteBufferHolder2 : DefaultByteBufferHolder
        {
            public DefaultByteBufferHolder2(IByteBuffer data) : base(data) { }
        }

        sealed class OtherByteBufHolder : DefaultByteBufferHolder
        {
            readonly int _extraField;

            public OtherByteBufHolder(IByteBuffer data, int extraField)
                : base(data)
            {
                _extraField = extraField;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(this, obj)) { return true; }

                if (obj is null || GetType() != obj.GetType()) { return false; }

                if (!base.Equals(obj)) { return false; }

                return _extraField == ((OtherByteBufHolder)obj)._extraField;
            }

            public override int GetHashCode()
            {
                int result = base.GetHashCode();
                result = 31 * result + _extraField;
                return result;
            }
        }
    }
}
