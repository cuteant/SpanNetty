// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// The default <see cref="IHttp2WindowUpdateFrame"/> implementation.
    /// </summary>
    public class DefaultHttp2WindowUpdateFrame : AbstractHttp2StreamFrame, IHttp2WindowUpdateFrame
    {
        private readonly int windowUpdateIncrement;

        public DefaultHttp2WindowUpdateFrame(int windowUpdateIncrement)
        {
            this.windowUpdateIncrement = windowUpdateIncrement;
        }

        public override string Name => "WINDOW_UPDATE";

        public int WindowSizeIncrement => this.windowUpdateIncrement;
    }
}
