
namespace DotNetty.Handlers.Tls
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    partial class TlsHandler
    {
        private sealed partial class MediationStream : Stream
        {
            private readonly TlsHandler _owner;
            private int _inputOffset;
            private int _inputLength;
            private TaskCompletionSource<int> _readCompletionSource;

            public MediationStream(TlsHandler owner)
            {
                _owner = owner;
            }

            public int SourceReadableBytes => _inputLength - _inputOffset;

            public override void Flush()
            {
                // NOOP: called on SslStream.Close
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing)
                {
                    TaskCompletionSource<int> p = _readCompletionSource;
                    if (p is object)
                    {
                        _readCompletionSource = null;
                        _ = p.TrySetResult(0);
                    }
                }
            }

            #region plumbing

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length
            {
                get { throw new NotSupportedException(); }
            }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            #endregion

            #region sync result

            private sealed class SynchronousAsyncResult<T> : IAsyncResult
            {
                public T Result { get; set; }

                public bool IsCompleted => true;

                public WaitHandle AsyncWaitHandle
                {
                    get { throw new InvalidOperationException("Cannot wait on a synchronous result."); }
                }

                public object AsyncState { get; set; }

                public bool CompletedSynchronously => true;
            }

            #endregion
        }
    }
}
