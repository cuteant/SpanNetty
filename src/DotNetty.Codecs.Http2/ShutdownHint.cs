// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Provides a hint as to if shutdown is justified, what type of shutdown should be executed.
    /// </summary>
    public enum ShutdownHint
    {
        /// <summary>
        /// Do not shutdown the underlying channel.
        /// </summary>
        NoShutdown,

        /// <summary>
        /// Attempt to execute a "graceful" shutdown. The definition of "graceful" is left to the implementation.
        /// An example of "graceful" would be wait for some amount of time until all active streams are closed.
        /// </summary>
        GracefulShutdown,

        /// <summary>
        /// Close the channel immediately after a <c>GOAWAY</c> is sent.
        /// </summary>
        HardShutdown
    }
}