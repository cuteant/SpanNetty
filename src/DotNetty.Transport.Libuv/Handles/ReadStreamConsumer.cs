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

    internal sealed class ReadStreamConsumer<T> : IStreamConsumer<T>
        where T : IInternalStreamHandle
    {
        private readonly Action<T, IStreamReadCompletion> _readAction;

        public ReadStreamConsumer(Action<T, IStreamReadCompletion> readAction)
        {
            if (readAction is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.readAction); }

            _readAction = readAction;
        }

        public void Consume(T stream, IStreamReadCompletion readCompletion) =>
            _readAction(stream, readCompletion);
    }
}
