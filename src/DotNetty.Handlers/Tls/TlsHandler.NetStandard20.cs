#if NETSTANDARD2_0
namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    partial class TlsHandler
    {
        partial class MediationStream
        {
            private byte[] _input;
            private ArraySegment<byte> _sslOwnedBuffer;
            private int _inputStartOffset;
            private int _readByteCount;

            public void SetSource(byte[] source, int offset)
            {
                _input = source;
                _inputStartOffset = offset;
                _inputOffset = 0;
                _inputLength = 0;
            }

            public void ResetSource()
            {
                _input = null;
                _inputLength = 0;
            }

            public void ExpandSource(int count)
            {
                Debug.Assert(_input != null);

                _inputLength += count;

                ArraySegment<byte> sslBuffer = _sslOwnedBuffer;
                if (sslBuffer.Array == null)
                {
                    // there is no pending read operation - keep for future
                    return;
                }
                _sslOwnedBuffer = default(ArraySegment<byte>);

                _readByteCount = this.ReadFromInput(sslBuffer.Array, sslBuffer.Offset, sslBuffer.Count);
                // hack: this tricks SslStream's continuation to run synchronously instead of dispatching to TP. Remove once Begin/EndRead are available. 
                new Task(ReadCompletionAction, this).RunSynchronously(TaskScheduler.Default);
            }

            static readonly Action<object> ReadCompletionAction = ReadCompletion;
            static void ReadCompletion(object ms)
            {
                var self = (MediationStream)ms;
                TaskCompletionSource<int> p = self._readCompletionSource;
                self._readCompletionSource = null;
                p.TrySetResult(self._readByteCount);
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (this.SourceReadableBytes > 0)
                {
                    // we have the bytes available upfront - write out synchronously
                    int read = ReadFromInput(buffer, offset, count);
                    return Task.FromResult(read);
                }

                Debug.Assert(_sslOwnedBuffer.Array == null);
                // take note of buffer - we will pass bytes there once available
                _sslOwnedBuffer = new ArraySegment<byte>(buffer, offset, count);
                _readCompletionSource = new TaskCompletionSource<int>();
                return _readCompletionSource.Task;
            }

            private int ReadFromInput(byte[] destination, int destinationOffset, int destinationCapacity)
            {
                Debug.Assert(destination != null);

                byte[] source = _input;
                int readableBytes = this.SourceReadableBytes;
                int length = Math.Min(readableBytes, destinationCapacity);
                Buffer.BlockCopy(source, _inputStartOffset + _inputOffset, destination, destinationOffset, length);
                _inputOffset += length;
                return length;
            }

            public override void Write(byte[] buffer, int offset, int count) => _owner.FinishWrap(buffer, offset, count, _owner.CapturedContext.NewPromise());

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => _owner.FinishWrapNonAppDataAsync(buffer, offset, count, _owner.CapturedContext.NewPromise());
        }
    }
}
#endif