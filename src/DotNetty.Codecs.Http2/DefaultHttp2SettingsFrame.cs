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
    using DotNetty.Common.Utilities;

    /// <summary>
    /// The default <see cref="IHttp2SettingsFrame"/> implementation.
    /// </summary>
    public class DefaultHttp2SettingsFrame : IHttp2SettingsFrame, IEquatable<DefaultHttp2SettingsFrame>
    {
        private readonly Http2Settings _settings;

        public DefaultHttp2SettingsFrame(Http2Settings settings)
        {
            if (settings is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.settings); }
            _settings = settings;
        }

        public Http2Settings Settings => _settings;

        public string Name => "SETTINGS";

        public override bool Equals(object obj)
        {
            return Equals(obj as DefaultHttp2SettingsFrame);
        }

        public bool Equals(DefaultHttp2SettingsFrame other)
        {
            return ReferenceEquals(this, other);
        }

        public override int GetHashCode() => _settings.GetHashCode();

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(settings=" + _settings + ')';
        }
    }
}
