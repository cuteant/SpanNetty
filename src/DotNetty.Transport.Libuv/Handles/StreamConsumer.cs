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

namespace DotNetty.Transport.Libuv.Handles
{
    using System;
    using DotNetty.Buffers;

    internal sealed class StreamConsumer<T> : IStreamConsumer<T>
        where T : IInternalStreamHandle
    {
        private static readonly Action<T> s_onCompleted = s => OnCompleted(s);
        private static readonly Action<IScheduleHandle> s_onClosed = s => OnClosed(s);

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
            _onCompleted = onCompleted ?? s_onCompleted;
        }

        public void Consume(T stream, IStreamReadCompletion readCompletion)
        {
            try
            {
                var error = readCompletion.Error;
                if (error is null)
                {
                    _onAccept(stream, readCompletion.Data);
                }
                else
                {
                    _onError(stream, error);
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

        private static void OnCompleted(T stream) => stream.CloseHandle(s_onClosed);

        private static void OnClosed(IScheduleHandle streamHandle) => streamHandle.Dispose();
    }
}
