// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Multipart
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.Multipart;
    using Xunit;

    public sealed class AbstractMemoryHttpDataTest
    {
        [Fact]
        public void SetContentFromFile()
        {
            TestHttpData test = new TestHttpData("test", Encoding.UTF8, 0);
            try
            {
                byte[] bytes = new byte[4096];
                (new Random()).NextBytes(bytes);
                using (var fs = File.Create(Path.GetTempFileName(), 4096, FileOptions.DeleteOnClose))
                {
                    fs.Write(bytes, 0, bytes.Length);
                    fs.Flush();
                    fs.Position = 0;
                    test.SetContent(fs);
                }
                var buf = test.GetByteBuffer();
                Assert.Equal(0, buf.ReaderIndex);
                Assert.Equal(buf.WriterIndex, bytes.Length);
                Assert.True(bytes.AsSpan().SequenceEqual(test.GetBytes()));
                Assert.True(bytes.AsSpan().SequenceEqual(ByteBufferUtil.GetBytes(buf)));
            }
            finally
            {
                //release the ByteBuf
                test.Delete();
            }
        }

        [Fact]
        public void RenameTo()
        {
            TestHttpData test = new TestHttpData("test", Encoding.UTF8, 0);
            try
            {
                int totalByteCount = 4096;
                byte[] bytes = new byte[totalByteCount];
                (new Random()).NextBytes(bytes);
                var content = Unpooled.WrappedBuffer(bytes);
                test.SetContent(content);
                using (var fs = File.Create(Path.GetTempFileName(), 4096, FileOptions.DeleteOnClose))
                {
                    var succ = test.RenameTo(fs);
                    Assert.True(succ);
                    fs.Position = 0;
                    var buf = new byte[totalByteCount];
                    fs.Read(buf, 0, buf.Length);
                    Assert.True(bytes.AsSpan().SequenceEqual(buf));
                }
            }
            finally
            {
                //release the ByteBuf in AbstractMemoryHttpData
                test.Delete();
            }
        }

        [Fact]
        public void SetContentFromStream()
        {
            // definedSize=0
            TestHttpData test = new TestHttpData("test", Encoding.UTF8, 0);
            string contentStr = "foo_test";
            var buf = Unpooled.WrappedBuffer(Encoding.UTF8.GetBytes(contentStr));
            buf.MarkReaderIndex();
            var bs = new ByteBufferStream(buf);
            try
            {
                test.SetContent(bs);
                Assert.False(buf.IsReadable());
                Assert.Equal(test.GetString(Encoding.UTF8), contentStr);
                buf.ResetReaderIndex();
                Assert.True(ByteBufferUtil.Equals(buf, test.GetByteBuffer()));
            }
            finally
            {
                bs.Close();
            }

            var random = new Random();

            for (int i = 0; i < 20; i++)
            {
                // Generate input data bytes.
                int size = random.Next(short.MaxValue);
                var bytes = new byte[size];

                random.NextBytes(bytes);

                // Generate parsed HTTP data block.
                var httpData = new TestHttpData("name", Encoding.UTF8, 0);

                httpData.SetContent(new MemoryStream(bytes));

                // Validate stored data.
                IByteBuffer buffer = httpData.GetByteBuffer();

                Assert.Equal(0, buffer.ReaderIndex);
                Assert.Equal(bytes.Length, buffer.WriterIndex);

                var data = new byte[bytes.Length];
                buffer.GetBytes(buffer.ReaderIndex, data);

                Assert.True(data.AsSpan().SequenceEqual(bytes));
            }
        }

        sealed class TestHttpData : AbstractMemoryHttpData
        {
            public TestHttpData(string name, Encoding contentEncoding, long size)
                : base(name, contentEncoding, size)
            {
            }

            public override int CompareTo(IInterfaceHttpData other)
            {
                throw new NotSupportedException("Should never be called.");
            }

            public override HttpDataType DataType => throw new NotSupportedException("Should never be called.");

            public override IByteBufferHolder Copy()
            {
                throw new NotSupportedException("Should never be called.");
            }

            public override IByteBufferHolder Duplicate()
            {
                throw new NotSupportedException("Should never be called.");
            }

            public override IByteBufferHolder RetainedDuplicate()
            {
                throw new NotSupportedException("Should never be called.");
            }

            public override IByteBufferHolder Replace(IByteBuffer content)
            {
                throw new NotSupportedException("Should never be called.");
            }
        }
    }
}
