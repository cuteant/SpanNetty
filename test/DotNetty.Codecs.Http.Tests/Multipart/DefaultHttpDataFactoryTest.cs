// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Tests.Multipart
{
    using System;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http.Multipart;
    using Xunit;

    public sealed class DefaultHttpDataFactoryTest : IDisposable
    {
        // req1 equals req2
        readonly IHttpRequest _req1 = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Post, "/form");
        readonly IHttpRequest _req2 = new DefaultHttpRequest(HttpVersion.Http11, HttpMethod.Post, "/form");
        readonly DefaultHttpDataFactory _factory;

        public DefaultHttpDataFactoryTest()
        {
            // Before doing anything, assert that the requests are equal
            Assert.Equal(_req1.GetHashCode(), _req2.GetHashCode());
            Assert.True(_req1.Equals(_req2));

            _factory = new DefaultHttpDataFactory();
        }

        [Fact]
        public void CustomBaseDirAndDeleteOnExit()
        {
            DefaultHttpDataFactory defaultHttpDataFactory = new DefaultHttpDataFactory(true);
            string dir = "target/DefaultHttpDataFactoryTest/customBaseDirAndDeleteOnExit";
            defaultHttpDataFactory.SetBaseDir(dir);
            defaultHttpDataFactory.SetDeleteOnExit(true);
            IAttribute attr = defaultHttpDataFactory.CreateAttribute(_req1, "attribute1");
            IFileUpload fu = defaultHttpDataFactory.CreateFileUpload(
                    _req1, "attribute1", "f.txt", "text/plain", null, null, 0);
            Assert.Equal(dir, ((DiskAttribute)attr).BaseDirectory);
            Assert.Equal(dir, ((DiskFileUpload)fu).BaseDirectory);
            Assert.True(((DiskAttribute)attr).DeleteOnExit);
            Assert.True(((DiskFileUpload)fu).DeleteOnExit);
        }

        [Fact]
        public void CleanRequestHttpDataShouldIdentifiesRequestsByTheirIdentities()
        {
            // Create some data belonging to req1 and req2
            IAttribute attribute1 = _factory.CreateAttribute(_req1, "attribute1", "value1");
            IAttribute attribute2 = _factory.CreateAttribute(_req2, "attribute2", "value2");
            IFileUpload file1 = _factory.CreateFileUpload(
                _req1,
                "file1",
                "file1.txt",
                HttpPostBodyUtil.DefaultTextContentType,
                HttpHeaderValues.Identity.ToString(),
                Encoding.UTF8,
                123);

            IFileUpload file2 = _factory.CreateFileUpload(
                _req2,
                "file2",
                "file2.txt",
                HttpPostBodyUtil.DefaultTextContentType,
                HttpHeaderValues.Identity.ToString(),
                Encoding.UTF8,
                123);
            file1.SetContent(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("file1 content")));
            file2.SetContent(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("file2 content")));

            // Assert that they are not deleted
            Assert.NotNull(attribute1.GetByteBuffer());
            Assert.NotNull(attribute2.GetByteBuffer());
            Assert.NotNull(file1.GetByteBuffer());
            Assert.NotNull(file2.GetByteBuffer());
            Assert.Equal(1, attribute1.ReferenceCount);
            Assert.Equal(1, attribute2.ReferenceCount);
            Assert.Equal(1, file1.ReferenceCount);
            Assert.Equal(1, file2.ReferenceCount);

            // Clean up by req1
            _factory.CleanRequestHttpData(_req1);

            // Assert that data belonging to req1 has been cleaned up
            Assert.Null(attribute1.GetByteBuffer());
            Assert.Null(file1.GetByteBuffer());
            Assert.Equal(0, attribute1.ReferenceCount);
            Assert.Equal(0, file1.ReferenceCount);

            // But not req2
            Assert.NotNull(attribute2.GetByteBuffer());
            Assert.NotNull(file2.GetByteBuffer());
            Assert.Equal(1, attribute2.ReferenceCount);
            Assert.Equal(1, file2.ReferenceCount);
        }

        [Fact]
        public void RemoveHttpDataFromCleanShouldIdentifiesDataByTheirIdentities()
        {
            // Create some equal data items belonging to the same request
            IAttribute attribute1 = _factory.CreateAttribute(_req1, "attribute", "value");
            IAttribute attribute2 = _factory.CreateAttribute(_req1, "attribute", "value");
            IFileUpload file1 = _factory.CreateFileUpload(
                _req1,
                "file",
                "file.txt",
                HttpPostBodyUtil.DefaultTextContentType,
                HttpHeaderValues.Identity.ToString(),
                Encoding.UTF8,
                123);
            IFileUpload file2 = _factory.CreateFileUpload(
                _req1,
                "file",
                "file.txt",
                HttpPostBodyUtil.DefaultTextContentType,
                HttpHeaderValues.Identity.ToString(),
                Encoding.UTF8,
                123);
            file1.SetContent(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("file content")));
            file2.SetContent(Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes("file content")));

            // Before doing anything, assert that the data items are equal
            Assert.Equal(attribute1.GetHashCode(), attribute2.GetHashCode());
            Assert.True(attribute1.Equals(attribute2));
            Assert.Equal(file1.GetHashCode(), file2.GetHashCode());
            Assert.True(file1.Equals(file2));

            // Remove attribute2 and file2 from being cleaned up by factory
            _factory.RemoveHttpDataFromClean(_req1, attribute2);
            _factory.RemoveHttpDataFromClean(_req1, file2);

            // Clean up by req1
            _factory.CleanRequestHttpData(_req1);

            // Assert that attribute1 and file1 have been cleaned up
            Assert.Null(attribute1.GetByteBuffer());
            Assert.Null(file1.GetByteBuffer());
            Assert.Equal(0, attribute1.ReferenceCount);
            Assert.Equal(0, file1.ReferenceCount);

            // But not attribute2 and file2
            Assert.NotNull(attribute2.GetByteBuffer());
            Assert.NotNull(file2.GetByteBuffer());
            Assert.Equal(1, attribute2.ReferenceCount);
            Assert.Equal(1, file2.ReferenceCount);

            // Cleanup attribute2 and file2 manually to avoid memory leak, not via factory
            attribute2.Release();
            file2.Release();
            Assert.Equal(0, attribute2.ReferenceCount);
            Assert.Equal(0, file2.ReferenceCount);
        }

        public void Dispose() => _factory.CleanAllHttpData();
    }
}
