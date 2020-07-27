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
    using System;
    using System.Threading;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// A <see cref="ChannelDuplexHandler"/> providing additional functionality for HTTP/2. Specifically it allows to:
    /// 
    /// <para>Create new outbound streams using <see cref="NewStream"/>.</para>
    /// <para>Iterate over all active streams using <see cref="ForEachActiveStream(IHttp2FrameStreamVisitor)"/>.</para>
    /// 
    /// <para>The <see cref="Http2FrameCodec"/> is required to be part of the <see cref="IChannelPipeline"/> before this handler is added,
    /// or else an <see cref="InvalidOperationException"/> will be thrown.</para>
    /// </summary>
    public abstract class Http2ChannelDuplexHandler : ChannelDuplexHandler
    {
        private Http2FrameCodec v_frameCodec;

        private Http2FrameCodec InternalframeCodec
        {
            get => Volatile.Read(ref v_frameCodec);
            set => Interlocked.Exchange(ref v_frameCodec, value);
        }

        public sealed override void HandlerAdded(IChannelHandlerContext ctx)
        {
            InternalframeCodec = RequireHttp2FrameCodec(ctx);
            HandlerAdded0(ctx);
        }

        protected virtual void HandlerAdded0(IChannelHandlerContext ctx)
        {
            // NOOP
        }

        public sealed override void HandlerRemoved(IChannelHandlerContext ctx)
        {
            try
            {
                HandlerRemoved0(ctx);
            }
            finally
            {
                InternalframeCodec = null;
            }
        }

        protected virtual void HandlerRemoved0(IChannelHandlerContext ctx)
        {
            // NOOP
        }

        /// <summary>
        /// Creates a new <see cref="IHttp2FrameStream"/> object.
        /// <para>This method is <c>thread-safe</c>.</para>
        /// </summary>
        public IHttp2FrameStream NewStream()
        {
            Http2FrameCodec codec = InternalframeCodec;
            if (codec is null)
            {
                ThrowHelper.ThrowInvalidOperationException_RequireHttp2FrameCodec();
            }
            return codec.NewStream();
        }

        /// <summary>
        /// Allows to iterate over all currently active streams.
        /// <para>This method may only be called from the eventloop thread.</para>
        /// </summary>
        protected void ForEachActiveStream(IHttp2FrameStreamVisitor streamVisitor)
        {
            InternalframeCodec.ForEachActiveStream(streamVisitor);
        }

        protected void ForEachActiveStream(Func<IHttp2FrameStream, bool> streamVisitor)
        {
            InternalframeCodec.ForEachActiveStream(streamVisitor);
        }

        private static Http2FrameCodec RequireHttp2FrameCodec(IChannelHandlerContext ctx)
        {
            var frameCodecCtx = ctx.Pipeline.Context<Http2FrameCodec>();
            if (frameCodecCtx is null)
            {
                ThrowHelper.ThrowArgumentException_RequireHttp2FrameCodec();
            }
            return (Http2FrameCodec)frameCodecCtx.Handler;
        }
    }
}
