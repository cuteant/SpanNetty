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
    /// Provides a hint as to if shutdown is justified, what type of shutdown should be executed.
    /// </summary>
    public enum ShutdownHint
    {
        /// <summary>
        /// Do not shutdown the underlying channel.
        /// </summary>
        NoShutdown,

        /// <summary>
        /// Attempt to execute a "graceful" shutdown. The definition of "graceful" is left to the implementation.
        /// An example of "graceful" would be wait for some amount of time until all active streams are closed.
        /// </summary>
        GracefulShutdown,

        /// <summary>
        /// Close the channel immediately after a <c>GOAWAY</c> is sent.
        /// </summary>
        HardShutdown
    }
}