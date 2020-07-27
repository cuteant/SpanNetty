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

namespace DotNetty.Codecs.Protobuf
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Google.Protobuf;

    public class ProtobufEncoder : MessageToMessageEncoder<IMessage>
    {
        private const int DefaultInitialCapacity = 1024 * 2;

        private readonly int _initialCapacity;

        public ProtobufEncoder() => _initialCapacity = DefaultInitialCapacity;

        public ProtobufEncoder(int initialCapacity)
        {
            if ((uint)(initialCapacity - 1) > SharedConstants.TooBigOrNegative)
            {
                initialCapacity = DefaultInitialCapacity;
            }
            _initialCapacity = initialCapacity;
        }

        public override bool IsSharable => true;

        protected override void Encode(IChannelHandlerContext context, IMessage message, List<object> output)
        {
            Debug.Assert(context != null);
            Debug.Assert(message != null);
            Debug.Assert(output != null);

            IByteBuffer buffer = null;
            try
            {
                int size = message.CalculateSize();
                if (0u >= (uint)size) { return; }

                buffer = context.Allocator.Buffer(_initialCapacity);
                using (var input = new ByteBufferStream(buffer, true, false))
                {
                    message.WriteTo(input);
                    input.Flush();
                }
                output.Add(buffer);
                buffer = null;
            }
            catch (Exception exception)
            {
                ThrowCodecException(exception);
            }
            finally
            {
                buffer?.Release();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowCodecException(Exception exc)
        {
            throw GetException();
            CodecException GetException()
            {
                return new CodecException(exc);
            }
        }
    }
}