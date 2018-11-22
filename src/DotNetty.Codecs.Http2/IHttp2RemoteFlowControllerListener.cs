// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Listener to the number of flow-controlled bytes written per stream.
    /// </summary>
    public interface IHttp2RemoteFlowControllerListener
    {
        /// <summary>
        /// Notification that <see cref="IHttp2RemoteFlowController.IsWritable(IHttp2Stream)"/> has changed for <paramref name="stream"/>.
        /// This method should not throw. Any thrown exceptions are considered a programming error and are ignored.
        /// </summary>
        /// <param name="stream">The stream which writability has changed for.</param>
        void WritabilityChanged(IHttp2Stream stream);
    }
}
