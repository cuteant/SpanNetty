namespace DotNetty.Codecs.Http2
{
    partial class AbstractHttp2StreamChannel
    {
        /// <summary>
        /// The current status of the read-processing for a <see cref="AbstractHttp2StreamChannel"/>.
        /// </summary>
        enum ReadStatus
        {
            /// <summary>
            /// No read in progress and no read was requested (yet)
            /// </summary>
            Idle,

            /// <summary>
            /// Reading in progress
            /// </summary>
            InProgress,

            /// <summary>
            /// A read operation was requested.
            /// </summary>
            Requested
        }
    }
}
