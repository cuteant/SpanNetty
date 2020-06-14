// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System.Buffers;
    using System.IO;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common;

    /// <summary>
    /// Abstract Memory HttpData implementation
    /// </summary>
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
            if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }

            long localsize = buffer.ReadableBytes;
            CheckSize(localsize, this.MaxSize);
            if (this.DefinedSize > 0 && this.DefinedSize < localsize)
            {
                ThrowHelper.ThrowIOException_OutOfSize(localsize, this.DefinedSize);
            }
            _ = (this.byteBuf?.Release());

            this.byteBuf = buffer;
            this.Size = localsize;
            this.SetCompleted();
        }

        public override void SetContent(Stream inputStream)
        {
            if (inputStream is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.inputStream); }

            if (!inputStream.CanRead)
            {
                ThrowHelper.ThrowArgumentException_Stream_NotReadable();
            }

            IByteBuffer buffer = ArrayPooled.Buffer();
            var bytes = ArrayPool<byte>.Shared.Rent(c_defaultCopyBufferSize);
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

                    _ = buffer.WriteBytes(bytes, 0, read);
                    written += read;
                    CheckSize(written, this.MaxSize);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
            this.Size = written;
            if (this.DefinedSize > 0 && this.DefinedSize < this.Size)
            {
                ThrowHelper.ThrowIOException_OutOfSize(this.Size, this.DefinedSize);
            }

            _ = (this.byteBuf?.Release());
            this.byteBuf = buffer;
            this.SetCompleted();
        }

        public override void AddContent(IByteBuffer buffer, bool last)
        {
            if (buffer is object)
            {
                long localsize = buffer.ReadableBytes;
                CheckSize(this.Size + localsize, this.MaxSize);
                if (this.DefinedSize > 0 && this.DefinedSize < this.Size + localsize)
                {
                    ThrowHelper.ThrowIOException_OutOfSize(this.Size + localsize, this.DefinedSize);
                }

                this.Size += localsize;
                if (this.byteBuf is null)
                {
                    this.byteBuf = buffer;
                }
                else if (this.byteBuf is CompositeByteBuffer buf)
                {
                    _ = buf.AddComponent(true, buffer);
                    _ = buf.SetWriterIndex((int)this.Size);
                }
                else
                {
                    CompositeByteBuffer compositeBuffer = ArrayPooled.CompositeBuffer(int.MaxValue);
                    _ = compositeBuffer.AddComponents(true, this.byteBuf, buffer);
                    _ = compositeBuffer.SetWriterIndex((int)this.Size);
                    this.byteBuf = compositeBuffer;
                }
            }
            if (last)
            {
                this.SetCompleted();
            }
            else
            {
                if (buffer is null)
                {
                    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer);
                }
            }
        }

        public override void Delete()
        {
            if (this.byteBuf is object)
            {
                _ = this.byteBuf.Release();
                this.byteBuf = null;
            }
        }

        public override byte[] GetBytes()
        {
            if (this.byteBuf is null)
            {
                return Unpooled.Empty.Array;
            }

            var array = new byte[this.byteBuf.ReadableBytes];
            _ = this.byteBuf.GetBytes(this.byteBuf.ReaderIndex, array);
            return array;
        }

        public override string GetString() => this.GetString(HttpConstants.DefaultEncoding);

        public override string GetString(Encoding encoding)
        {
            if (this.byteBuf is null)
            {
                return string.Empty;
            }
            if (encoding is null)
            {
                encoding = HttpConstants.DefaultEncoding;
            }
            return this.byteBuf.ToString(encoding);
        }

        /// <summary>
        /// Utility to go from a In Memory FileUpload
        /// to a Disk (or another implementation) FileUpload
        /// </summary>
        /// <returns>the attached ByteBuf containing the actual bytes</returns>
        public override IByteBuffer GetByteBuffer() => this.byteBuf;

        public override IByteBuffer GetChunk(int length)
        {
            if (this.byteBuf is null || 0u >= (uint)length || 0u >= (uint)this.byteBuf.ReadableBytes)
            {
                this.chunkPosition = 0;
                return Unpooled.Empty;
            }
            int sizeLeft = this.byteBuf.ReadableBytes - this.chunkPosition;
            if (0u >= (uint)sizeLeft)
            {
                this.chunkPosition = 0;
                return Unpooled.Empty;
            }
            int sliceLength = length;
            if ((uint)sizeLeft < (uint)length)
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
            if (destination is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.destination); }

            if (!destination.CanWrite)
            {
                ThrowHelper.ThrowArgumentException_Stream_NotWritable();
            }
            if (this.byteBuf is null)
            {
                // empty file
                return true;
            }

            _ = this.byteBuf.GetBytes(this.byteBuf.ReaderIndex, destination, this.byteBuf.ReadableBytes);
            destination.Flush();
            return true;
        }

        public override FileStream GetFile() => throw new IOException("Not represented by a stream");

        public override IReferenceCounted Touch(object hint)
        {
            _ = (this.byteBuf?.Touch(hint));
            return this;
        }
    }
}
