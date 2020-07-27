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
        private IHttp2Stream v_stream;

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
            _ = stream.SetProperty(streamKey, this);
            InternalStream = stream;
            return this;
        }

        internal IHttp2Stream InternalStream
        {
            get => Volatile.Read(ref v_stream);
            set => Interlocked.Exchange(ref v_stream, value);
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
