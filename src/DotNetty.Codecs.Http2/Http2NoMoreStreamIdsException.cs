// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    /// <summary>
    /// This exception is thrown when there are no more stream IDs available for the current connection
    /// </summary>
    public class Http2NoMoreStreamIdsException : Http2Exception
    {
        private const string ErrorMessage = "No more streams can be created on this connection";

        public Http2NoMoreStreamIdsException()
            : base(Http2Error.ProtocolError, ErrorMessage, ShutdownHint.GracefulShutdown)
        {
        }

        public Http2NoMoreStreamIdsException(Exception cause)
            : base(Http2Error.ProtocolError, ErrorMessage, cause, ShutdownHint.GracefulShutdown)
        {
        }
    }
}
