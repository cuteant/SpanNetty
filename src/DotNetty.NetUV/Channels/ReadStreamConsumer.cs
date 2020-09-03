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
    using DotNetty.NetUV.Handles;

    internal sealed class ReadStreamConsumer<T> : IStreamConsumer<T>
        where T : StreamHandle
    {
        private readonly Action<T, IStreamReadCompletion> readAction;

        public ReadStreamConsumer(Action<T, IStreamReadCompletion> readAction)
        {
            if (readAction is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.readAction); }

            this.readAction = readAction;
        }

        public void Consume(T stream, IStreamReadCompletion readCompletion) =>
            this.readAction(stream, readCompletion);
    }
}
