// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Buffers
{
    using System;
    using System.Diagnostics.Contracts;
    using System.IO;
    using CuteAnt.Runtime;
    using DotNetty.Common.Utilities;
#if !NET40
    using System.Threading;
    using System.Threading.Tasks;
    using CuteAnt.AsyncEx;
#endif

    public sealed class ReadOnlyByteBufferStream : Stream
    {
        readonly IByteBuffer buffer;
        bool releaseReferenceOnClosure;

        public ReadOnlyByteBufferStream(IByteBuffer buffer, bool releaseReferenceOnClosure)
        {
            Contract.Requires(buffer != null);

            this.buffer = buffer;
            this.releaseReferenceOnClosure = releaseReferenceOnClosure;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => this.buffer.WriterIndex;

        public override long Position
        {
            get => this.buffer.ReaderIndex;
            set => this.buffer.SetReaderIndex((int)value);
        }

        public override int Read(byte[] output, int offset, int count)
        {
            if (offset + count > output.Length)
            {
                ThrowHelper.ThrowArgumentException_Sum();
            }

            int read = Math.Min(count, this.buffer.ReadableBytes);
            this.buffer.ReadBytes(output, offset, read);
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
            if (buffer == null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.buffer); }
            if (offset < 0) { ThrowHelper.ThrowArgumentOutOfRangeException_NeedNonNegNum(ExceptionArgument.offset); }
            if (count < 0) { ThrowHelper.ThrowArgumentOutOfRangeException_NeedNonNegNum(ExceptionArgument.count); }
            if (buffer.Length - offset < count) { ThrowHelper.ThrowArgumentException_InvalidOffLen(); }

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
            //	return Task.FromCancellation<VoidTaskResult>(oce);
            //}
            catch (Exception ex2)
            {
                //return AsyncUtils.CreateTaskFromException<VoidTaskResult>(ex2);
                //tcs.TrySetException(ex2);
                return AsyncUtils.FromException<int>(ex2);
            }
        }
#endif

        public override void Flush()
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (this.releaseReferenceOnClosure)
            {
                this.releaseReferenceOnClosure = false;
                if (disposing)
                {
                    this.buffer.Release();
                }
                else
                {
                    this.buffer.SafeRelease();
                }
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin == SeekOrigin.Current)
            {
                offset += this.Position;
            }
            else if (origin == SeekOrigin.End)
            {
                offset += this.Length;
            }

            this.Position = offset;
            return this.Position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] input, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
