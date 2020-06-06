// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System;

    public sealed class TlsHandshakeCompletionEvent
    {
        public static readonly TlsHandshakeCompletionEvent Success = new TlsHandshakeCompletionEvent();

        readonly Exception _exception;

        /// <summary>
        ///     Creates a new event that indicates a successful handshake.
        /// </summary>
        TlsHandshakeCompletionEvent()
        {
            _exception = null;
        }

        /// <summary>
        ///     Creates a new event that indicates an unsuccessful handshake.
        ///     Use {@link #SUCCESS} to indicate a successful handshake.
        /// </summary>
        public TlsHandshakeCompletionEvent(Exception exception)
        {
            if (exception is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.exception); }
            _exception = exception;
        }

        /// <summary>
        ///     Return <c>true</c> if the handshake was successful
        /// </summary>
        public bool IsSuccessful => _exception is null;

        /// <summary>
        ///     Return the {@link Throwable} if {@link #isSuccess()} returns <c>false</c>
        ///     and so the handshake failed.
        /// </summary>
        public Exception Exception => _exception;

        public override string ToString()
        {
            Exception ex = Exception;
            return ex is null ? "TlsHandshakeCompletionEvent(SUCCESS)" : $"TlsHandshakeCompletionEvent({ex})";
        }
    }
}