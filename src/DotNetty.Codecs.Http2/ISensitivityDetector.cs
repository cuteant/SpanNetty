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
    using DotNetty.Common.Utilities;

    /// <summary>
    /// Determine if a header name/value pair is treated as
    /// <a href="https://tools.ietf.org/html/rfc7541#section-7.1.3">sensitive</a>.
    /// If the object can be dynamically modified and shared across multiple connections it may need to be thread safe.
    /// </summary>
    public interface ISensitivityDetector
    {
        /// <summary>
        /// Determine if a header <paramref name="name"/>/<paramref name="value"/> pair should be treated as
        /// <a href="https://tools.ietf.org/html/rfc7541#section-7.1.3">sensitive</a>.
        /// </summary>
        /// <param name="name">The name for the header.</param>
        /// <param name="value">The value of the header.</param>
        /// <returns><c>true</c> if a header <paramref name="name"/>/<paramref name="value"/> pair should be treated as
        /// <a href="https://tools.ietf.org/html/rfc7541#section-7.1.3">sensitive</a>.
        /// <c>false</c> otherwise.</returns>
        bool IsSensitive(ICharSequence name, ICharSequence value);
    }
}