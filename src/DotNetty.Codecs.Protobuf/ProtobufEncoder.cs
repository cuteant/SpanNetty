// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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