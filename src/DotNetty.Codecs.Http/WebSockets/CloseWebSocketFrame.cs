// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// Web Socket Frame for closing the connection.
    /// </summary>
    public class CloseWebSocketFrame : WebSocketFrame
    {
        /// <summary>
        /// Creates a new empty close frame.
        /// </summary>
        public CloseWebSocketFrame()
            : base(true, 0, Opcode.Close, ArrayPooled.Buffer(0))
        {
        }

        /// <summary>
        /// Creates a new empty close frame with closing status code and reason text
        /// </summary>
        /// <param name="status">Status code as per <a href="http://tools.ietf.org/html/rfc6455#section-7.4">RFC 6455</a>. For
        /// example, <tt>1000</tt> indicates normal closure.</param>
        public CloseWebSocketFrame(WebSocketCloseStatus status)
            : this(status.Code, status.ReasonText)
        {
        }

        /// <summary>
        /// Creates a new empty close frame with closing status code and reason text
        /// </summary>
        /// <param name="status">Status code as per <a href="http://tools.ietf.org/html/rfc6455#section-7.4">RFC 6455</a>. For
        /// example, <tt>1000</tt> indicates normal closure.</param>
        /// <param name="reasonText">Reason text. Set to null if no text.</param>
        public CloseWebSocketFrame(WebSocketCloseStatus status, ICharSequence reasonText)
            : this(status.Code, reasonText)
        {
        }

        /// <summary>
        /// Creates a new close frame with no losing status code and no reason text
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="rsv">reserved bits used for protocol extensions.</param>
        public CloseWebSocketFrame(bool finalFragment, int rsv)
            : base(finalFragment, rsv, Opcode.Close, ArrayPooled.Buffer(0))
        {
        }

        /// <summary>
        /// Creates a new close frame
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="rsv">reserved bits used for protocol extensions.</param>
        /// <param name="binaryData">the content of the frame. Must be 2 byte integer followed by optional UTF-8 encoded string.</param>
        public CloseWebSocketFrame(bool finalFragment, int rsv, IByteBuffer binaryData)
            : base(finalFragment, rsv, Opcode.Close, binaryData)
        {
        }

        /// <summary>
        /// Creates a new empty close frame with closing status code and reason text
        /// </summary>
        /// <param name="statusCode">Integer status code as per <a href="http://tools.ietf.org/html/rfc6455#section-7.4">RFC 6455</a>. For
        /// example, <tt>1000</tt> indicates normal closure.</param>
        /// <param name="reasonText">Reason text. Set to null if no text.</param>
        public CloseWebSocketFrame(int statusCode, ICharSequence reasonText)
            : this(true, 0, statusCode, reasonText)
        {
        }

        /// <summary>
        /// Creates a new close frame with closing status code and reason text
        /// </summary>
        /// <param name="finalFragment">flag indicating if this frame is the final fragment</param>
        /// <param name="rsv">reserved bits used for protocol extensions.</param>
        /// <param name="statusCode">Integer status code as per <a href="http://tools.ietf.org/html/rfc6455#section-7.4">RFC 6455</a>. For
        /// example, <tt>1000</tt> indicates normal closure.</param>
        /// <param name="reasonText">Reason text. Set to null if no text.</param>
        public CloseWebSocketFrame(bool finalFragment, int rsv, int statusCode, ICharSequence reasonText)
            : base(finalFragment, rsv, Opcode.Close, NewBinaryData(statusCode, reasonText))
        {
        }

        static IByteBuffer NewBinaryData(int statusCode, ICharSequence reasonText)
        {
            if (reasonText is null)
            {
                reasonText = StringCharSequence.Empty;
            }

            IByteBuffer binaryData = ArrayPooled.Buffer(2 + reasonText.Count);
            binaryData.WriteShort(statusCode);
            if ((uint)reasonText.Count > 0u)
            {
                binaryData.WriteCharSequence(reasonText, Encoding.UTF8);
            }

            binaryData.SetReaderIndex(0);
            return binaryData;
        }

        ///<summary>
        ///    Returns the closing status code as per http://tools.ietf.org/html/rfc6455#section-7.4 RFC 6455. 
        ///    If a status code is set, -1 is returned.
        /// </summary>
        public int StatusCode()
        {
            IByteBuffer binaryData = this.Content;
            if (binaryData is null || 0u >= (uint)binaryData.Capacity)
            {
                return -1;
            }

            binaryData.SetReaderIndex(0);
            return binaryData.GetShort(0);
        }

        ///<summary>
        ///     Returns the reason text as per http://tools.ietf.org/html/rfc6455#section-7.4 RFC 6455
        ///     If a reason text is not supplied, an empty string is returned.
        /// </summary>
        public ICharSequence ReasonText()
        {
            IByteBuffer binaryData = this.Content;
            if (binaryData is null || binaryData.Capacity <= 2)
            {
                return StringCharSequence.Empty;
            }

            binaryData.SetReaderIndex(2);
            string reasonText = binaryData.ToString(Encoding.UTF8);
            binaryData.SetReaderIndex(0);

            return new StringCharSequence(reasonText);
        }

        public override IByteBufferHolder Replace(IByteBuffer content) => new CloseWebSocketFrame(this.IsFinalFragment, this.Rsv, content);
    }
}
