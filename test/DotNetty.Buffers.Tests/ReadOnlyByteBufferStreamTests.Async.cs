// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET40
namespace DotNetty.Buffers.Tests
{
    using System;
    using System.Threading.Tasks;
    using Xunit;

    public partial class ReadOnlyByteBufferStreamTests
    {
        [Fact]
        public async Task CanReadCountBytesIntoBufferAsync()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[4];
            int read = await stream.ReadAsync(output, 0, output.Length);

            Assert.Equal(4, read);
            Assert.Equal(new byte[] { 42, 42, 42, 42 }, output);
        }


        [Fact]
        public async Task CanReadCountBytesIntoBufferAtOffsetAsync()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[4];
            int read = await stream.ReadAsync(output, 1, 2);

            Assert.Equal(2, read);
            Assert.Equal(new byte[] { 0, 42, 42, 0 }, output);
        }

        [Fact]
        public async Task CanDoMultipleReadsFromStreamIntoBufferAsync()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[4];
            await stream.ReadAsync(output, 0, 2);
            int read = await stream.ReadAsync(output, 2, 2);

            Assert.Equal(2, read);
            Assert.Equal(new byte[] { 42, 42, 42, 42 }, output);
        }

        [Fact]
        public async Task SingleReadCannotPassTheEndOfTheStreamAsync()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[6];

            // single read is too big for the stream
            await stream.ReadAsync(output, 0, 6);

            Assert.Equal(new byte[] { 42, 42, 42, 42, 0, 0 }, output);
        }

        [Fact]
        public async Task MultiReadCannotPassTheEndOfTheStreamAsync()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[6];

            // 2nd read is too big for the stream
            await stream.ReadAsync(output, 0, 2);
            await stream.ReadAsync(output, 0, 4);

            Assert.Equal(new byte[] { 42, 42, 0, 0, 0, 0 }, output);
        }

        [Fact]
        public async Task SingleReadCannotWritePastTheEndOfTheDestinationBufferAsync()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[3];

            // single read is too big for the output buffer
            var e = await Assert.ThrowsAsync<ArgumentException>(async () => await stream.ReadAsync(output, 0, 4));
#if TEST40
            Assert.Equal("The sum of offset and count is larger than the output length", e.Message);
#else
            Assert.Equal("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.", e.Message);
#endif
        }

        [Fact]
        public async Task MultiReadCannotWritePastTheEndOfTheDestinationBufferAsync()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[3];

            // 2nd read is too big for the output buffer
            await stream.ReadAsync(output, 0, 2);
            await Assert.ThrowsAsync<ArgumentException>(async () => await stream.ReadAsync(output, 2, 2));
        }

        [Fact]
        public async Task ReadZeroBytesFromTheEndOfTheStreamAsync()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[4];

            await stream.ReadAsync(output, 0, 4);
            int read = await stream.ReadAsync(output, 0, 4);

            Assert.Equal(0, read);
        }

        [Fact]
        public async Task CanGetConsistentStreamLengthAcrossReadsAsync()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);
            var output = new byte[4];

            await stream.ReadAsync(output, 0, 4);
            Assert.Equal(this.testBuffer.WriterIndex, stream.Length);

            await stream.ReadAsync(output, 0, 4);
            Assert.Equal(this.testBuffer.WriterIndex, stream.Length);
        }

        [Fact]
        public async Task CanGetTheStreamPositionAsync()
        {
            var stream = new ReadOnlyByteBufferStream(this.testBuffer, false);

            Assert.Equal(0, stream.Position);

            var output = new byte[4];
            int read = await stream.ReadAsync(output, 0, 2);

            Assert.Equal(2, stream.Position);
        }
    }
}
#endif