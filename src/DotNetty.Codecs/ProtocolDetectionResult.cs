namespace DotNetty.Codecs
{
    using DotNetty.Buffers;

    /// <summary>
    /// Result of detecting a protocol.
    /// </summary>
    /// <typeparam name="T">the type of the protocol</typeparam>
    public sealed class ProtocolDetectionResult<T> where T : class
    {
        /// <summary>
        /// Returns a <see cref="ProtocolDetectionResult{T}"/> that signals that more data is needed to detect the protocol.
        /// </summary>
        public static readonly ProtocolDetectionResult<T> NeedsMoreData;

        /// <summary>
        /// Returns a <see cref="ProtocolDetectionResult{T}"/> that signals the data was invalid for the protocol.
        /// </summary>
        public static readonly ProtocolDetectionResult<T> Invalid;

        static ProtocolDetectionResult()
        {
            NeedsMoreData = new ProtocolDetectionResult<T>(ProtocolDetectionState.NeedsMoreData, default);
            Invalid = new ProtocolDetectionResult<T>(ProtocolDetectionState.Invalid, default);
        }

        private ProtocolDetectionResult(ProtocolDetectionState state, T result)
        {
            State = state;
            DetectedProtocol = result;
        }

        /// <summary>
        /// Returns a <see cref="ProtocolDetectionResult{T}"/> which holds the detected protocol.
        /// </summary>
        public static ProtocolDetectionResult<T> Detected(T protocol)
        {
            if (protocol is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.protocol); }

            return new ProtocolDetectionResult<T>(ProtocolDetectionState.Detected, protocol);
        }

        /// <summary>
        /// Return the <see cref="ProtocolDetectionState"/>. If the state is <see cref="ProtocolDetectionState.Detected"/> you
        /// can retrieve the protocol via <see cref="DetectedProtocol"/>.
        /// </summary>
        public ProtocolDetectionState State { get; }

        /// <summary>
        /// Returns the protocol if <see cref="State"/> returns <see cref="ProtocolDetectionState.Detected"/>, otherwise <c>null</c>.
        /// </summary>
        public T DetectedProtocol { get; }
    }
}
