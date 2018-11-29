
namespace DotNetty.Buffers.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using Moq;
    using Xunit;

    public class ReadOnlyByteBufferTest
    {
        [Fact]
        public void ShouldNotAllowNullInConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new ReadOnlyByteBuffer(null));
        }

        [Fact]
        public void TestUnmodifiableBuffer()
        {
            Assert.True(Unpooled.UnmodifiableBuffer(Unpooled.Buffer(1)) is ReadOnlyByteBuffer);
        }

        [Fact]
        public void TestUnwrap()
        {
            var buf = Unpooled.Buffer(1);
            Assert.Same(buf, Unpooled.UnmodifiableBuffer(buf).Unwrap());
        }

        [Fact]
        public void ShouldReturnReadOnlyDerivedBuffer()
        {
            var buf = Unpooled.UnmodifiableBuffer(Unpooled.Buffer(1));
            Assert.True(buf.Duplicate() is ReadOnlyByteBuffer);
            Assert.True(buf.Slice() is ReadOnlyByteBuffer);
            Assert.True(buf.Slice(0, 1) is ReadOnlyByteBuffer);
            Assert.True(buf.Duplicate() is ReadOnlyByteBuffer);
        }

        [Fact]
        public void ShouldReturnWritableCopy()
        {
            var buf = Unpooled.UnmodifiableBuffer(Unpooled.Buffer(1));
            Assert.False(buf.Copy() is ReadOnlyByteBuffer);
        }

        [Fact]
        public void ShouldForwardReadCallsBlindly()
        {
            var buf = new Mock<IByteBuffer>();
            buf.Setup(x => x.MaxCapacity).Returns(65536);
            buf.Setup(x => x.ReaderIndex).Returns(0);
            buf.Setup(x => x.WriterIndex).Returns(0);
            buf.Setup(x => x.Capacity).Returns(0);

            buf.Setup(x => x.GetBytes(
                It.Is<int>(v => v == 4),
                It.IsAny<Stream>(),
                It.Is<int>(v => v == 5)))
               .Returns(buf.Object);

            buf.Setup(x => x.GetBytes(
                It.Is<int>(v => v == 6),
                It.IsAny<byte[]>(),
                It.Is<int>(v => v == 7),
                It.Is<int>(v => v == 8)))
               .Returns(buf.Object);

            buf.Setup(x => x.GetBytes(
                It.Is<int>(v => v == 9),
                It.IsAny<IByteBuffer>(),
                It.Is<int>(v => v == 10),
                It.Is<int>(v => v == 11)))
               .Returns(buf.Object);

            buf.Setup(x => x.GetByte(It.Is<int>(v => v == 13))).Returns(14);
            buf.Setup(x => x.GetShort(It.Is<int>(v => v == 15))).Returns(16);
            buf.Setup(x => x.GetUnsignedMedium(It.Is<int>(v => v == 17))).Returns(18);
            buf.Setup(x => x.GetInt(It.Is<int>(v => v == 19))).Returns(20);
            buf.Setup(x => x.GetLong(It.Is<int>(v => v == 21))).Returns(22L);

            var bb = new ArraySegment<byte>(new byte[100]);
            buf.Setup(x => x.GetIoBuffer(
                It.Is<int>(v => v == 23),
                It.Is<int>(v => v == 24)))
               .Returns(bb);
            buf.Setup(x => x.Capacity).Returns(27);

            var roBuf = Unpooled.UnmodifiableBuffer(buf.Object);
            roBuf.GetBytes(4, (Stream)null, 5);
            roBuf.GetBytes(6, (byte[])null, 7, 8);
            roBuf.GetBytes(9, (IByteBuffer)null, 10, 11);
            Assert.Equal((byte)14, roBuf.GetByte(13));
            Assert.Equal((short)16, roBuf.GetShort(15));
            Assert.Equal(18, roBuf.GetUnsignedMedium(17));
            Assert.Equal(20, roBuf.GetInt(19));
            Assert.Equal(22L, roBuf.GetLong(21));

            var roBB = roBuf.GetIoBuffer(23, 24);
            Assert.Equal(bb.Count, roBB.Count);

            Assert.Equal(27, roBuf.Capacity);
        }

        [Fact]
        public void ShouldRejectDiscardReadBytes()
        {
            Assert.Throws<ReadOnlyBufferException>(() => Unpooled.UnmodifiableBuffer(Unpooled.Empty).DiscardReadBytes());
            Assert.Throws<ReadOnlyBufferException>(() => Unpooled.UnmodifiableBuffer(Unpooled.Empty).DiscardSomeReadBytes());
        }

        [Fact]
        public void ShouldRejectSetByte()
        {
            Assert.Throws<ReadOnlyBufferException>(() => Unpooled.UnmodifiableBuffer(Unpooled.Empty).SetByte(0, (byte)0));
        }

        [Fact]
        public void ShouldRejectSetShort()
        {
            Assert.Throws<ReadOnlyBufferException>(() => Unpooled.UnmodifiableBuffer(Unpooled.Empty).SetShort(0, (short)0));
        }

        [Fact]
        public void ShouldRejectSetMedium()
        {
            Assert.Throws<ReadOnlyBufferException>(() => Unpooled.UnmodifiableBuffer(Unpooled.Empty).SetMedium(0, 0));
        }

        [Fact]
        public void ShouldRejectSetInt()
        {
            Assert.Throws<ReadOnlyBufferException>(() => Unpooled.UnmodifiableBuffer(Unpooled.Empty).SetInt(0, 0));
        }

        [Fact]
        public void ShouldRejectSetLong()
        {
            Assert.Throws<ReadOnlyBufferException>(() => Unpooled.UnmodifiableBuffer(Unpooled.Empty).SetLong(0, 0));
        }

        [Fact]
        public void ShouldRejectSetBytes1()
        {
            Assert.ThrowsAsync<ReadOnlyBufferException>(() => Unpooled.UnmodifiableBuffer(Unpooled.Empty).SetBytesAsync(0, (System.IO.Stream)null, 0, CancellationToken.None));
        }

        [Fact]
        public void ShouldRejectSetBytes3()
        {
            Assert.Throws<ReadOnlyBufferException>(() => Unpooled.UnmodifiableBuffer(Unpooled.Empty).SetBytes(0, (byte[])null, 0, 0));
        }

        [Fact]
        public void ShouldRejectSetBytes4()
        {
            Assert.Throws<ReadOnlyBufferException>(() => Unpooled.UnmodifiableBuffer(Unpooled.Empty).SetBytes(0, (IByteBuffer)null, 0, 0));
        }

        [Fact]
        public void ShouldIndicateNotWritable()
        {
            Assert.False(Unpooled.UnmodifiableBuffer(Unpooled.Buffer(1)).IsWritable());
        }

        [Fact]
        public void ShouldIndicateNotWritableAnyNumber()
        {
            Assert.False(Unpooled.UnmodifiableBuffer(Unpooled.Buffer(1)).IsWritable(1));
        }

        [Fact]
        public void EnsureWritableIntStatusShouldFailButNotThrow()
        {
            EnsureWritableIntStatusShouldFailButNotThrow0(false);
        }

        [Fact]
        public void EnsureWritableForceIntStatusShouldFailButNotThrow()
        {
            EnsureWritableIntStatusShouldFailButNotThrow0(true);
        }

        private static void EnsureWritableIntStatusShouldFailButNotThrow0(bool force)
        {
            var buf = Unpooled.Buffer(1);
            var readOnly = buf.AsReadOnly();
            int result = readOnly.EnsureWritable(1, force);
            Assert.Equal(1, result);
            Assert.False(ByteBufferUtil.EnsureWritableSuccess(result));
            readOnly.Release();
        }

        [Fact]
        public void EnsureWritableShouldThrow()
        {
            var buf = Unpooled.Buffer(1);
            var readOnly = buf.AsReadOnly();
            try
            {
                readOnly.EnsureWritable(1);
                Assert.False(true);
            }
            catch (Exception ex)
            {
                Assert.True(ex is ReadOnlyBufferException);
            }
            finally
            {
                buf.Release();
            }
        }

        [Fact]
        public void AsReadOnly()
        {
            var buf = Unpooled.Buffer(1);
            var readOnly = buf.AsReadOnly();
            Assert.True(readOnly.IsReadOnly);
            Assert.Same(readOnly, readOnly.AsReadOnly());
            readOnly.Release();
        }
    }
}
