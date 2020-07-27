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
    /// Listener to the number of flow-controlled bytes written per stream.
    /// </summary>
    public interface IHttp2RemoteFlowControllerListener
    {
        /// <summary>
        /// Notification that <see cref="IHttp2RemoteFlowController.IsWritable(IHttp2Stream)"/> has changed for <paramref name="stream"/>.
        /// This method should not throw. Any thrown exceptions are considered a programming error and are ignored.
        /// </summary>
        /// <param name="stream">The stream which writability has changed for.</param>
        void WritabilityChanged(IHttp2Stream stream);
    }
}
