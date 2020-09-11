/*
 * Copyright (c) Johnny Z. All rights reserved.
 *
 *   https://github.com/StormHub/NetUV
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com)
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.NetUV.Channels
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.NetUV.Handles;

    internal sealed class StreamConsumer<T> : IStreamConsumer<T>
        where T : StreamHandle
    {
        private readonly Action<T, ReadableBuffer> _onAccept;
        private readonly Action<T, Exception> _onError;
        private readonly Action<T> _onCompleted;

        public StreamConsumer(
            Action<T, ReadableBuffer> onAccept,
            Action<T, Exception> onError,
            Action<T> onCompleted)
        {
            if (onAccept is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onAccept); }
            if (onError is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.onError); }

            _onAccept = onAccept;
            _onError = onError;
            _onCompleted = onCompleted ?? OnCompleted;
        }

        public void Consume(T stream, IStreamReadCompletion readCompletion)
        {
            try
            {
                if (readCompletion.Error is object)
                {
                    _onError(stream, readCompletion.Error);
                }
                else
                {
                    _onAccept(stream, readCompletion.Data);
                }

                if (readCompletion.Completed)
                {
                    _onCompleted(stream);
                }
            }
            catch (Exception exception)
            {
                _onError(stream, exception);
            }
        }

        private static void OnCompleted(T stream) => stream.CloseHandle(OnClosed);

        private static void OnClosed(StreamHandle streamHandle) => streamHandle.Dispose();
    }
}
