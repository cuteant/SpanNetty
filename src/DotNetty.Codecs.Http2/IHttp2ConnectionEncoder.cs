// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Handler for outbound HTTP/2 traffic.
    /// </summary>
    public interface IHttp2ConnectionEncoder : IHttp2FrameWriter
    {
        /// <summary>
        /// Sets the lifecycle manager. Must be called as part of initialization before the encoder is used.
        /// </summary>
        /// <param name="lifecycleManager"></param>
        void LifecycleManager(IHttp2LifecycleManager lifecycleManager);

        /// <summary>
        /// Provides direct access to the underlying connection.
        /// </summary>
        IHttp2Connection Connection { get; }

        /// <summary>
        /// Provides the remote flow controller for managing outbound traffic.
        /// </summary>
        IHttp2RemoteFlowController FlowController { get; }

        /// <summary>
        /// Provides direct access to the underlying frame writer object.
        /// </summary>
        IHttp2FrameWriter FrameWriter { get; }

        /// <summary>
        /// Gets the local settings on the top of the queue that has been sent but not ACKed. This may
        /// return <c>null</c>.
        /// </summary>
        Http2Settings PollSentSettings { get; }

        /// <summary>
        /// Sets the settings for the remote endpoint of the HTTP/2 connection.
        /// </summary>
        /// <param name="settings"></param>
        void RemoteSettings(Http2Settings settings);
    }
}
