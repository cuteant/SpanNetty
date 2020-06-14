// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    /// <summary>
    ///     The <see cref="IRecvByteBufAllocator" /> that always yields the same buffer
    ///     size prediction. This predictor ignores the feedback from the I/O thread.
    /// </summary>
    public sealed class FixedRecvByteBufAllocator : DefaultMaxMessagesRecvByteBufAllocator
    {
        public static readonly FixedRecvByteBufAllocator Default = new FixedRecvByteBufAllocator(4 * 1024);

        sealed class HandleImpl : MaxMessageHandle<FixedRecvByteBufAllocator>
        {
            readonly int _bufferSize;

            public HandleImpl(FixedRecvByteBufAllocator owner, int bufferSize)
                : base(owner)
            {
                _bufferSize = bufferSize;
            }

            public override int Guess() => _bufferSize;
        }

        readonly int _bufferSize;

        /// <summary>
        ///     Creates a new predictor that always returns the same prediction of
        ///     the specified buffer size.
        /// </summary>
        public FixedRecvByteBufAllocator(int bufferSize)
        {
            if ((uint)(bufferSize - 1) > SharedConstants.TooBigOrNegative) // <= 0
            {
                ThrowHelper.ThrowArgumentException_Positive(bufferSize, ExceptionArgument.bufferSize);
            }

            _bufferSize = bufferSize;
        }

        public override IRecvByteBufAllocatorHandle NewHandle() => new HandleImpl(this, _bufferSize);
    }
}