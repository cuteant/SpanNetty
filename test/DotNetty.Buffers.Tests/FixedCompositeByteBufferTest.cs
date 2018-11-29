
namespace DotNetty.Buffers.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using Xunit;

    public class FixedCompositeByteBufferTest
    {
        private static IByteBuffer NewBuffer(params IByteBuffer[] buffers)
        {
            return new FixedCompositeByteBuf(UnpooledByteBufferAllocator.Default, buffers);
        }

        [Fact]
        public void TestSetBoolean()
        {
            IByteBuffer buf = NewBuffer(Unpooled.WrappedBuffer(new byte[8]));
            Assert.Throws<ReadOnlyBufferException>(() =>
            {
                try
                {
                    buf.SetBoolean(0, true);
                }
                finally
                {
                    buf.Release();
                }
            });
        }

        [Fact]
        public void TestSetByte()
        {
            IByteBuffer buf = NewBuffer(Unpooled.WrappedBuffer(new byte[8]));
            Assert.Throws<ReadOnlyBufferException>(() =>
            {
                try
                {
                    buf.SetByte(0, 1);
                }
                finally
                {
                    buf.Release();
                }
            });
        }

        [Fact]
        public void TestSetBytesWithByteBuf()
        {
            IByteBuffer buf = NewBuffer(Unpooled.WrappedBuffer(new byte[8]));
            IByteBuffer src = Unpooled.WrappedBuffer(new byte[4]);
            Assert.Throws<ReadOnlyBufferException>(() =>
            {
                try
                {
                    buf.SetBytes(0, src);
                }
                finally
                {
                    buf.Release();
                    src.Release();
                }
            });
        }

        [Fact]
        public void TestSetBytesWithInputStream()
        {
            IByteBuffer buf = NewBuffer(Unpooled.WrappedBuffer(new byte[8]));
            Assert.ThrowsAsync<ReadOnlyBufferException>(async () =>
            {
                try
                {
                    await buf.SetBytesAsync(0, new MemoryStream(new byte[4]), 4, CancellationToken.None);
                }
                finally
                {
                    buf.Release();
                }
            });
        }

        [Fact]
        public void TestSetChar()
        {
            IByteBuffer buf = NewBuffer(Unpooled.WrappedBuffer(new byte[8]));
            Assert.Throws<ReadOnlyBufferException>(() =>
            {
                try
                {
                    buf.SetChar(0, 'b');
                }
                finally
                {
                    buf.Release();
                }
            });
        }

        [Fact]
        public void TestSetDouble()
        {
            IByteBuffer buf = NewBuffer(Unpooled.WrappedBuffer(new byte[8]));
            Assert.Throws<ReadOnlyBufferException>(() =>
            {
                try
                {
                    buf.SetDouble(0, 1);
                }
                finally
                {
                    buf.Release();
                }
            });
        }

        [Fact]
        public void TestSetFloat()
        {
            IByteBuffer buf = NewBuffer(Unpooled.WrappedBuffer(new byte[8]));
            Assert.Throws<ReadOnlyBufferException>(() =>
            {
                try
                {
                    buf.SetFloat(0, 1);
                }
                finally
                {
                    buf.Release();
                }
            });
        }

        [Fact]
        public void TestSetInt()
        {
            IByteBuffer buf = NewBuffer(Unpooled.WrappedBuffer(new byte[8]));
            Assert.Throws<ReadOnlyBufferException>(() =>
            {
                try
                {
                    buf.SetInt(0, 1);
                }
                finally
                {
                    buf.Release();
                }
            });
        }

        [Fact]
        public void TestSetLong()
        {
            IByteBuffer buf = NewBuffer(Unpooled.WrappedBuffer(new byte[8]));
            Assert.Throws<ReadOnlyBufferException>(() =>
            {
                try
                {
                    buf.SetLong(0, 1);
                }
                finally
                {
                    buf.Release();
                }
            });
        }

        [Fact]
        public void TestSetMedium()
        {
            IByteBuffer buf = NewBuffer(Unpooled.WrappedBuffer(new byte[8]));
            Assert.Throws<ReadOnlyBufferException>(() => 
            {
                try
                {
                    buf.SetMedium(0, 1);
                }
                finally
                {
                    buf.Release();
                }
            });
        }

        [Fact]
        public void TestCopyingToOtherBuffer()
        {
            IByteBuffer buf1 = Unpooled.DirectBuffer(10);
            IByteBuffer buf2 = Unpooled.Buffer(10);
            IByteBuffer buf3 = Unpooled.DirectBuffer(10);
            buf1.WriteBytes(Encoding.ASCII.GetBytes("a"));
            buf2.WriteBytes(Encoding.ASCII.GetBytes("b"));
            buf3.WriteBytes(Encoding.ASCII.GetBytes("c"));
            IByteBuffer composite = Unpooled.WrappedUnmodifiableBuffer(buf1, buf2, buf3);
            IByteBuffer copy = Unpooled.DirectBuffer(3);
            IByteBuffer copy2 = Unpooled.Buffer(3);
            copy.SetBytes(0, composite, 0, 3);
            copy2.SetBytes(0, composite, 0, 3);
            copy.SetWriterIndex(3);
            copy2.SetWriterIndex(3);
            Assert.Equal(0, ByteBufferUtil.Compare(copy, composite));
            Assert.Equal(0, ByteBufferUtil.Compare(copy2, composite));
            Assert.Equal(0, ByteBufferUtil.Compare(copy, copy2));
            copy.Release();
            copy2.Release();
            composite.Release();
        }

        [Fact]
        public void TestCopyingToOutputStream()
        {
            IByteBuffer buf1 = Unpooled.DirectBuffer(10);
            IByteBuffer buf2 = Unpooled.Buffer(10);
            IByteBuffer buf3 = Unpooled.DirectBuffer(10);
            buf1.WriteBytes(Encoding.ASCII.GetBytes("a"));
            buf2.WriteBytes(Encoding.ASCII.GetBytes("b"));
            buf3.WriteBytes(Encoding.ASCII.GetBytes("c"));
            IByteBuffer composite = Unpooled.WrappedUnmodifiableBuffer(buf1, buf2, buf3);
            IByteBuffer copy = Unpooled.DirectBuffer(3);
            IByteBuffer copy2 = Unpooled.Buffer(3);
            var copyStream = new ByteBufferStream(copy);
            var copy2Stream = new ByteBufferStream(copy2);
            try
            {
                composite.GetBytes(0, copyStream, 3);
                composite.GetBytes(0, copy2Stream, 3);
                Assert.Equal(0, ByteBufferUtil.Compare(copy, composite));
                Assert.Equal(0, ByteBufferUtil.Compare(copy2, composite));
                Assert.Equal(0, ByteBufferUtil.Compare(copy, copy2));
            }
            finally
            {
                copy.Release();
                copy2.Release();
                copyStream.Close();
                copy2Stream.Close();
                composite.Release();
            }
        }

        [Fact]
        public void TestExtractNioBuffers()
        {
            IByteBuffer buf1 = Unpooled.DirectBuffer(10);
            IByteBuffer buf2 = Unpooled.Buffer(10);
            IByteBuffer buf3 = Unpooled.DirectBuffer(10);
            buf1.WriteBytes(Encoding.ASCII.GetBytes("a"));
            buf2.WriteBytes(Encoding.ASCII.GetBytes("b"));
            buf3.WriteBytes(Encoding.ASCII.GetBytes("c"));
            IByteBuffer composite = Unpooled.WrappedUnmodifiableBuffer(buf1, buf2, buf3);
            var byteBuffers = composite.GetIoBuffers(0, 3);
            Assert.Equal(3, byteBuffers.Length);
            Assert.Single(byteBuffers[0]);
            Assert.Single(byteBuffers[1]);
            Assert.Single(byteBuffers[2]);
            composite.Release();
        }

        [Fact]
        public void TestEmptyArray()
        {
            IByteBuffer buf = NewBuffer(new IByteBuffer[0]);
            buf.Release();
        }

        [Fact]
        public void TestHasMemoryAddressWithSingleBuffer()
        {
            IByteBuffer buf1 = Unpooled.DirectBuffer(10);
            if (!buf1.HasMemoryAddress)
            {
                buf1.Release();
                return;
            }
            IByteBuffer buf = NewBuffer(buf1);
            Assert.True(buf.HasMemoryAddress);
            Assert.Equal(buf1.GetPinnableMemoryAddress(), buf.GetPinnableMemoryAddress());
            buf.Release();
        }

        [Fact]
        public void TestHasMemoryAddressWhenEmpty()
        {
            Assert.False(Unpooled.Empty.HasMemoryAddress);
            //IByteBuffer buf = NewBuffer(new IByteBuffer[0]);
            //Assert.True(buf.HasMemoryAddress);
            //Assert.Equal(Unpooled.Empty.GetPinnableMemoryAddress(), buf.GetPinnableMemoryAddress());
            //buf.Release();
        }

        [Fact]
        public void TestHasNoMemoryAddressWhenMultipleBuffers()
        {
            IByteBuffer buf1 = Unpooled.DirectBuffer(10);
            if (!buf1.HasMemoryAddress)
            {
                buf1.Release();
                return;
            }

            IByteBuffer buf2 = Unpooled.DirectBuffer(10);
            IByteBuffer buf = NewBuffer(buf1, buf2);
            Assert.False(buf.HasMemoryAddress);
            try
            {
                buf.GetPinnableMemoryAddress();
                Assert.False(true);
            }
            catch (NotSupportedException)
            {
                // expected
            }
            finally
            {
                buf.Release();
            }
        }

        [Fact]
        public void TestHasArrayWithSingleBuffer()
        {
            IByteBuffer buf1 = Unpooled.Buffer(10);
            IByteBuffer buf = NewBuffer(buf1);
            Assert.True(buf.HasArray);
            Assert.Equal(buf1.Array, buf.Array);
            buf.Release();
        }

        [Fact]
        public void TestHasArrayWhenEmpty()
        {
            IByteBuffer buf = NewBuffer(new IByteBuffer[0]);
            Assert.True(buf.HasArray);
            Assert.Equal(Unpooled.Empty.Array, buf.Array);
            buf.Release();
        }

        [Fact]
        public void TestHasNoArrayWhenMultipleBuffers()
        {
            IByteBuffer buf1 = Unpooled.Buffer(10);
            IByteBuffer buf2 = Unpooled.Buffer(10);
            IByteBuffer buf = NewBuffer(buf1, buf2);
            Assert.False(buf.HasArray);
            Assert.Throws<NotSupportedException>(() =>
            {
                try
                {
                    var ary = buf.Array;
                    Assert.False(true);
                }
                finally
                {
                    buf.Release();
                }
            });
        }
    }
}
