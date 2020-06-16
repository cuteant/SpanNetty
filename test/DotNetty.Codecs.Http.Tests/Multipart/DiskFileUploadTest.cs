// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Multipart
{
    using System;
    using System.IO;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.Multipart;
    using Xunit;

    public sealed class DiskFileUploadTest
    {
        [Fact]
        public void SpecificCustomBaseDir()
        {
            var baseDir = "target/DiskFileUploadTest/testSpecificCustomBaseDir";
            var fullDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, baseDir);
            if (!Directory.Exists(fullDir))
            {
                Directory.CreateDirectory(fullDir); // we don't need to clean it since it is in volatile files anyway
            }
            DiskFileUpload f =
                    new DiskFileUpload("d1", "d1", "application/json", null, null, 100,
                            fullDir, false);

            f.SetContent(Unpooled.Empty);

            Assert.StartsWith(fullDir.TrimEnd('\\', '/').Replace(@"\", "/"), f.GetFile().Name.Replace(@"\", "/"));
            Assert.True(File.Exists(f.GetFile().Name));
            Assert.Equal(0, f.GetFile().Length);
            f.Delete();
        }

        [Fact]
        public void DiskFileUploadEquals()
        {
            var f2 = new DiskFileUpload("d1", "d1", "application/json", null, null, 100);
            Assert.Equal(f2, f2);
            f2.Delete();
        }

        [Fact]
        public void EmptyBufferSetMultipleTimes()
        {
            var f = new DiskFileUpload("d1", "d1", "application/json", null, null, 100);

            f.SetContent(Unpooled.Empty);

            Assert.NotNull(f.GetFile());
            Assert.Equal(0, f.GetFile().Length);
            f.SetContent(Unpooled.Empty);
            Assert.NotNull(f.GetFile());
            Assert.Equal(0, f.GetFile().Length);
            f.Delete();

        }

        [Fact]
        public void EmptyBufferSetAfterNonEmptyBuffer()
        {
            var f = new DiskFileUpload("d1", "d1", "application/json", null, null, 100);

            f.SetContent(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4 }));

            Assert.NotNull(f.GetFile());
            Assert.Equal(4, f.GetFile().Length);
            f.SetContent(Unpooled.Empty);
            Assert.NotNull(f.GetFile());
            Assert.Equal(0, f.GetFile().Length);
            f.Delete();
        }

        [Fact]
        public void NonEmptyBufferSetMultipleTimes()
        {
            var f = new DiskFileUpload("d1", "d1", "application/json", null, null, 100);

            f.SetContent(Unpooled.WrappedBuffer(new byte[] { 1, 2, 3, 4 }));

            Assert.NotNull(f.GetFile());
            Assert.Equal(4, f.GetFile().Length);
            f.SetContent(Unpooled.WrappedBuffer(new byte[] { 1, 2 }));
            Assert.NotNull(f.GetFile());
            Assert.Equal(2, f.GetFile().Length);
            f.Delete();
        }

        [Fact]
        public void AddContents()
        {
            DiskFileUpload f1 = new DiskFileUpload("file1", "file1", "application/json", null, null, 0);
            try
            {
                string json = "{\"foo\":\"bar\"}";
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                f1.AddContent(Unpooled.WrappedBuffer(bytes), true);
                Assert.Equal(json, f1.GetString());
                Assert.True(bytes.AsSpan().SequenceEqual(f1.GetBytes()));
                var fis = f1.GetFile();
                Assert.Equal(bytes.Length, fis.Length);

                byte[] buf = new byte[bytes.Length];
                int offset = 0;
                int read = 0;
                int len = buf.Length;
                fis.Position = 0;
                while ((read = fis.Read(buf, offset, len)) > 0)
                {
                    len -= read;
                    offset += read;
                    if (len <= 0 || offset >= buf.Length)
                    {
                        break;
                    }
                }
                Assert.True(bytes.AsSpan().SequenceEqual(buf));
            }
            finally
            {
                f1.Delete();
            }
        }

        [Fact]
        public void SetContentFromByteBuf()
        {
            DiskFileUpload f1 = new DiskFileUpload("file2", "file2", "application/json", null, null, 0);
            try
            {
                string json = "{\"hello\":\"world\"}";
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                f1.SetContent(Unpooled.WrappedBuffer(bytes));
                Assert.Equal(json, f1.GetString());
                Assert.True(bytes.AsSpan().SequenceEqual(f1.GetBytes()));
                var file = f1.GetFile();
                Assert.Equal(bytes.Length, file.Length);
                file.Position = 0;
                Assert.True(bytes.AsSpan().SequenceEqual(DoReadFile(file, bytes.Length)));
            }
            finally
            {
                f1.Delete();
            }
        }

        [Fact]
        public void SetContentFromInputStream()
        {
            string json = "{\"hello\":\"world\",\"foo\":\"bar\"}";
            DiskFileUpload f1 = new DiskFileUpload("file3", "file3", "application/json", null, null, 0);
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                var buf = Unpooled.WrappedBuffer(bytes);
                var bs = new ByteBufferStream(buf);
                try
                {
                    f1.SetContent(bs);
                    Assert.Equal(json, f1.GetString());
                    Assert.True(bytes.AsSpan().SequenceEqual(f1.GetBytes()));
                    var file = f1.GetFile();
                    Assert.Equal(bytes.Length, file.Length);
                    Assert.True(bytes.AsSpan().SequenceEqual(DoReadFile(file, bytes.Length)));
                }
                finally
                {
                    buf.Release();
                }
            }
            finally
            {
                f1.Delete();
            }
        }

        private static byte[] DoReadFile(FileStream fis, int maxRead)
        {
            try
            {
                byte[] buf = new byte[maxRead];
                int offset = 0;
                int read = 0;
                int len = buf.Length;
                while ((read = fis.Read(buf, offset, len)) > 0)
                {
                    len -= read;
                    offset += read;
                    if (len <= 0 || offset >= buf.Length)
                    {
                        break;
                    }
                }
                return buf;
            }
            finally
            {
                fis.Close();
            }
        }
    }
}
