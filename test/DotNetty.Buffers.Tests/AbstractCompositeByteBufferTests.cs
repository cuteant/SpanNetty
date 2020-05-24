// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DotNetty.Common.Utilities;
    using Xunit;

    public abstract class AbstractCompositeByteBufferTests : AbstractByteBufferTests
    {
        private static readonly IByteBufferAllocator ALLOC = UnpooledByteBufferAllocator.Default;

        protected override IByteBuffer NewBuffer(int length, int maxCapacity)
        {
            this.AssumedMaxCapacity = maxCapacity == int.MaxValue;

            var buffers = new List<IByteBuffer>();
            for (int i = 0; i < length + 45; i += 45)
            {
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[1]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[2]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[3]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[4]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[5]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[6]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[7]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[8]));
                buffers.Add(Unpooled.Empty);
                buffers.Add(Unpooled.WrappedBuffer(new byte[9]));
                buffers.Add(Unpooled.Empty);
            }

            IByteBuffer buffer = Unpooled.WrappedBuffer(int.MaxValue, buffers.ToArray());

            // Truncate to the requested capacity.
            buffer.AdjustCapacity(length);

            Assert.Equal(length, buffer.Capacity);
            Assert.Equal(length, buffer.ReadableBytes);
            Assert.False(buffer.IsWritable());
            buffer.SetWriterIndex(0);
            return buffer;
        }

        protected override bool DiscardReadBytesDoesNotMoveWritableBytes() => false;

        [Fact]
        public void ComponentAtOffset()
        {
            var buf = (CompositeByteBuffer)Unpooled.WrappedBuffer(
                new byte[] { 1, 2, 3, 4, 5 },
                new byte[] { 4, 5, 6, 7, 8, 9, 26 });

            //Ensure that a random place will be fine
            Assert.Equal(5, buf.ComponentAtOffset(2).Capacity);

            //Loop through each byte

            byte index = 0;
            while (index < buf.Capacity)
            {
                IByteBuffer byteBuf = buf.ComponentAtOffset(index++);
                Assert.NotNull(byteBuf);
                Assert.True(byteBuf.Capacity > 0);
                Assert.True(byteBuf.GetByte(0) > 0);
                Assert.True(byteBuf.GetByte(byteBuf.ReadableBytes - 1) > 0);
            }

            buf.Release();
        }

        [Fact]
        public void ToComponentIndex()
        {
            var buf = (CompositeByteBuffer)Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5 },
                    new byte[] { 4, 5, 6, 7, 8, 9, 26 }, new byte[] { 10, 9, 8, 7, 6, 5, 33 });

            // spot checks
            Assert.Equal(0, buf.ToComponentIndex(4));
            Assert.Equal(1, buf.ToComponentIndex(5));
            Assert.Equal(2, buf.ToComponentIndex(15));

            //Loop through each byte

            byte index = 0;

            while (index < buf.Capacity)
            {
                int cindex = buf.ToComponentIndex(index++);
                Assert.True(cindex >= 0 && cindex < buf.NumComponents);
            }

            buf.Release();
        }

        [Fact]
        public void ToByteIndex()
        {
            var buf = (CompositeByteBuffer)Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5 },
                    new byte[] { 4, 5, 6, 7, 8, 9, 26 }, new byte[] { 10, 9, 8, 7, 6, 5, 33 });

            // spot checks
            Assert.Equal(0, buf.ToByteIndex(0));
            Assert.Equal(5, buf.ToByteIndex(1));
            Assert.Equal(12, buf.ToByteIndex(2));

            buf.Release();
        }

        [Fact]
        public void DiscardReadBytes3()
        {
            IByteBuffer a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            IByteBuffer b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 5),
                Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 5, 5));
            a.SkipBytes(6);
            a.MarkReaderIndex();
            b.SkipBytes(6);
            b.MarkReaderIndex();
            Assert.Equal(a.ReaderIndex, b.ReaderIndex);
            a.SetReaderIndex(a.ReaderIndex - 1);
            b.SetReaderIndex(b.ReaderIndex - 1);
            Assert.Equal(a.ReaderIndex, b.ReaderIndex);
            a.SetWriterIndex(a.WriterIndex - 1);
            a.MarkWriterIndex();
            b.SetWriterIndex(b.WriterIndex - 1);
            b.MarkWriterIndex();
            Assert.Equal(a.WriterIndex, b.WriterIndex);
            a.SetWriterIndex(a.WriterIndex + 1);
            b.SetWriterIndex(b.WriterIndex + 1);
            Assert.Equal(a.WriterIndex, b.WriterIndex);
            Assert.True(ByteBufferUtil.Equals(a, b));
            // now discard
            a.DiscardReadBytes();
            b.DiscardReadBytes();
            Assert.Equal(a.ReaderIndex, b.ReaderIndex);
            Assert.Equal(a.WriterIndex, b.WriterIndex);
            Assert.True(ByteBufferUtil.Equals(a, b));
            a.ResetReaderIndex();
            b.ResetReaderIndex();
            Assert.Equal(a.ReaderIndex, b.ReaderIndex);
            a.ResetWriterIndex();
            b.ResetWriterIndex();
            Assert.Equal(a.WriterIndex, b.WriterIndex);
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
        }

        [Fact]
        public void AutoConsolidation()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer(2);

            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1 }));
            Assert.Equal(1, buf.NumComponents);

            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 2, 3 }));
            Assert.Equal(2, buf.NumComponents);

            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 4, 5, 6 }));

            Assert.Equal(1, buf.NumComponents);
            Assert.True(buf.HasArray);
            Assert.NotNull(buf.Array);
            Assert.Equal(0, buf.ArrayOffset);

            buf.Release();
        }

        [Fact]
        public void CompositeToSingleBuffer()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer(3);

            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            Assert.Equal(1, buf.NumComponents);

            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 4 }));
            Assert.Equal(2, buf.NumComponents);

            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 5, 6 }));
            Assert.Equal(3, buf.NumComponents);

            // NOTE: hard-coding 6 here, since it seems like AddComponent doesn't bump the writer index.
            // I'm unsure as to whether or not this is correct behavior
            ArraySegment<byte> nioBuffer = buf.GetIoBuffer(0, 6);
            Assert.Equal(6, nioBuffer.Count);
            Assert.True(nioBuffer.Array.SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6 }));

            buf.Release();
        }

        [Fact]
        public void FullConsolidation()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer(int.MaxValue);
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1 }));
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 2, 3 }));
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 4, 5, 6 }));
            buf.Consolidate();

            Assert.Equal(1, buf.NumComponents);
            Assert.True(buf.HasArray);
            Assert.NotNull(buf.Array);
            Assert.Equal(0, buf.ArrayOffset);

            buf.Release();
        }

        [Fact]
        public void RangedConsolidation()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer(int.MaxValue);
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1 }));
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 2, 3 }));
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 4, 5, 6 }));
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 7, 8, 9, 10 }));
            buf.Consolidate(1, 2);

            Assert.Equal(3, buf.NumComponents);
            Assert.Equal(Unpooled.WrappedBuffer(new byte[] { 1 }), buf[0]);
            Assert.Equal(Unpooled.WrappedBuffer(new byte[] { 2, 3, 4, 5, 6 }), buf[1]);
            Assert.Equal(Unpooled.WrappedBuffer(new byte[] { 7, 8, 9, 10 }), buf[2]);

            buf.Release();
        }

        [Fact]
        public void CompositeWrappedBuffer()
        {
            IByteBuffer header = Unpooled.Buffer(12);
            IByteBuffer payload = Unpooled.Buffer(512);

            header.WriteBytes(new byte[12]);
            payload.WriteBytes(new byte[512]);

            IByteBuffer buffer = Unpooled.WrappedBuffer(header, payload);

            Assert.Equal(12, header.ReadableBytes);
            Assert.Equal(512, payload.ReadableBytes);

            Assert.Equal(12 + 512, buffer.ReadableBytes);
            Assert.Equal(2, buffer.IoBufferCount);

            buffer.Release();
        }

        [Fact]
        public void SeveralBuffersEquals()
        {
            // XXX Same tests with several buffers in wrappedCheckedBuffer
            // Different length.
            IByteBuffer a = Unpooled.WrappedBuffer(new byte[] { 1 });
            IByteBuffer b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1 }),
                Unpooled.WrappedBuffer(new byte[] { 2 }));
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Same content, same firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1 }),
                Unpooled.WrappedBuffer(new byte[] { 2 }),
                Unpooled.WrappedBuffer(new byte[] { 3 }));
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Same content, different firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4 }, 1, 2),
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4 }, 3, 1));
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Different content, same firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1, 2 }),
                Unpooled.WrappedBuffer(new byte[] { 4 }));
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Different content, different firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 4, 5 }, 1, 2),
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 4, 5 }, 3, 1));
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Same content, same firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }),
                Unpooled.WrappedBuffer(new byte[] { 4, 5, 6 }),
                Unpooled.WrappedBuffer(new byte[] { 7, 8, 9, 10 }));
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Same content, different firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, 1, 5),
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, 6, 5));
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Different content, same firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 6 }),
                Unpooled.WrappedBuffer(new byte[] { 7, 8, 5, 9, 10 }));
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();

            // Different content, different firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 6, 7, 8, 5, 9, 10, 11 }, 1, 5),
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 6, 7, 8, 5, 9, 10, 11 }, 6, 5));
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
        }

        [Fact]
        public void WrappedBuffer()
        {
            var bytes = new byte[16];
            IByteBuffer a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(bytes));
            Assert.Equal(16, a.Capacity);
            a.Release();

            a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            IByteBuffer b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[][] { new byte[] { 1, 2, 3 } }));
            Assert.Equal(a, b);

            a.Release();
            b.Release();

            a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(
                new byte[] { 1 },
                new byte[] { 2 },
                new byte[] { 3 }));
            Assert.Equal(a, b);

            a.Release();
            b.Release();

            a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            b = Unpooled.WrappedBuffer(new[] {
                Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 })
            });
            Assert.Equal(a, b);

            a.Release();
            b.Release();

            a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1 }),
                Unpooled.WrappedBuffer(new byte[] { 2 }),
                Unpooled.WrappedBuffer(new byte[] { 3 }));
            Assert.Equal(a, b);

            a.Release();
            b.Release();

            a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new[] {
                Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 })
            }));
            Assert.Equal(a, b);

            a.Release();
            b.Release();

            a = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }));
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 1 }),
                Unpooled.WrappedBuffer(new byte[] { 2 }),
                Unpooled.WrappedBuffer(new byte[] { 3 })));
            Assert.Equal(a, b);

            a.Release();
            b.Release();
        }

        [Fact]
        public void WrittenBuffersEquals()
        {
            //XXX Same tests than testEquals with written AggregateChannelBuffers
            // Different length.
            IByteBuffer a = Unpooled.WrappedBuffer(new byte[] { 1 });
            IByteBuffer b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1 }, new byte[1]));
            IByteBuffer c = Unpooled.WrappedBuffer(new byte[] { 2 });

            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 1);
            b.WriteBytes(c);
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Same content, same firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1 }, new byte[2]));
            c = Unpooled.WrappedBuffer(new byte[] { 2 });

            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 2);
            b.WriteBytes(c);
            c.Release();
            c = Unpooled.WrappedBuffer(new byte[] { 3 });

            b.WriteBytes(c);
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Same content, different firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4 }, 1, 3));
            c = Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4 }, 3, 1);
            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 1);
            b.WriteBytes(c);
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Different content, same firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2 }, new byte[1]));
            c = Unpooled.WrappedBuffer(new byte[] { 4 });
            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 1);
            b.WriteBytes(c);
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Different content, different firstIndex, short length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 4, 5 }, 1, 3));
            c = Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 4, 5 }, 3, 1);
            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 1);
            b.WriteBytes(c);
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Same content, same firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 }, new byte[7]));
            c = Unpooled.WrappedBuffer(new byte[] { 4, 5, 6 });

            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 7);
            b.WriteBytes(c);
            c.Release();
            c = Unpooled.WrappedBuffer(new byte[] { 7, 8, 9, 10 });
            b.WriteBytes(c);
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Same content, different firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, 1, 10));
            c = Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, 6, 5);
            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 5);
            b.WriteBytes(c);
            Assert.True(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Different content, same firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 6 }, new byte[5]));
            c = Unpooled.WrappedBuffer(new byte[] { 7, 8, 5, 9, 10 });
            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 5);
            b.WriteBytes(c);
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();

            // Different content, different firstIndex, long length.
            a = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            b = Unpooled.WrappedBuffer(
                Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 6, 7, 8, 5, 9, 10, 11 }, 1, 10));
            c = Unpooled.WrappedBuffer(new byte[] { 0, 1, 2, 3, 4, 6, 7, 8, 5, 9, 10, 11 }, 6, 5);
            // to enable writeBytes
            b.SetWriterIndex(b.WriterIndex - 5);
            b.WriteBytes(c);
            Assert.False(ByteBufferUtil.Equals(a, b));

            a.Release();
            b.Release();
            c.Release();
        }

        [Fact]
        public void EmptyBuffer()
        {
            IByteBuffer b = Unpooled.WrappedBuffer(new byte[] { 1, 2 }, new byte[] { 3, 4 });
            b.ReadBytes(new byte[4]);
            b.ReadBytes(ArrayExtensions.ZeroBytes);
            b.Release();
        }

        [Fact]
        public void ReadWithEmptyCompositeBuffer()
        {
            IByteBuffer buf = Unpooled.CompositeBuffer();
            int n = 65;
            for (int i = 0; i < n; i++)
            {
                buf.WriteByte(1);
                Assert.Equal(1, buf.ReadByte());
            }
            buf.Release();
        }

        [Fact]
        public void ComponentMustBeDuplicate()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            buf.AddComponent(Unpooled.Buffer(4, 6).SetIndex(1, 3));
            Assert.IsAssignableFrom<AbstractDerivedByteBuffer>(buf[0]);
            Assert.Equal(4, buf[0].Capacity);
            Assert.Equal(6, buf[0].MaxCapacity);
            Assert.Equal(2, buf[0].ReadableBytes);
            buf.Release();
        }

        [Fact]
        public void ReferenceCounts1()
        {
            IByteBuffer c1 = Unpooled.Buffer().WriteByte(1);
            var c2 = (IByteBuffer)Unpooled.Buffer().WriteByte(2).Retain();
            var c3 = (IByteBuffer)Unpooled.Buffer().WriteByte(3).Retain(2);

            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            Assert.Equal(1, buf.ReferenceCount);
            buf.AddComponents(c1, c2, c3);

            Assert.Equal(1, buf.ReferenceCount);

            // Ensure that c[123]'s refCount did not change.
            Assert.Equal(1, c1.ReferenceCount);
            Assert.Equal(2, c2.ReferenceCount);
            Assert.Equal(3, c3.ReferenceCount);

            Assert.Equal(1, buf[0].ReferenceCount);
            Assert.Equal(2, buf[1].ReferenceCount);
            Assert.Equal(3, buf[2].ReferenceCount);

            c3.Release(2);
            c2.Release();
            buf.Release();
        }

        [Fact]
        public void ReferenceCounts2()
        {
            IByteBuffer c1 = Unpooled.Buffer().WriteByte(1);
            var c2 = (IByteBuffer)Unpooled.Buffer().WriteByte(2).Retain();
            var c3 = (IByteBuffer)Unpooled.Buffer().WriteByte(3).Retain(2);

            CompositeByteBuffer bufA = Unpooled.CompositeBuffer();
            bufA.AddComponents(c1, c2, c3).SetWriterIndex(3);

            CompositeByteBuffer bufB = Unpooled.CompositeBuffer();
            bufB.AddComponents((IByteBuffer)bufA);

            // Ensure that bufA.ReferenceCount did not change.
            Assert.Equal(1, bufA.ReferenceCount);

            // Ensure that c[123]'s refCnt did not change.
            Assert.Equal(1, c1.ReferenceCount);
            Assert.Equal(2, c2.ReferenceCount);
            Assert.Equal(3, c3.ReferenceCount);

            // This should decrease bufA.ReferenceCount.
            bufB.Release();
            Assert.Equal(0, bufB.ReferenceCount);

            // Ensure bufA.ReferenceCount changed.
            Assert.Equal(0, bufA.ReferenceCount);

            // Ensure that c[123]'s refCnt also changed due to the deallocation of bufA.
            Assert.Equal(0, c1.ReferenceCount);
            Assert.Equal(1, c2.ReferenceCount);
            Assert.Equal(2, c3.ReferenceCount);

            c3.Release(2);
            c2.Release();
        }

        [Fact]
        public void ReferenceCounts3()
        {
            IByteBuffer c1 = Unpooled.Buffer().WriteByte(1);
            var c2 = (IByteBuffer)Unpooled.Buffer().WriteByte(2).Retain();
            var c3 = (IByteBuffer)Unpooled.Buffer().WriteByte(3).Retain(2);

            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            Assert.Equal(1, buf.ReferenceCount);

            var components = new List<IByteBuffer>
            {
                c1,
                c2,
                c3
            };
            buf.AddComponents(components);

            // Ensure that c[123]'s refCount did not change.
            Assert.Equal(1, c1.ReferenceCount);
            Assert.Equal(2, c2.ReferenceCount);
            Assert.Equal(3, c3.ReferenceCount);

            Assert.Equal(1, buf[0].ReferenceCount);
            Assert.Equal(2, buf[1].ReferenceCount);
            Assert.Equal(3, buf[2].ReferenceCount);

            c3.Release(2);
            c2.Release();
            buf.Release();
        }

        [Fact]
        public void NestedLayout()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            buf.AddComponent(
                Unpooled.CompositeBuffer()
                    .AddComponent(Unpooled.WrappedBuffer(new byte[] { 1, 2 }))
                    .AddComponent(Unpooled.WrappedBuffer(new byte[] { 3, 4 })).Slice(1, 2));

            ArraySegment<byte>[] nioBuffers = buf.GetIoBuffers(0, 2);
            Assert.Equal(2, nioBuffers.Length);
            Assert.Equal((byte)2, nioBuffers[0].Array[nioBuffers[0].Offset]);
            Assert.Equal((byte)3, nioBuffers[1].Array[nioBuffers[1].Offset]);
            buf.Release();
        }

        [Fact]
        public void RemoveLastComponent()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1, 2 }));
            Assert.Equal(1, buf.NumComponents);
            buf.RemoveComponent(0);
            Assert.Equal(0, buf.NumComponents);
            buf.Release();
        }

        [Fact]
        public void CopyEmpty()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            Assert.Equal(0, buf.NumComponents);

            IByteBuffer copy = buf.Copy();
            Assert.Equal(0, copy.ReadableBytes);

            buf.Release();
            copy.Release();
        }

        [Fact]
        public void DuplicateEmpty()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            Assert.Equal(0, buf.NumComponents);
            Assert.Equal(0, buf.Duplicate().ReadableBytes);

            buf.Release();
        }

        [Fact]
        public void RemoveLastComponentWithOthersLeft()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1, 2 }));
            buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1, 2 }));
            Assert.Equal(2, buf.NumComponents);
            buf.RemoveComponent(1);
            Assert.Equal(1, buf.NumComponents);
            buf.Release();
        }

        [Fact]
        public void RemoveComponents()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            for (int i = 0; i < 10; i++)
            {
                buf.AddComponent(Unpooled.WrappedBuffer(new byte[] { 1, 2 }));
            }
            Assert.Equal(10, buf.NumComponents);
            Assert.Equal(20, buf.Capacity);
            buf.RemoveComponents(4, 3);
            Assert.Equal(7, buf.NumComponents);
            Assert.Equal(14, buf.Capacity);
            buf.Release();
        }

        //@Test
        //public void testGatheringWritesHeap() throws Exception {
        //    testGatheringWrites(buffer().order(order), buffer().order(order));
        //}

        //@Test
        //public void testGatheringWritesDirect() throws Exception {
        //    testGatheringWrites(directBuffer().order(order), directBuffer().order(order));
        //}

        //@Test
        //public void testGatheringWritesMixes() throws Exception {
        //    testGatheringWrites(buffer().order(order), directBuffer().order(order));
        //}

        //@Test
        //public void testGatheringWritesHeapPooled() throws Exception {
        //    testGatheringWrites(PooledByteBufAllocator.DEFAULT.heapBuffer().order(order),
        //            PooledByteBufAllocator.DEFAULT.heapBuffer().order(order));
        //}

        //@Test
        //public void testGatheringWritesDirectPooled() throws Exception {
        //    testGatheringWrites(PooledByteBufAllocator.DEFAULT.directBuffer().order(order),
        //            PooledByteBufAllocator.DEFAULT.directBuffer().order(order));
        //}

        //@Test
        //public void testGatheringWritesMixesPooled() throws Exception {
        //    testGatheringWrites(PooledByteBufAllocator.DEFAULT.heapBuffer().order(order),
        //            PooledByteBufAllocator.DEFAULT.directBuffer().order(order));
        //}

        //private static void testGatheringWrites(ByteBuf buf1, ByteBuf buf2) throws Exception {
        //    CompositeByteBuf buf = compositeBuffer();
        //    buf.addComponent(buf1.writeBytes(new byte[]{1, 2}));
        //    buf.addComponent(buf2.writeBytes(new byte[]{1, 2}));
        //    buf.writerIndex(3);
        //    buf.readerIndex(1);

        //    TestGatheringByteChannel channel = new TestGatheringByteChannel();

        //    buf.readBytes(channel, 2);

        //    byte[] data = new byte[2];
        //    buf.getBytes(1, data);
        //    assertArrayEquals(data, channel.writtenBytes());

        //    buf.release();
        //}

        //@Test
        //public void testGatheringWritesPartialHeap() throws Exception {
        //    testGatheringWritesPartial(buffer().order(order), buffer().order(order), false);
        //}

        //@Test
        //public void testGatheringWritesPartialDirect() throws Exception {
        //    testGatheringWritesPartial(directBuffer().order(order), directBuffer().order(order), false);
        //}

        //@Test
        //public void testGatheringWritesPartialMixes() throws Exception {
        //    testGatheringWritesPartial(buffer().order(order), directBuffer().order(order), false);
        //}

        //@Test
        //public void testGatheringWritesPartialHeapSlice() throws Exception {
        //    testGatheringWritesPartial(buffer().order(order), buffer().order(order), true);
        //}

        //@Test
        //public void testGatheringWritesPartialDirectSlice() throws Exception {
        //    testGatheringWritesPartial(directBuffer().order(order), directBuffer().order(order), true);
        //}

        //@Test
        //public void testGatheringWritesPartialMixesSlice() throws Exception {
        //    testGatheringWritesPartial(buffer().order(order), directBuffer().order(order), true);
        //}

        //@Test
        //public void testGatheringWritesPartialHeapPooled() throws Exception {
        //    testGatheringWritesPartial(PooledByteBufAllocator.DEFAULT.heapBuffer().order(order),
        //            PooledByteBufAllocator.DEFAULT.heapBuffer().order(order), false);
        //}

        //@Test
        //public void testGatheringWritesPartialDirectPooled() throws Exception {
        //    testGatheringWritesPartial(PooledByteBufAllocator.DEFAULT.directBuffer().order(order),
        //            PooledByteBufAllocator.DEFAULT.directBuffer().order(order), false);
        //}

        //@Test
        //public void testGatheringWritesPartialMixesPooled() throws Exception {
        //    testGatheringWritesPartial(PooledByteBufAllocator.DEFAULT.heapBuffer().order(order),
        //            PooledByteBufAllocator.DEFAULT.directBuffer().order(order), false);
        //}

        //@Test
        //public void testGatheringWritesPartialHeapPooledSliced() throws Exception {
        //    testGatheringWritesPartial(PooledByteBufAllocator.DEFAULT.heapBuffer().order(order),
        //            PooledByteBufAllocator.DEFAULT.heapBuffer().order(order), true);
        //}

        //@Test
        //public void testGatheringWritesPartialDirectPooledSliced() throws Exception {
        //    testGatheringWritesPartial(PooledByteBufAllocator.DEFAULT.directBuffer().order(order),
        //            PooledByteBufAllocator.DEFAULT.directBuffer().order(order), true);
        //}

        //@Test
        //public void testGatheringWritesPartialMixesPooledSliced() throws Exception {
        //    testGatheringWritesPartial(PooledByteBufAllocator.DEFAULT.heapBuffer().order(order),
        //            PooledByteBufAllocator.DEFAULT.directBuffer().order(order), true);
        //}

        //private static void testGatheringWritesPartial(ByteBuf buf1, ByteBuf buf2, boolean slice) throws Exception {
        //    CompositeByteBuf buf = compositeBuffer();
        //    buf1.writeBytes(new byte[]{1, 2, 3, 4});
        //    buf2.writeBytes(new byte[]{1, 2, 3, 4});
        //    if (slice) {
        //        buf1 = buf1.readerIndex(1).slice();
        //        buf2 = buf2.writerIndex(3).slice();
        //        buf.addComponent(buf1);
        //        buf.addComponent(buf2);
        //        buf.writerIndex(6);
        //    } else {
        //        buf.addComponent(buf1);
        //        buf.addComponent(buf2);
        //        buf.writerIndex(7);
        //        buf.readerIndex(1);
        //    }

        //    TestGatheringByteChannel channel = new TestGatheringByteChannel(1);

        //    while (buf.isReadable()) {
        //        buf.readBytes(channel, buf.readableBytes());
        //    }

        //    byte[] data = new byte[6];

        //    if (slice) {
        //        buf.getBytes(0, data);
        //    } else {
        //        buf.getBytes(1, data);
        //    }
        //    assertArrayEquals(data, channel.writtenBytes());

        //    buf.release();
        //}

        //@Test
        //public void testGatheringWritesSingleHeap() throws Exception {
        //    testGatheringWritesSingleBuf(buffer().order(order));
        //}

        //@Test
        //public void testGatheringWritesSingleDirect() throws Exception {
        //    testGatheringWritesSingleBuf(directBuffer().order(order));
        //}

        //private static void testGatheringWritesSingleBuf(ByteBuf buf1) throws Exception {
        //    CompositeByteBuf buf = compositeBuffer();
        //    buf.addComponent(buf1.writeBytes(new byte[]{1, 2, 3, 4}));
        //    buf.writerIndex(3);
        //    buf.readerIndex(1);

        //    TestGatheringByteChannel channel = new TestGatheringByteChannel();
        //    buf.readBytes(channel, 2);

        //    byte[] data = new byte[2];
        //    buf.getBytes(1, data);
        //    assertArrayEquals(data, channel.writtenBytes());

        //    buf.release();
        //}

        //public void InternalNioBuffer()
        //{
        //    CompositeByteBuffer buf = Unpooled.CompositeBuffer();
        //    Assert.Empty(buf.GetIoBuffer(0, 0));

        //    // If non-derived buffer is added, its internal buffer should be returned
        //    var concreteBuffer = Unpooled.DirectBuffer().WriteByte(1);
        //    buf.AddComponent(concreteBuffer);
        //    Assert.Same(concreteBuffer.GetIoBuffer(0, 1).Array, buf.GetIoBuffer(0, 1).Array);
        //    buf.Release();

        //    // In derived cases, the original internal buffer must not be used
        //    buf = Unpooled.CompositeBuffer();
        //    concreteBuffer = Unpooled.DirectBuffer().WriteByte(1);
        //    buf.AddComponent(concreteBuffer.Slice());
        //    Assert.NotSame(concreteBuffer.GetIoBuffer(0, 1).Array, buf.GetIoBuffer(0, 1).Array);
        //    buf.Release();

        //    buf = Unpooled.CompositeBuffer();
        //    concreteBuffer = Unpooled.DirectBuffer().WriteByte(1);
        //    buf.AddComponent(concreteBuffer.Duplicate());
        //    Assert.NotSame(concreteBuffer.GetIoBuffer(0, 1).Array, buf.GetIoBuffer(0, 1).Array);
        //    buf.Release();
        //}

        [Fact]
        public void DirectMultipleBufs()
        {
            CompositeByteBuffer buf = Unpooled.CompositeBuffer();
            Assert.False(buf.IsDirect);

            buf.AddComponent(Unpooled.DirectBuffer().WriteByte(1));

            Assert.True(buf.IsDirect);
            buf.AddComponent(Unpooled.DirectBuffer().WriteByte(1));
            Assert.True(buf.IsDirect);

            buf.AddComponent(Unpooled.Buffer().WriteByte(1));
            Assert.False(buf.IsDirect);

            buf.Release();
        }

        [Fact]
        public void DiscardSomeReadBytes()
        {
            CompositeByteBuffer cbuf = Unpooled.CompositeBuffer();
            int len = 8 * 4;
            for (int i = 0; i < len; i += 4)
            {
                IByteBuffer buf = Unpooled.Buffer().WriteInt(i);
                cbuf.AdjustCapacity(cbuf.WriterIndex);
                cbuf.AddComponent(buf).SetWriterIndex(i + 4);
            }
            cbuf.WriteByte(1);

            var me = new byte[len];
            cbuf.ReadBytes(me);
            cbuf.ReadByte();

            cbuf.DiscardSomeReadBytes();
            cbuf.Release();
        }

        [Fact]
        public void AddEmptyBufferRelease()
        {
            CompositeByteBuffer cbuf = Unpooled.CompositeBuffer();
            IByteBuffer buf = Unpooled.Buffer();
            Assert.Equal(1, buf.ReferenceCount);
            cbuf.AddComponent(buf);
            Assert.Equal(1, buf.ReferenceCount);

            cbuf.Release();
            Assert.Equal(0, buf.ReferenceCount);
        }

        [Fact]
        public void AddEmptyBuffersRelease()
        {
            CompositeByteBuffer cbuf = Unpooled.CompositeBuffer();
            IByteBuffer buf = Unpooled.Buffer();
            IByteBuffer buf2 = Unpooled.Buffer().WriteInt(1);
            IByteBuffer buf3 = Unpooled.Buffer();

            Assert.Equal(1, buf.ReferenceCount);
            Assert.Equal(1, buf2.ReferenceCount);
            Assert.Equal(1, buf3.ReferenceCount);

            cbuf.AddComponents(buf, buf2, buf3);
            Assert.Equal(1, buf.ReferenceCount);
            Assert.Equal(1, buf2.ReferenceCount);
            Assert.Equal(1, buf3.ReferenceCount);

            cbuf.Release();
            Assert.Equal(0, buf.ReferenceCount);
            Assert.Equal(0, buf2.ReferenceCount);
            Assert.Equal(0, buf3.ReferenceCount);
        }

        [Fact]
        public void AddEmptyBufferInMiddle()
        {
            CompositeByteBuffer cbuf = Unpooled.CompositeBuffer();
            IByteBuffer buf1 = Unpooled.Buffer().WriteByte(1);
            cbuf.AddComponent(true, buf1);
            cbuf.AddComponent(true, Unpooled.Empty);
            IByteBuffer buf3 = Unpooled.Buffer().WriteByte(2);
            cbuf.AddComponent(true, buf3);

            Assert.Equal(2, cbuf.ReadableBytes);
            Assert.Equal((byte)1, cbuf.ReadByte());
            Assert.Equal((byte)2, cbuf.ReadByte());

            Assert.Same(Unpooled.Empty, cbuf.InternalComponent(1));
            Assert.NotSame(Unpooled.Empty, cbuf.InternalComponentAtOffset(1));
            cbuf.Release();
        }

        [Fact]
        public void InsertEmptyBufferInMiddle()
        {
            CompositeByteBuffer cbuf = Unpooled.CompositeBuffer();
            IByteBuffer buf1 = Unpooled.Buffer().WriteByte(1);
            cbuf.AddComponent(true, buf1);
            IByteBuffer buf2 = Unpooled.Buffer().WriteByte(2);
            cbuf.AddComponent(true, buf2);

            // insert empty one between the first two
            cbuf.AddComponent(true, 1, Unpooled.Empty);

            Assert.Equal(2, cbuf.ReadableBytes);
            Assert.Equal((byte)1, cbuf.ReadByte());
            Assert.Equal((byte)2, cbuf.ReadByte());

            Assert.Equal(2, cbuf.Capacity);
            Assert.Equal(3, cbuf.NumComponents);

            byte[] dest = new byte[2];
            // should skip over the empty one, not throw a java.lang.Error :)
            cbuf.GetBytes(0, dest);

            Assert.Equal(new byte[] { 1, 2 }, dest);

            cbuf.Release();
        }

        [Fact]
        public void AddFlattenedComponentsTest()
        {
            var b1 = Unpooled.WrappedBuffer(new byte[] { 1, 2, 3 });
            CompositeByteBuffer newComposite = Unpooled.CompositeBuffer()
                    .AddComponent(true, b1)
                    .AddFlattenedComponents(true, (IByteBuffer)b1.Retain())
                    .AddFlattenedComponents(true, Unpooled.Empty);

            Assert.Equal(2, newComposite.NumComponents);
            Assert.Equal(6, newComposite.Capacity);
            Assert.Equal(6, newComposite.WriterIndex);

            // It is important to use a pooled allocator here to ensure
            // the slices returned by readRetainedSlice are of type
            // PooledSlicedByteBuf, which maintains an independent refcount
            // (so that we can be sure to cover this case)
            var buffer = PooledByteBufferAllocator.Default.Buffer()
                  .WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

            // use mixture of slice and retained slice
            var s1 = buffer.ReadRetainedSlice(2);
            var s2 = s1.RetainedSlice(0, 2);
            var s3 = (IByteBuffer)buffer.Slice(0, 2).Retain();
            var s4 = s2.RetainedSlice(0, 2);
            buffer.Release();

            var compositeToAdd = Unpooled.CompositeBuffer()
                .AddComponent(s1)
                .AddComponent(Unpooled.Empty)
                .AddComponents(s2, s3, s4);
            // set readable range to be from middle of first component
            // to middle of penultimate component
            compositeToAdd.SetIndex(1, 5);

            Assert.Equal(1, compositeToAdd.ReferenceCount);
            Assert.Equal(1, s4.ReferenceCount);

            var compositeCopy = compositeToAdd.Copy();

            newComposite.AddFlattenedComponents(true, compositeToAdd);

            // verify that added range matches
            ByteBufferUtil.Equals(compositeCopy, 0,
                    newComposite, 6, compositeCopy.ReadableBytes);

            // should not include empty component or last component
            // (latter outside of the readable range)
            Assert.Equal(5, newComposite.NumComponents);
            Assert.Equal(10, newComposite.Capacity);
            Assert.Equal(10, newComposite.WriterIndex);

            Assert.Equal(0, compositeToAdd.ReferenceCount);
            // s4 wasn't in added range so should have been jettisoned
            Assert.Equal(0, s4.ReferenceCount);
            Assert.Equal(1, newComposite.ReferenceCount);

            // releasing composite should release the remaining components
            newComposite.Release();
            Assert.Equal(0, newComposite.ReferenceCount);
            Assert.Equal(0, s1.ReferenceCount);
            Assert.Equal(0, s2.ReferenceCount);
            Assert.Equal(0, s3.ReferenceCount);
            Assert.Equal(0, b1.ReferenceCount);
        }

        //    @Test
        //public void testIterator()
        //    {
        //        CompositeByteBuf cbuf = compositeBuffer();
        //        cbuf.addComponent(EMPTY_BUFFER);
        //        cbuf.addComponent(EMPTY_BUFFER);

        //        Iterator<ByteBuf> it = cbuf.iterator();
        //        assertTrue(it.hasNext());
        //        assertSame(EMPTY_BUFFER, it.next());
        //        assertTrue(it.hasNext());
        //        assertSame(EMPTY_BUFFER, it.next());
        //        assertFalse(it.hasNext());

        //        try
        //        {
        //            it.next();
        //            fail();
        //        }
        //        catch (NoSuchElementException e)
        //        {
        //            //Expected
        //        }
        //        cbuf.release();
        //    }

        //    @Test
        //public void testEmptyIterator()
        //    {
        //        CompositeByteBuf cbuf = compositeBuffer();

        //        Iterator<ByteBuf> it = cbuf.iterator();
        //        assertFalse(it.hasNext());

        //        try
        //        {
        //            it.next();
        //            fail();
        //        }
        //        catch (NoSuchElementException e)
        //        {
        //            //Expected
        //        }
        //        cbuf.release();
        //    }

        //    @Test(expected = ConcurrentModificationException.class)
        //public void testIteratorConcurrentModificationAdd()
        //    {
        //        CompositeByteBuf cbuf = compositeBuffer();
        //        cbuf.addComponent(EMPTY_BUFFER);

        //        Iterator<ByteBuf> it = cbuf.iterator();
        //        cbuf.addComponent(EMPTY_BUFFER);

        //        assertTrue(it.hasNext());
        //        try
        //        {
        //            it.next();
        //        }
        //        finally
        //        {
        //            cbuf.release();
        //        }
        //    }

        //    @Test(expected = ConcurrentModificationException.class)
        //public void testIteratorConcurrentModificationRemove()
        //    {
        //        CompositeByteBuf cbuf = compositeBuffer();
        //        cbuf.addComponent(EMPTY_BUFFER);

        //        Iterator<ByteBuf> it = cbuf.iterator();
        //        cbuf.removeComponent(0);

        //        assertTrue(it.hasNext());
        //        try
        //        {
        //            it.next();
        //        }
        //        finally
        //        {
        //            cbuf.release();
        //        }
        //    }

        [Fact]
        public void ReleasesItsComponents()
        {
            IByteBuffer buffer = PooledByteBufferAllocator.Default.Buffer(); // 1

            buffer.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

            var s1 = (IByteBuffer)buffer.ReadSlice(2).Retain(); // 2
            var s2 = (IByteBuffer)s1.ReadSlice(2).Retain(); // 3
            var s3 = (IByteBuffer)s2.ReadSlice(2).Retain(); // 4
            var s4 = (IByteBuffer)s3.ReadSlice(2).Retain(); // 5

            IByteBuffer composite = PooledByteBufferAllocator.Default.CompositeBuffer()
                .AddComponent(s1)
                .AddComponents(s2, s3, s4);

            Assert.Equal(1, composite.ReferenceCount);
            Assert.Equal(5, buffer.ReferenceCount);

            // releasing composite should release the 4 components
            ReferenceCountUtil.Release(composite);
            Assert.Equal(0, composite.ReferenceCount);
            Assert.Equal(1, buffer.ReferenceCount);

            // last remaining ref to buffer
            ReferenceCountUtil.Release(buffer);
            Assert.Equal(0, buffer.ReferenceCount);
        }

        [Fact]
        public void ReleasesItsComponents2()
        {
            // It is important to use a pooled allocator here to ensure
            // the slices returned by readRetainedSlice are of type
            // PooledSlicedByteBuf, which maintains an independent refcount
            // (so that we can be sure to cover this case)
            IByteBuffer buffer = PooledByteBufferAllocator.Default.Buffer(); // 1

            buffer.WriteBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

            // use readRetainedSlice this time - produces different kind of slices
            IByteBuffer s1 = buffer.ReadRetainedSlice(2); // 2
            IByteBuffer s2 = s1.ReadRetainedSlice(2); // 3
            IByteBuffer s3 = s2.ReadRetainedSlice(2); // 4
            IByteBuffer s4 = s3.ReadRetainedSlice(2); // 5

            IByteBuffer composite = Unpooled.CompositeBuffer()
                .AddComponent(s1)
                .AddComponents(s2, s3, s4);
            //.order(ByteOrder.LITTLE_ENDIAN);

            Assert.Equal(1, composite.ReferenceCount);
            Assert.Equal(2, buffer.ReferenceCount);

            // releasing composite should release the 4 components
            composite.Release();
            Assert.Equal(0, composite.ReferenceCount);
            Assert.Equal(1, buffer.ReferenceCount);

            // last remaining ref to buffer
            buffer.Release();
            Assert.Equal(0, buffer.ReferenceCount);
        }

        [Fact]
        public void ReleasesOnShrink()
        {
            IByteBuffer b1 = Unpooled.Buffer(2).WriteShort(1);
            IByteBuffer b2 = Unpooled.Buffer(2).WriteShort(2);

            // composite takes ownership of s1 and s2
            IByteBuffer composite = Unpooled.CompositeBuffer()
                .AddComponents(b1, b2);

            Assert.Equal(4, composite.Capacity);

            // reduce capacity down to two, will drop the second component
            composite.AdjustCapacity(2);
            Assert.Equal(2, composite.Capacity);

            // releasing composite should release the components
            composite.Release();
            Assert.Equal(0, composite.ReferenceCount);
            Assert.Equal(0, b1.ReferenceCount);
            Assert.Equal(0, b2.ReferenceCount);
        }

        [Fact]
        public void ReleasesOnShrink2()
        {
            // It is important to use a pooled allocator here to ensure
            // the slices returned by readRetainedSlice are of type
            // PooledSlicedByteBuf, which maintains an independent refcount
            // (so that we can be sure to cover this case)
            IByteBuffer buffer = PooledByteBufferAllocator.Default.Buffer();

            buffer.WriteShort(1).WriteShort(2);

            IByteBuffer b1 = buffer.ReadRetainedSlice(2);
            IByteBuffer b2 = b1.RetainedSlice(b1.ReaderIndex, 2);

            // composite takes ownership of b1 and b2
            IByteBuffer composite = Unpooled.CompositeBuffer()
                .AddComponents(b1, b2);

            Assert.Equal(4, composite.Capacity);

            // reduce capacity down to two, will drop the second component
            composite.AdjustCapacity(2);
            Assert.Equal(2, composite.Capacity);

            // releasing composite should release the components
            composite.Release();
            Assert.Equal(0, composite.ReferenceCount);
            Assert.Equal(0, b1.ReferenceCount);
            Assert.Equal(0, b2.ReferenceCount);

            // release last remaining ref to buffer
            buffer.Release();
            Assert.Equal(0, buffer.ReferenceCount);
        }

        [Fact]
        public void AllocatorIsSameWhenCopy() => this.AllocatorIsSameWhenCopy0(false);

        [Fact]
        public void AllocatorIsSameWhenCopyUsingIndexAndLength() => this.AllocatorIsSameWhenCopy0(true);

        void AllocatorIsSameWhenCopy0(bool withIndexAndLength)
        {
            IByteBuffer buffer = this.NewBuffer(8);
            buffer.WriteZero(4);
            IByteBuffer copy = withIndexAndLength ? buffer.Copy(0, 4) : buffer.Copy();
            Assert.Equal(buffer, copy);
            Assert.Same(buffer.Allocator, copy.Allocator);
            buffer.Release();
            copy.Release();
        }

        [Fact]
        public void TestDecomposeMultiple()
        {
            TestDecompose(150, 500, 3);
        }

        [Fact]
        public void TestDecomposeOne()
        {
            TestDecompose(310, 50, 1);
        }

        [Fact]
        public void TestDecomposeNone()
        {
            TestDecompose(310, 0, 0);
        }

        static void TestDecompose(int offset, int length, int expectedListSize)
        {
            byte[] bytes = new byte[1024];
            var seed = Environment.TickCount;
            var random = new Random(seed);
            random.NextBytes(bytes);
            IByteBuffer buf = Unpooled.WrappedBuffer(bytes);

            var composite = Unpooled.CompositeBuffer();
            composite.AddComponents(true,
                                    buf.RetainedSlice(100, 200),
                                    buf.RetainedSlice(300, 400),
                                    buf.RetainedSlice(700, 100));

            var slice = composite.Slice(offset, length);
            var bufferList = composite.Decompose(offset, length);
            Assert.Equal(expectedListSize, bufferList.Count);
            var wrapped = Unpooled.WrappedBuffer(bufferList.ToArray());

            Assert.Equal(slice, wrapped);
            composite.Release();
            buf.Release();

            foreach (var buffer in bufferList)
            {
                Assert.Equal(0, buffer.ReferenceCount);
            }
        }

        [Fact]
        public void TestComponentsLessThanLowerBound()
        {
            try
            {
                new CompositeByteBuffer(ALLOC, true, 0);
                Assert.False(true);
            }
            catch (ArgumentException e)
            {
                Assert.Equal("maxNumComponents: 0 (expected: >= 1)", e.Message);
            }
        }

        [Fact]
        public void TestComponentsEqualToLowerBound()
        {
            AssertCompositeBufCreated(1);
        }

        [Fact]
        public void TestComponentsGreaterThanLowerBound()
        {
            AssertCompositeBufCreated(5);
        }

        /**
         * Assert that a new {@linkplain CompositeByteBuf} was created successfully with the desired number of max
         * components.
         */
        static void AssertCompositeBufCreated(int expectedMaxComponents)
        {
            var buf = new CompositeByteBuffer(ALLOC, true, expectedMaxComponents);

            Assert.Equal(expectedMaxComponents, buf.MaxNumComponents);
            Assert.True(buf.Release());
        }

        [Fact]
        public void DiscardSomeReadBytesCorrectlyUpdatesLastAccessed()
        {
            DiscardCorrectlyUpdatesLastAccessed0(true);
        }

        [Fact]
        public void DiscardReadBytesCorrectlyUpdatesLastAccessed()
        {
            DiscardCorrectlyUpdatesLastAccessed0(false);
        }

        private static void DiscardCorrectlyUpdatesLastAccessed0(bool discardSome)
        {
            CompositeByteBuffer cbuf = Unpooled.CompositeBuffer();
            List<IByteBuffer> buffers = new List<IByteBuffer>(4);
            for (int i = 0; i < 4; i++)
            {
                IByteBuffer buf = Unpooled.Buffer().WriteInt(i);
                cbuf.AddComponent(true, buf);
                buffers.Add(buf);
            }

            // Skip the first 2 bytes which means even if we call discard*ReadBytes() later we can no drop the first
            // component as it is still used.
            cbuf.SkipBytes(2);
            if (discardSome)
            {
                cbuf.DiscardSomeReadBytes();
            }
            else
            {
                cbuf.DiscardReadBytes();
            }
            Assert.Equal(4, cbuf.NumComponents);

            // Now skip 3 bytes which means we should be able to drop the first component on the next discard*ReadBytes()
            // call.
            cbuf.SkipBytes(3);

            if (discardSome)
            {
                cbuf.DiscardSomeReadBytes();
            }
            else
            {
                cbuf.DiscardReadBytes();
            }
            Assert.Equal(3, cbuf.NumComponents);
            // Now skip again 3 bytes which should bring our readerIndex == start of the 3 component.
            cbuf.SkipBytes(3);

            // Read one int (4 bytes) which should bring our readerIndex == start of the 4 component.
            Assert.Equal(2, cbuf.ReadInt());
            if (discardSome)
            {
                cbuf.DiscardSomeReadBytes();
            }
            else
            {
                cbuf.DiscardReadBytes();
            }

            // Now all except the last component should have been dropped / released.
            Assert.Equal(1, cbuf.NumComponents);
            Assert.Equal(3, cbuf.ReadInt());
            if (discardSome)
            {
                cbuf.DiscardSomeReadBytes();
            }
            else
            {
                cbuf.DiscardReadBytes();
            }
            Assert.Equal(0, cbuf.NumComponents);

            // These should have been released already.
            foreach (IByteBuffer buffer in buffers)
            {
                Assert.Equal(0, buffer.ReferenceCount);
            }
            Assert.True(cbuf.Release());
        }
    }
}
