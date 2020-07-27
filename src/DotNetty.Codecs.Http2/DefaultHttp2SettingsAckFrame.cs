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

using DotNetty.Common.Utilities;

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// The default <see cref="IHttp2SettingsAckFrame"/> implementation.
    /// </summary>
    public sealed class DefaultHttp2SettingsAckFrame : IHttp2SettingsAckFrame
    {
        public static readonly DefaultHttp2SettingsAckFrame Instance = new DefaultHttp2SettingsAckFrame();

        private DefaultHttp2SettingsAckFrame() { }

        public string Name => "SETTINGS(ACK)";

        public override string ToString() => StringUtil.SimpleClassName<DefaultHttp2SettingsAckFrame>();
    }
}
