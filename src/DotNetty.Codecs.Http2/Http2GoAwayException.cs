// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Thrown by <see cref="StreamBufferingEncoder"/> if buffered streams are terminated due to
    /// receipt of a <c>GOAWAY</c>.
    /// </summary>
    public sealed class Http2GoAwayException : Http2Exception
    {
        public Http2GoAwayException(int lastStreamId, Http2Error errorCode, byte[] debugData)
            : base(Http2Error.StreamClosed)
        {
            this.LastStreamId = lastStreamId;
            this.ErrorCode = errorCode;
            this.DebugData = debugData;
        }

        public int LastStreamId { get; }

        public Http2Error ErrorCode { get; }

        public byte[] DebugData { get; }
    }
}
