// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Text;
    using CuteAnt.Buffers;
    using DotNetty.Buffers;
    using DotNetty.Common;

    public abstract class AbstractMemoryHttpData : AbstractHttpData
    {
        // We pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
        // The SetContent buffer is short-lived and is likely to be collected at Gen0, and it offers a significant
        // improvement in Copy performance.
        const int c_defaultCopyBufferSize = 81920;

        IByteBuffer byteBuf;
        int chunkPosition;

        protected AbstractMemoryHttpData(string name, Encoding charset, long size) 
            : base(name, charset, size)
        {
        }

        public override void SetContent(IByteBuffer buffer)
        {
            Contract.Requires(buffer != null);

            long localsize = buffer.ReadableBytes;
            CheckSize(localsize, this.MaxSize);
            if (this.DefinedSize > 0 && this.DefinedSize < localsize)
            {
                ThrowHelper.ThrowIOException_OutOfSize(localsize, this.DefinedSize);
            }
            this.byteBuf?.Release();

            this.byteBuf = buffer;
            this.Size = localsize;
            this.SetCompleted();
        }

        public override void SetContent(Stream inputStream)
        {
            Contract.Requires(inputStream != null);

            if (!inputStream.CanRead)
            {
                ThrowHelper.ThrowArgumentException_Stream_NotReadable();
            }

            IByteBuffer buffer = ArrayPooled.Buffer();
            var bytes = BufferManager.Shared.Rent(c_defaultCopyBufferSize);
            int written = 0;
            try
            {
                while (true)
                {
                    int read = inputStream.Read(bytes, 0, bytes.Length);
                    if (read <= 0)
                    {
                        break;
                    }

                    buffer.WriteBytes(bytes, 0, read);
                    written += read;
                    CheckSize(written, this.MaxSize);
                }
            }
            finally
            {
                BufferManager.Shared.Return(bytes);
            }
            this.Size = written;
            if (this.DefinedSize > 0 && this.DefinedSize < this.Size)
            {
                ThrowHelper.ThrowIOException_OutOfSize(this.Size, this.DefinedSize);
            }

            this.byteBuf?.Release();
            this.byteBuf = buffer;
            this.SetCompleted();
        }

        public override void AddContent(IByteBuffer buffer, bool last)
        {
            if (buffer != null)
            {
                long localsize = buffer.ReadableBytes;
                CheckSize(this.Size + localsize, this.MaxSize);
                if (this.DefinedSize > 0 && this.DefinedSize < this.Size + localsize)
                {
                    ThrowHelper.ThrowIOException_OutOfSize(this.Size + localsize, this.DefinedSize);
                }

                this.Size += localsize;
                if (this.byteBuf == null)
                {
                    this.byteBuf = buffer;
                }
                else if (this.byteBuf is CompositeByteBuffer buf)
                {
                    buf.AddComponent(true, buffer);
                    buf.SetWriterIndex((int)this.Size);
                }
                else
                {
                    CompositeByteBuffer compositeBuffer = Unpooled.CompositeBuffer(int.MaxValue);
                    compositeBuffer.AddComponents(true, this.byteBuf, buffer);
                    compositeBuffer.SetWriterIndex((int)this.Size);
                    this.byteBuf = compositeBuffer;
                }
            }
            if (last)
            {
                this.SetCompleted();
            }
            else
            {
                if (buffer == null)
                {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer);
                }
            }
        }

        public override void Delete()
        {
            if (this.byteBuf != null)
            {
                this.byteBuf.Release();
                this.byteBuf = null;
            }
        }

        public override byte[] GetBytes()
        {
            if (this.byteBuf == null)
            {
                return Unpooled.Empty.Array;
            }

            var array = new byte[this.byteBuf.ReadableBytes];
            this.byteBuf.GetBytes(this.byteBuf.ReaderIndex, array);
            return array;
        }

        public override string GetString() => this.GetString(HttpConstants.DefaultEncoding);

        public override string GetString(Encoding encoding)
        {
            if (this.byteBuf == null)
            {
                return string.Empty;
            }
            if (encoding == null)
            {
                encoding = HttpConstants.DefaultEncoding;
            }
            return this.byteBuf.ToString(encoding);
        }

        public override IByteBuffer GetByteBuffer() => this.byteBuf;

        public override IByteBuffer GetChunk(int length)
        {
            if (this.byteBuf == null || length == 0 || this.byteBuf.ReadableBytes == 0)
            {
                this.chunkPosition = 0;
                return Unpooled.Empty;
            }
            int sizeLeft = this.byteBuf.ReadableBytes - this.chunkPosition;
            if (sizeLeft == 0)
            {
                this.chunkPosition = 0;
                return Unpooled.Empty;
            }
            int sliceLength = length;
            if (sizeLeft < length)
            {
                sliceLength = sizeLeft;
            }

            IByteBuffer chunk = this.byteBuf.RetainedSlice(this.chunkPosition, sliceLength);
            this.chunkPosition += sliceLength;
            return chunk;
        }

        public override bool IsInMemory => true;

        public override bool RenameTo(FileStream destination)
        {
            Contract.Requires(destination != null);

            if (!destination.CanWrite)
            {
                ThrowHelper.ThrowArgumentException_Stream_NotWritable();
            }
            if (this.byteBuf == null)
            {
                return true;
            }

            this.byteBuf.GetBytes(this.byteBuf.ReaderIndex, destination, this.byteBuf.ReadableBytes);
            destination.Flush();
            return true;
        }

        public override FileStream GetFile() => throw new IOException("Not represented by a stream");

        public override IReferenceCounted Touch(object hint)
        {
            this.byteBuf?.Touch(hint);
            return this;
        }
    }
}
