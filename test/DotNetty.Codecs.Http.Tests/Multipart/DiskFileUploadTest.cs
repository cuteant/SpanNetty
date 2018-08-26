// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Multipart
{
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.Multipart;
    using Xunit;

    public sealed class DiskFileUploadTest
    {
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
    }
}
