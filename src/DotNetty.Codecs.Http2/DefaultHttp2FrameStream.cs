// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;

    /// <summary>
    /// <see cref="IHttp2FrameStream"/> implementation.
    /// </summary>
    // TODO(buchgr): Merge Http2FrameStream and Http2Stream.
    internal class DefaultHttp2FrameStream : IHttp2FrameStream
    {
        private int id = -1;
        private IHttp2Stream stream;

        public DefaultHttp2FrameStream SetStreamAndProperty(IHttp2ConnectionPropertyKey streamKey, IHttp2Stream stream)
        {
            Debug.Assert(id == -1 || stream.Id == id);
            stream.SetProperty(streamKey, this);
            this.InternalStream = stream;
            return this;
        }

        internal IHttp2Stream InternalStream
        {
            get => Volatile.Read(ref this.stream);
            set => Interlocked.Exchange(ref this.stream, value);
        }

        public virtual int Id
        {
            get
            {
                var stream = this.InternalStream;
                return stream is null ? Volatile.Read(ref this.id) : stream.Id;
            }
            set => Interlocked.Exchange(ref this.id, value);
        }

        public virtual Http2StreamState State
        {
            get
            {
                var stream = this.InternalStream;
                return stream is null ? Http2StreamState.Idle : stream.State;
            }
        }

        public bool Equals(IHttp2FrameStream other) => ReferenceEquals(this, other);

        public override string ToString()
        {
            return this.Id.ToString(CultureInfo.InvariantCulture);
        }
    }
}
