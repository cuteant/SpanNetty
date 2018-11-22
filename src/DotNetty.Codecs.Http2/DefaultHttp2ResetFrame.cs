// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    /// <summary>
    /// The default <see cref="IHttp2ResetFrame"/> implementation.
    /// </summary>
    public sealed class DefaultHttp2ResetFrame : AbstractHttp2StreamFrame, IHttp2ResetFrame
    {
        private readonly Http2Error errorCode;

        /// <summary>
        /// Construct a reset message.
        /// </summary>
        /// <param name="error">the reason for reset</param>
        public DefaultHttp2ResetFrame(Http2Error error)
        {
            this.errorCode = error;
        }

        public override string Name => "RST_STREAM";

        public Http2Error ErrorCode => this.errorCode;

        protected override bool Equals0(IHttp2StreamFrame other)
        {
            return other is DefaultHttp2ResetFrame resetFrame
                && base.Equals0(other)
                && this.errorCode == resetFrame.errorCode;
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            var errCode = (long)this.errorCode;
            hash = hash * 31 + (int)(errCode ^ (errCode.RightUShift(32)));
            return hash;
        }

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(stream=" + this.Stream + ", errorCode=" + this.errorCode + ')';
        }
    }
}
