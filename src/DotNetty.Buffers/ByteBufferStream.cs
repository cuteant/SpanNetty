using System;
using System.IO;
using System.Runtime.CompilerServices;
using CuteAnt.Buffers;
using CuteAnt.Runtime;
using DotNetty.Common.Utilities;
#if !NET40
using System.Threading;
using System.Threading.Tasks;
using CuteAnt.AsyncEx;
#endif

namespace DotNetty.Buffers
{
    public class ByteBufferStream : Stream
    {
        #region @@ Fields @@

        private bool _isOpen;
        private bool _writable;
        private bool _releaseReferenceOnClosure;
        private readonly IByteBuffer _buffer;

        #endregion

        #region @@ Constructors @@

        public ByteBufferStream(IByteBuffer buffer) : this(buffer, true, false) { }

        public ByteBufferStream(IByteBuffer buffer, bool releaseReferenceOnClosure) : this(buffer, true, releaseReferenceOnClosure) { }

        public ByteBufferStream(IByteBuffer buffer, bool writable, bool releaseReferenceOnClosure)
        {
            if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }

            _buffer = buffer;
            _releaseReferenceOnClosure = releaseReferenceOnClosure;
            _isOpen = true;
            _writable = writable;
        }

        #endregion

        #region @@ Properties @@

        public IByteBuffer Buffer => _buffer;

        public override bool CanRead => _isOpen;

        public override bool CanSeek => _isOpen;

        public override bool CanWrite => _writable;

        public override long Length
        {
            get
            {
                EnsureNotClosed();
                return _buffer.WriterIndex;
            }
        }

        #endregion

        #region -- ReaderPosition --

        /// <summary>Only for reader position</summary>
        public override long Position
        {
            get
            {
                EnsureNotClosed();
                return _buffer.ReaderIndex;
            }
            set
            {
                EnsureNotClosed();
                _buffer.SetReaderIndex((int)value);
            }
        }

        /// <summary>Only for reader position</summary>
        public void MarkPosition()
        {
            EnsureNotClosed();
            _buffer.MarkReaderIndex();
        }

        /// <summary>Only for reader position</summary>
        public void ResetPosition()
        {
            EnsureNotClosed();
            _buffer.ResetReaderIndex();
        }

        /// <summary>Only for reader position</summary>
        public override long Seek(long offset, SeekOrigin origin)
        {
            EnsureNotClosed();
            switch (origin)
            {
                case SeekOrigin.Current:
                    offset += this.Position;
                    break;
                case SeekOrigin.End:
                    offset += this.Length;
                    break;
                    //case SeekOrigin.Begin:
                    //default:
                    //    break;
            }
            this.Position = offset;
            return this.Position;
        }

        #endregion

        #region -- WriterPosition --

        public int WriterPosition
        {
            get
            {
                EnsureNotClosed();
                return _buffer.WriterIndex;
            }
            set
            {
                EnsureNotClosed();
                _buffer.SetWriterIndex(value);
            }
        }

        public void MarkWriterPosition()
        {
            EnsureNotClosed();
            _buffer.MarkWriterIndex();
        }

        public void ResetWriterPosition()
        {
            EnsureNotClosed();
            _buffer.ResetWriterIndex();
        }

        #endregion

        #region -- SetLength --

        public override void SetLength(long value)
        {
            EnsureNotClosed();
            _buffer.EnsureWritable((int)value);
        }

        #endregion

        #region -- CopyToAsync --

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
        public override void CopyTo(Stream destination, int bufferSize)
        {
            EnsureNotClosed();

            var remaining = _buffer.ReadableBytes;
            if (remaining <= 0) { return; }

            ValidateCopyToArgs(destination);

            var ioBuffers = _buffer.GetIoBuffers();
            foreach (var ioBuffer in ioBuffers)
            {
                destination.Write(ioBuffer.Array, ioBuffer.Offset, ioBuffer.Count);
            }
        }
#endif

#if !NET40
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            EnsureNotClosed();

            var remaining = _buffer.ReadableBytes;
            if (remaining <= 0) { return; }

            ValidateCopyToArgs(destination);

            // If cancelled - return fast:
            if (cancellationToken.IsCancellationRequested) { return; }

            var ioBuffers = _buffer.GetIoBuffers();
            if (destination is MemoryStream || destination is BufferManagerOutputStream)
            {
                try
                {
                    // If destination is a MemoryStream, CopyTo synchronously:
                    foreach (var ioBuffer in ioBuffers)
                    {
                        if (cancellationToken.IsCancellationRequested) { return; }
                        destination.Write(ioBuffer.Array, ioBuffer.Offset, ioBuffer.Count);
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
            else
            {
                // If destination is not a memory stream, write there asynchronously:
                foreach (var ioBuffer in ioBuffers)
                {
                    if (cancellationToken.IsCancellationRequested) { return; }
                    await destination.WriteAsync(ioBuffer.Array, ioBuffer.Offset, ioBuffer.Count);
                }
            }
        }
#endif

        #endregion

        #region -- Read --

        public override int ReadByte()
        {
            EnsureNotClosed();
            try
            {
                return _buffer.ReadByte();
            }
            catch
            {
                return -1;
            }
        }

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
        public override int Read(Span<byte> buffer)
        {
            EnsureNotClosed();

            return _buffer.ReadBytes(buffer);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureNotClosed();

            return new ValueTask<int>(_buffer.ReadBytes(buffer));
        }
#endif

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }
            if (offset < 0) { ThrowHelper.ThrowArgumentOutOfRangeException_NeedNonNegNum(ExceptionArgument.offset); }
            if (count < 0) { ThrowHelper.ThrowArgumentOutOfRangeException_NeedNonNegNum(ExceptionArgument.count); }
            if (buffer.Length - offset < count) { ThrowHelper.ThrowArgumentException_InvalidOffLen(); }

            EnsureNotClosed();

            int read = Math.Min(count, _buffer.ReadableBytes);
            if (0u >= (uint)read) { return 0; }
            _buffer.ReadBytes(buffer, offset, read);
            return read;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            var readNum = this.Read(buffer, offset, count);
            return new CompletedAsyncResult<int>(readNum, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return CompletedAsyncResult<int>.End(asyncResult);
        }

#if !NET40
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return TaskConstants<int>.Canceled;
            }
            try
            {
                var readNum = this.Read(buffer, offset, count);
                return Task.FromResult(readNum);
            }
            //catch (OperationCanceledException oce)
            //{
            //    return Task.FromCancellation<int>(oce);
            //}
            catch (Exception ex2)
            {
                return AsyncUtils.FromException<int>(ex2);
            }
        }
#endif

        #endregion

        #region -- Write --

        public override void WriteByte(byte value)
        {
            EnsureNotClosed();
            EnsureWriteable();

            _buffer.WriteByte(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }
            if (offset < 0) { ThrowHelper.ThrowArgumentOutOfRangeException_NeedNonNegNum(ExceptionArgument.offset); }
            if (count < 0) { ThrowHelper.ThrowArgumentOutOfRangeException_NeedNonNegNum(ExceptionArgument.count); }
            if (buffer.Length - offset < count) { ThrowHelper.ThrowArgumentException_InvalidOffLen(); }

            EnsureNotClosed();
            EnsureWriteable();

            _buffer.WriteBytes(buffer, offset, count);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }
            if (offset < 0) { ThrowHelper.ThrowArgumentOutOfRangeException_NeedNonNegNum(ExceptionArgument.offset); }
            if (count < 0) { ThrowHelper.ThrowArgumentOutOfRangeException_NeedNonNegNum(ExceptionArgument.count); }
            if (buffer.Length - offset < count) { ThrowHelper.ThrowArgumentException_InvalidOffLen(); }

            EnsureNotClosed();
            EnsureWriteable();

            _buffer.WriteBytes(buffer, offset, count);
            return new CompletedAsyncResult(callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            CompletedAsyncResult.End(asyncResult);
        }

#if !NET40
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }
            if (offset < 0) { ThrowHelper.ThrowArgumentOutOfRangeException_NeedNonNegNum(ExceptionArgument.offset); }
            if (count < 0) { ThrowHelper.ThrowArgumentOutOfRangeException_NeedNonNegNum(ExceptionArgument.count); }
            if (buffer.Length - offset < count) { ThrowHelper.ThrowArgumentException_InvalidOffLen(); }

            EnsureNotClosed();
            EnsureWriteable();

            // If cancellation is already requested, bail early
            if (cancellationToken.IsCancellationRequested) { return AsyncUtils.FromCanceled(cancellationToken); }

            try
            {
                _buffer.WriteBytes(buffer, offset, count);
                return TaskConstants.Completed;
            }
            //catch (OperationCanceledException oce)
            //{
            //    return Task.FromCancellation<VoidTaskResult>(oce);
            //}
            catch (Exception exception)
            {
                return AsyncUtils.FromException(exception);
            }
        }
#endif

#if NETCOREAPP || NETSTANDARD_2_0_GREATER
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureNotClosed();
            EnsureWriteable();

            _buffer.WriteBytes(buffer);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureNotClosed();
            EnsureWriteable();

            _buffer.WriteBytes(buffer);
            return default;
        }
#endif

        #endregion

        #region -- Flush --

        public override void Flush() { }

#if !NET40
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) { return AsyncUtils.FromCanceled(cancellationToken); }

            try
            {
                Flush();
                return TaskConstants.Completed;
            }
            catch (Exception ex)
            {
                return AsyncUtils.FromException(ex);
            }
        }
#endif

        #endregion

        #region ++ Dispose ++

        protected override void Dispose(bool disposing)
        {
            _isOpen = false;
            _writable = false;
            if (_releaseReferenceOnClosure)
            {
                _releaseReferenceOnClosure = false;
                if (disposing)
                {
                    _buffer.Release();
                }
                else
                {
                    _buffer.SafeRelease();
                }
            }
        }

        #endregion

        #region ** Helper **

        [MethodImpl(InlineMethod.Value)]
        private void EnsureNotClosed()
        {
            if (!_isOpen) { ThrowHelper.ThrowObjectDisposedException_StreamIsClosed(); }
        }

        [MethodImpl(InlineMethod.Value)]
        private void EnsureWriteable()
        {
            if (!_writable) { ThrowHelper.ThrowNotSupportedException_UnwritableStream(); }
        }

        /// <summary>Validate the arguments to CopyTo, as would Stream.CopyTo.</summary>
        [MethodImpl(InlineMethod.Value)]
        private static void ValidateCopyToArgs(Stream destination)
        {
            if (destination is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.destination); }

            bool destinationCanWrite = destination.CanWrite;
            if (!destinationCanWrite)
            {
                if (destination.CanRead)
                {
                    ThrowHelper.ThrowNotSupportedException_UnwritableStream();
                }
                else
                {
                    ThrowHelper.ThrowObjectDisposedException_StreamIsClosed(ExceptionArgument.destination);
                }
            }
        }

        #endregion
    }
}
