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
    using System.Net;
    using DotNetty.Buffers;

    public interface IReadCompletion : IDisposable
    {
        ReadableBuffer Data { get; }

        Exception Error { get; }
    }

    public interface IStreamReadCompletion : IReadCompletion
    {
        bool Completed { get; }
    }

    public interface IDatagramReadCompletion : IReadCompletion
    {
        IPEndPoint RemoteEndPoint { get; }
    }

    internal class ReadCompletion : IReadCompletion
    {
        private readonly ReadableBuffer _readableBuffer;
        private Exception _error;

        internal ReadCompletion(ref ReadableBuffer data, Exception error)
        {
            _readableBuffer = data;
            _error = error;
        }

        public ReadableBuffer Data => _readableBuffer;

        public Exception Error => _error;

        public void Dispose()
        {
            IByteBuffer buffer = Data.Buffer;
            if (buffer.IsAccessible)
            {
                buffer.Release();
            }
            _error = null;
        }
    }
}
