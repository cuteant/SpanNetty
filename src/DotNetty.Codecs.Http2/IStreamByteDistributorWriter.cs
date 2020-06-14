// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    /// <summary>
    /// Object that performs the writing of the bytes that have been allocated for a stream.
    /// </summary>
    public interface IStreamByteDistributorWriter
    {
        /// <summary>
        /// Writes the allocated bytes for this stream.
        /// <para>Any <see cref="System.Exception"/> thrown from this method is considered a programming error.
        /// A <c>GOAWAY</c> frame will be sent and the will be connection closed.</para>
        /// </summary>
        /// <param name="stream">the stream for which to perform the write.</param>
        /// <param name="numBytes">the number of bytes to write.</param>
        void Write(IHttp2Stream stream, int numBytes);
    }

    public sealed class ActionStreamByteDistributorWriter : IStreamByteDistributorWriter
    {
        private readonly Action<IHttp2Stream, int> _writeAction;

        public ActionStreamByteDistributorWriter(Action<IHttp2Stream, int> writeAction)
        {
            if (writeAction is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.writeAction); }
            _writeAction = writeAction;
        }

        public void Write(IHttp2Stream stream, int numBytes) => _writeAction(stream, numBytes);
    }
}
