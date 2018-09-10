// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Streams
{
    using System;
    using System.IO;
    using System.Threading;
    using DotNetty.Buffers;

    public class ChunkedStream : IChunkedInput<IByteBuffer>
    {
        public static readonly int DefaultChunkSize = 8192;

        readonly Stream input;
        readonly int chunkSize;
        long offset; bool closed;

        public ChunkedStream(Stream input) : this(input, DefaultChunkSize)
        {
        }

        public ChunkedStream(Stream input, int chunkSize)
        {
            if (null == input) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.input); }
            if (chunkSize <= 0) { ThrowHelper.ThrowArgumentException_Positive(chunkSize, ExceptionArgument.chunkSize); }

            this.input = input;
            this.chunkSize = chunkSize;
        }

        public long TransferredBytes => this.offset;

        public bool IsEndOfInput => this.closed || (this.input.Position == this.input.Length);

        public void Close()
        {
            this.closed = true;
            this.input.Dispose();
        }

        public IByteBuffer ReadChunk(IByteBufferAllocator allocator)
        {
            if (this.IsEndOfInput)
            {
                return null;
            }

            long availableBytes = this.input.Length - this.input.Position;
            int readChunkSize = availableBytes <= 0 
                ? this.chunkSize 
                : (int)Math.Min(this.chunkSize, availableBytes);

            bool release = true;
            IByteBuffer buffer = allocator.Buffer(readChunkSize);
            try
            {
                // transfer to buffer
                int count = buffer.SetBytesAsync(buffer.WriterIndex, this.input, readChunkSize, CancellationToken.None).Result;
                buffer.SetWriterIndex(buffer.WriterIndex + count);
                this.offset += count;

                release = false;
            }
            finally
            {
                if (release)
                {
                    buffer.Release();
                }
            }

            return buffer;
        }

        public long Length => -1;

        public long Progress => this.offset;
    }
}
