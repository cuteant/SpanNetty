// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.Multipart
{
    using System;
    using System.IO;
    using System.Text;
    using CuteAnt.Buffers;
    using DotNetty.Buffers;
    using DotNetty.Common;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public abstract class AbstractDiskHttpData : AbstractHttpData
    {
        // We pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
        // The SetContent/RenameTo buffer is short-lived and is likely to be collected at Gen0, and it offers a significant
        // improvement in Copy performance.
        const int c_defaultCopyBufferSize = 81920;

        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<AbstractDiskHttpData>();

        FileStream fileStream;
        long chunkPosition;

        protected AbstractDiskHttpData(string name, Encoding charset, long size) : base(name, charset, size)
        {
        }

        protected abstract string DiskFilename { get; }

        protected abstract string Prefix { get; }

        protected abstract string BaseDirectory { get; }

        protected abstract string Postfix { get; }

        protected abstract bool DeleteOnExit { get; }

        FileStream TempFile()
        {
            string newpostfix;
            string diskFilename = this.DiskFilename;
            if (diskFilename != null)
            {
                newpostfix = '_' + diskFilename;
            }
            else
            {
                newpostfix = this.Postfix;
            }
            string directory = string.IsNullOrWhiteSpace(this.BaseDirectory)
                ? Path.GetTempPath()
                : Path.IsPathRooted(this.BaseDirectory) ? this.BaseDirectory : Path.Combine(Path.GetTempPath(), this.BaseDirectory);
            // File.createTempFile
            string fileName = Path.Combine(directory, $"{this.Prefix}{Path.GetRandomFileName()}{newpostfix}");
            FileStream tmpFile = File.Create(fileName, 4096, // DefaultBufferSize
                this.DeleteOnExit ? FileOptions.DeleteOnClose : FileOptions.None);
            return tmpFile;
        }

        public override void SetContent(IByteBuffer buffer)
        {
            if (null == buffer) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }
            try
            {
                if (this.fileStream != null)
                {
                    this.Delete();
                }

                this.fileStream = this.TempFile();

                this.Size = buffer.ReadableBytes;
                CheckSize(this.Size, this.MaxSize);
                if (this.DefinedSize > 0 && this.DefinedSize < this.Size)
                {
                    ThrowHelper.ThrowIOException_OutOfSize(this.Size, this.DefinedSize);
                }
                if (buffer.ReadableBytes == 0)
                {
                    // empty file
                    return;
                }

                buffer.GetBytes(buffer.ReaderIndex, this.fileStream, buffer.ReadableBytes);
                buffer.SetReaderIndex(buffer.ReaderIndex + buffer.ReadableBytes);
                this.fileStream.Flush();
                this.SetCompleted();
            }
            finally
            {
                // Release the buffer as it was retained before and we not need a reference to it at all
                // See https://github.com/netty/netty/issues/1516
                buffer.Release();
            }
        }

        public override void AddContent(IByteBuffer buffer, bool last)
        {
            if (buffer != null)
            {
                try
                {
                    int localsize = buffer.ReadableBytes;
                    CheckSize(this.Size + localsize, this.MaxSize);
                    if (this.DefinedSize > 0 && this.DefinedSize < this.Size + localsize)
                    {
                        ThrowHelper.ThrowIOException_OutOfSize(this.Size, this.DefinedSize);
                    }
                    if (this.fileStream == null)
                    {
                        this.fileStream = this.TempFile();
                    }
                    buffer.GetBytes(buffer.ReaderIndex, this.fileStream, buffer.ReadableBytes);
                    buffer.SetReaderIndex(buffer.ReaderIndex + localsize);
                    this.fileStream.Flush();

                    this.Size += buffer.ReadableBytes;
                }
                finally
                {
                    // Release the buffer as it was retained before and we not need a reference to it at all
                    // See https://github.com/netty/netty/issues/1516
                    buffer.Release();
                }
            }
            if (last)
            {
                if (this.fileStream == null)
                {
                    this.fileStream = this.TempFile();
                }
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

        public override void SetContent(Stream source)
        {
            if (null == source) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.source); }

            if (this.fileStream != null)
            {
                this.Delete();
            }

            this.fileStream = this.TempFile();
            int written = 0;
            var bytes = BufferManager.Shared.Rent(c_defaultCopyBufferSize);
            try
            {
                while (true)
                {
                    int read = source.Read(bytes, 0, bytes.Length);
                    if (read <= 0)
                    {
                        break;
                    }

                    written += read;
                    CheckSize(written, this.MaxSize);
                    this.fileStream.Write(bytes, 0, read);
                }
            }
            finally
            {
                BufferManager.Shared.Return(bytes);
            }
            this.fileStream.Flush();
            // Reset the position to start for reads
            this.fileStream.Position -= written;

            this.Size = written;
            if (this.DefinedSize > 0 && this.DefinedSize < this.Size)
            {
                try
                {
                    Delete(this.fileStream);
                }
                catch (Exception error)
                {
                    if (Logger.WarnEnabled) Logger.FailedToDelete(this.fileStream, error);
                }
                this.fileStream = null;
                ThrowHelper.ThrowIOException_OutOfSize(this.Size, this.DefinedSize);
            }
            //isRenamed = true;
            this.SetCompleted();
        }

        public override void Delete()
        {
            if (this.fileStream != null)
            {
                try
                {
                    Delete(this.fileStream);
                }
                catch (IOException error)
                {
                    if (Logger.WarnEnabled) Logger.FailedToDeleteFile(error);
                }

                this.fileStream = null;
            }
        }

        public override byte[] GetBytes() => this.fileStream == null
            ? ArrayExtensions.ZeroBytes : ReadFrom(this.fileStream);

        public override IByteBuffer GetByteBuffer()
        {
            if (this.fileStream == null)
            {
                return Unpooled.Empty;
            }

            var array = ReadFrom(this.fileStream);
            return Unpooled.WrappedBuffer(array);
        }

        public override IByteBuffer GetChunk(int length)
        {
            if (this.fileStream == null || 0u >= (uint)length)
            {
                this.chunkPosition = 0L;
                return Unpooled.Empty;
            }
            var sizeLeft = this.fileStream.Length - this.chunkPosition;
            if (sizeLeft == 0L)
            {
                this.chunkPosition = 0L;
                return Unpooled.Empty;
            }
            int sliceLength = length;
            if (sizeLeft < length)
            {
                sliceLength = (int)sizeLeft;
            }

            var lastPosition = this.fileStream.Position;
            this.fileStream.Seek(this.chunkPosition, SeekOrigin.Begin);
            int read = 0;
            var bytes = new byte[sliceLength];
            while (read < sliceLength)
            {
                int readnow = this.fileStream.Read(bytes, read, sliceLength - read);
                if (readnow <= 0)
                {
                    break;
                }

                read += readnow;
            }
            this.fileStream.Seek(lastPosition, SeekOrigin.Begin);
            if (read == 0)
            {
                return Unpooled.Empty;
            }
            else
            {
                this.chunkPosition += read;
            }
            var buffer = Unpooled.WrappedBuffer(bytes);
            buffer.SetReaderIndex(0);
            buffer.SetWriterIndex(read);
            return buffer;
        }

        public override string GetString() => this.GetString(HttpConstants.DefaultEncoding);

        public override string GetString(Encoding encoding)
        {
            if (this.fileStream == null)
            {
                return string.Empty;
            }
            byte[] array = ReadFrom(this.fileStream);
            if (encoding == null)
            {
                encoding = HttpConstants.DefaultEncoding;
            }

            return encoding.GetString(array);
        }

        public override bool IsInMemory => false;

        public override bool RenameTo(FileStream destination)
        {
            if (null == destination) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.destination); }
            if (this.fileStream == null)
            {
                ThrowHelper.ThrowInvalidOperationException_NoFileDefined();
            }

            // must copy
            var buffer = BufferManager.Shared.Rent(c_defaultCopyBufferSize);
            int position = 0;
            var lastPosition = this.fileStream.Position;
            this.fileStream.Seek(0, SeekOrigin.Begin);

            try
            {
                while (position < this.Size)
                {
                    int read = this.fileStream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        break;
                    }

                    destination.Write(buffer, 0, read);
                    position += read;
                }
            }
            finally
            {
                BufferManager.Shared.Return(buffer);
            }

            if (position == this.Size)
            {
                try
                {
                    Delete(this.fileStream);
                }
                catch (IOException exception)
                {
                    if (Logger.WarnEnabled) Logger.FailedToDeleteFile(exception);
                }
                this.fileStream = destination;
                this.fileStream.Seek(lastPosition, SeekOrigin.Begin);
                return true;
            }
            else
            {
                try
                {
                    Delete(destination);
                }
                catch (IOException exception)
                {
                    if (Logger.WarnEnabled) Logger.FailedToDeleteFile(exception);
                }
                this.fileStream.Seek(lastPosition, SeekOrigin.Begin);
                return false;
            }
        }

        static void Delete(FileStream fileStream)
        {
            string fileName = fileStream.Name;
            fileStream.Dispose();
            File.Delete(fileName);
        }

        static byte[] ReadFrom(Stream fileStream)
        {
            long srcsize = fileStream.Length;
            if (srcsize > int.MaxValue)
            {
                ThrowHelper.ThrowArgumentException_FileTooBig();
            }

            var array = new byte[(int)srcsize];
            var lastPosition = fileStream.Position;
            fileStream.Seek(0, SeekOrigin.Begin);
            fileStream.Read(array, 0, array.Length);
            fileStream.Seek(lastPosition, SeekOrigin.Begin);
            return array;
        }

        public override FileStream GetFile() => this.fileStream;

        public override IReferenceCounted Touch(object hint) => this;
    }
}
