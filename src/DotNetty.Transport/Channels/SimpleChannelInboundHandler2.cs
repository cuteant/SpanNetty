﻿/*
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
 * Copyright (c) The DotNetty Project (Microsoft). All rights reserved.
 *
 *   https://github.com/azure/dotnetty
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Transport.Channels
{
    using DotNetty.Common.Utilities;

    public abstract class SimpleChannelInboundHandler2<I> : ChannelHandlerAdapter
        where I : class
    {
        readonly bool _autoRelease;

        protected SimpleChannelInboundHandler2() : this(true)
        {
        }

        protected SimpleChannelInboundHandler2(bool autoRelease)
        {
            _autoRelease = autoRelease;
        }

        public virtual bool TryAcceptInboundMessage(object msg, out I imsg)
        {
            imsg = msg as I;
            return imsg is object;
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            bool release = true;
            try
            {
                if (TryAcceptInboundMessage(msg, out I imsg))
                {
                    ChannelRead0(ctx, imsg);
                }
                else
                {
                    release = false;
                    _ = ctx.FireChannelRead(msg);
                }
            }
            finally
            {
                if (_autoRelease && release)
                {
                    _ = ReferenceCountUtil.Release(msg);
                }
            }
        }

        protected abstract void ChannelRead0(IChannelHandlerContext ctx, I msg);
    }
}
