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

namespace DotNetty.Transport.Libuv
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Libuv.Handles;
    using DotNetty.Transport.Channels;

    public sealed class UVEventLoopGroup : MultithreadEventLoopGroup<UVEventLoopGroup, UVEventLoop>
    {
        private static readonly int DefaultEventLoopCount;
        private static readonly Func<UVEventLoopGroup, UVEventLoop> DefaultEventLoopFactory;

        static UVEventLoopGroup()
        {
            DefaultEventLoopCount = Environment.ProcessorCount;
            DefaultEventLoopFactory = group => new UVEventLoop(group);
        }


        public UVEventLoopGroup()
            : this(0)
        {
        }

        public UVEventLoopGroup(int nThreads)
            : base(0u >= (uint)nThreads ? DefaultEventLoopCount : nThreads, UVEventLoopChooserFactory<UVEventLoop>.Instance, DefaultEventLoopFactory)
        {
        }

        public UVEventLoopGroup(int nThreads, TimeSpan breakoutInterval)
            : this(nThreads, DefaultThreadFactory<UVEventLoop>.Instance, RejectedExecutionHandlers.Reject(), breakoutInterval)
        {
        }

        public UVEventLoopGroup(int nThreads, IRejectedExecutionHandler rejectedHandler)
            : this(nThreads, rejectedHandler, AbstractUVEventLoop.DefaultBreakoutInterval)
        {
        }

        public UVEventLoopGroup(int nThreads, IRejectedExecutionHandler rejectedHandler, TimeSpan breakoutInterval)
            : this(nThreads, DefaultThreadFactory<UVEventLoop>.Instance, rejectedHandler, breakoutInterval)
        {
        }

        public UVEventLoopGroup(int nThreads, IThreadFactory threadFactory, TimeSpan breakoutInterval)
            : this(nThreads, threadFactory, RejectedExecutionHandlers.Reject(), breakoutInterval)
        {
        }

        public UVEventLoopGroup(int nThreads, IThreadFactory threadFactory, IRejectedExecutionHandler rejectedHandler, TimeSpan breakoutInterval)
            : base(0u >= (uint)nThreads ? DefaultEventLoopCount : nThreads,
                  UVEventLoopChooserFactory<UVEventLoop>.Instance,
                  group => new UVEventLoop(group, threadFactory, rejectedHandler, breakoutInterval))
        {
        }

        public override Task RegisterAsync(IChannel channel)
        {
            if (channel is not INativeChannel nativeChannel)
            {
                return ThrowHelper.FromArgumentException_RegChannel();
            }

            // The handle loop must be the same as the loop of the
            // handle was created from.
            var handle = nativeChannel.GetHandle();
            var loopHandle = ((IInternalStreamHandle)handle).LoopHandle();
            var eventLoops = GetItems();
            for (int i = 0; i < eventLoops.Count; i++)
            {
                var eventLoop = eventLoops[i];
                if (eventLoop.UnsafeLoop.InternalHandle == loopHandle)
                {
                    return eventLoop.RegisterAsync(nativeChannel);
                }
            }

            return ThrowHelper.FromInvalidOperationException(loopHandle);
        }
    }
}