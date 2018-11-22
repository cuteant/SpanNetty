// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>The allowed states of an HTTP2 stream.</summary>
    public sealed class Http2StreamState : IEquatable<Http2StreamState>
    {
        public static readonly Http2StreamState Idle = new Http2StreamState(false, false);
        public static readonly Http2StreamState ReservedLocal = new Http2StreamState(false, false);
        public static readonly Http2StreamState ReservedRemote = new Http2StreamState(false, false);
        public static readonly Http2StreamState Open = new Http2StreamState(true, true);
        public static readonly Http2StreamState HalfClosedLocal = new Http2StreamState(false, true);
        public static readonly Http2StreamState HalfClosedRemote = new Http2StreamState(true, false);
        public static readonly Http2StreamState Closed = new Http2StreamState(false, false);

        /// <summary>
        /// Indicates whether the local side of this stream is open (i.e. the state is either
        /// <see cref="Open"/> or <see cref="HalfClosedRemote"/>.
        /// </summary>
        public readonly bool LocalSideOpen;

        /// <summary>
        /// Indicates whether the remote side of this stream is open (i.e. the state is either
        /// <see cref="Open"/> or <see cref="HalfClosedLocal"/>.
        /// </summary>
        public readonly bool RemoteSideOpen;

        Http2StreamState(bool localSideOpen, bool remoteSideOpen)
        {
            this.LocalSideOpen = localSideOpen;
            this.RemoteSideOpen = remoteSideOpen;
        }

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

        public override bool Equals(object obj) => ReferenceEquals(this, obj);

        public bool Equals(Http2StreamState other) => ReferenceEquals(this, other);

        public override string ToString()
        {
            if (Idle == this) { return "Idle"; }
            if (ReservedLocal == this) { return "ReservedLocal"; }
            if (ReservedRemote == this) { return "ReservedRemote"; }
            if (Open == this) { return "Open"; }
            if (HalfClosedLocal == this) { return "HalfClosedLocal"; }
            if (HalfClosedRemote == this) { return "HalfClosedRemote"; }
            return "Closed";
        }
    }
}