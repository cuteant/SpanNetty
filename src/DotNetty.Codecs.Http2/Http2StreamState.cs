// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// The allowed states of an HTTP2 stream.
    /// </summary>
    public enum Http2StreamState
    {
        /// <summary>
        /// None
        /// </summary>
        None,

        /// <summary>
        /// LocalSideOpen: <c>false</c> RemoteSideOpen: <c>false</c>
        /// </summary>
        Idle,

        /// <summary>
        /// LocalSideOpen: <c>false</c> RemoteSideOpen: <c>false</c>
        /// </summary>
        ReservedLocal,

        /// <summary>
        /// LocalSideOpen: <c>false</c> RemoteSideOpen: <c>false</c>
        /// </summary>
        ReservedRemote,

        /// <summary>
        /// LocalSideOpen: <c>true</c> RemoteSideOpen: <c>true</c>
        /// </summary>
        Open,

        /// <summary>
        /// LocalSideOpen: <c>false</c> RemoteSideOpen: <c>true</c>
        /// </summary>
        HalfClosedLocal,

        /// <summary>
        /// LocalSideOpen: <c>true</c> RemoteSideOpen: <c>false</c>
        /// </summary>
        HalfClosedRemote,

        /// <summary>
        /// LocalSideOpen: <c>false</c> RemoteSideOpen: <c>false</c>
        /// </summary>
        Closed,
    }

    public static class Http2StreamStateExtensions
    {
        /// <summary>
        /// Indicates whether the local side of this stream is open (i.e. the state is either
        /// <see cref="Http2StreamState.Open"/> or <see cref="Http2StreamState.HalfClosedRemote"/>.
        /// </summary>
        public static bool LocalSideOpen(this Http2StreamState state)
        {
            return state switch
            {
                Http2StreamState.Idle => false,
                Http2StreamState.ReservedLocal => false,
                Http2StreamState.ReservedRemote => false,
                Http2StreamState.Open => true,
                Http2StreamState.HalfClosedLocal => false,
                Http2StreamState.HalfClosedRemote => true,
                Http2StreamState.Closed => false,
                _ => throw ThrowHelper.GetNotSupportedException(),
            };
        }

        /// <summary>
        /// Indicates whether the remote side of this stream is open (i.e. the state is either
        /// <see cref="Http2StreamState.Open"/> or <see cref="Http2StreamState.HalfClosedLocal"/>.
        /// </summary>
        public static bool RemoteSideOpen(this Http2StreamState state)
        {
            return state switch
            {
                Http2StreamState.Idle => false,
                Http2StreamState.ReservedLocal => false,
                Http2StreamState.ReservedRemote => false,
                Http2StreamState.Open => true,
                Http2StreamState.HalfClosedLocal => true,
                Http2StreamState.HalfClosedRemote => false,
                Http2StreamState.Closed => false,
                _ => throw ThrowHelper.GetNotSupportedException(),
            };
        }
    }
}