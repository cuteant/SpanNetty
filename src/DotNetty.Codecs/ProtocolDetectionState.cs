namespace DotNetty.Codecs
{
    /// <summary>
    /// The state of the current detection.
    /// </summary>
    public enum ProtocolDetectionState
    {
        /// <summary>
        /// Need more data to detect the protocol.
        /// </summary>
        NeedsMoreData,

        /// <summary>
        /// The data was invalid.
        /// </summary>
        Invalid,

        /// <summary>
        /// Protocol was detected.
        /// </summary>
        Detected
    }
}
