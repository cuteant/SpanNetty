// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using DotNetty.Buffers;

    public sealed class DefaultMessageSizeEstimator : IMessageSizeEstimator
    {
        sealed class HandleImpl : IMessageSizeEstimatorHandle
        {
            readonly int unknownSize;

            public HandleImpl(int unknownSize)
            {
                this.unknownSize = unknownSize;
            }

            public int Size(object msg)
            {
                switch (msg)
                {
                    case IByteBuffer byteBuffer:
                        return byteBuffer.ReadableBytes;

                    case IByteBufferHolder byteBufferHolder:
                        return byteBufferHolder.Content.ReadableBytes;

                    case IFileRegion fileRegion:
                        return 0;

                    default:
                        return this.unknownSize;
                }
            }
        }

        /// <summary>
        /// Returns the default implementation, which returns <c>8</c> for unknown messages.
        /// </summary>
        public static readonly IMessageSizeEstimator Default = new DefaultMessageSizeEstimator(8);

        readonly IMessageSizeEstimatorHandle handle;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="unknownSize">The size which is returned for unknown messages.</param>
        public DefaultMessageSizeEstimator(int unknownSize)
        {
            if (unknownSize < 0) { ThrowHelper.ThrowArgumentException_PositiveOrZero(unknownSize, ExceptionArgument.unknownSize); }
            this.handle = new HandleImpl(unknownSize);
        }

        public IMessageSizeEstimatorHandle NewHandle() => this.handle;
    }
}