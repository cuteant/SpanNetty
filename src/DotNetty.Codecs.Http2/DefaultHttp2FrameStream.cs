// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// <see cref="IHttp2FrameStream"/> implementation.
    /// </summary>
    // TODO(buchgr): Merge Http2FrameStream and Http2Stream.
    public class DefaultHttp2FrameStream : IHttp2FrameStream
    {
        private int v_id = -1;
        private IHttp2Stream _stream;

        internal readonly Http2FrameStreamEvent StateChanged;
        internal readonly Http2FrameStreamEvent WritabilityChanged;

        internal IChannel Attachment;

        public DefaultHttp2FrameStream()
        {
            StateChanged = Http2FrameStreamEvent.StateChanged(this);
            WritabilityChanged = Http2FrameStreamEvent.WritabilityChanged(this);
        }

        public DefaultHttp2FrameStream SetStreamAndProperty(IHttp2ConnectionPropertyKey streamKey, IHttp2Stream stream)
        {
            Debug.Assert(v_id == -1 || stream.Id == v_id);
            stream.SetProperty(streamKey, this);
            InternalStream = stream;
            return this;
        }

        internal IHttp2Stream InternalStream
        {
            get => Volatile.Read(ref _stream);
            set => Interlocked.Exchange(ref _stream, value);
        }

        public virtual int Id
        {
            get
            {
                var stream = InternalStream;
                return stream is null ? Volatile.Read(ref v_id) : stream.Id;
            }
            set => Interlocked.Exchange(ref v_id, value);
        }

        public virtual Http2StreamState State
        {
            get
            {
                var stream = InternalStream;
                return stream is null ? Http2StreamState.Idle : stream.State;
            }
        }

        public bool Equals(IHttp2FrameStream other) => ReferenceEquals(this, other);

        public override string ToString()
        {
            return Id.ToString(CultureInfo.InvariantCulture);
        }
    }
}
