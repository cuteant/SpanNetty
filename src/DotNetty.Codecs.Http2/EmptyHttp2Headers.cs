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
    using DotNetty.Codecs;
    using DotNetty.Common.Utilities;

    public sealed class EmptyHttp2Headers : EmptyHeaders<ICharSequence, ICharSequence>, IHttp2Headers
    {
        public static readonly EmptyHttp2Headers Instance = new EmptyHttp2Headers();

        private EmptyHttp2Headers() { }

        public ICharSequence Method
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public ICharSequence Scheme
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public ICharSequence Authority
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public ICharSequence Path
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public ICharSequence Status
        {
            get => null;
            set => throw new NotSupportedException();
        }

        public bool Contains(ICharSequence name, ICharSequence value, bool caseInsensitive)
        {
            return false;
        }
    }
}
