/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

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