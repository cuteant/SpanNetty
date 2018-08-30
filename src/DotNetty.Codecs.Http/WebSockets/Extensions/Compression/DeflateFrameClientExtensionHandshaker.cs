// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    using System;
    using System.Collections.Generic;

    using static DeflateFrameServerExtensionHandshaker;

    public sealed class DeflateFrameClientExtensionHandshaker : IWebSocketClientExtensionHandshaker
    {
        readonly int compressionLevel;
        readonly bool useWebkitExtensionName;

        public DeflateFrameClientExtensionHandshaker(bool useWebkitExtensionName)
            : this(6, useWebkitExtensionName)
        {
        }

        public DeflateFrameClientExtensionHandshaker(int compressionLevel, bool useWebkitExtensionName)
        {
            if (compressionLevel < 0 || compressionLevel > 9)
            {
                ThrowHelper.ThrowArgumentException_CompressionLevel(compressionLevel);
            }
            this.compressionLevel = compressionLevel;
            this.useWebkitExtensionName = useWebkitExtensionName;
        }

        public WebSocketExtensionData NewRequestData() => new WebSocketExtensionData(
            this.useWebkitExtensionName ? XWebkitDeflateFrameExtension : DeflateFrameExtension,
            new Dictionary<string, string>(StringComparer.Ordinal));

        public IWebSocketClientExtension HandshakeExtension(WebSocketExtensionData extensionData)
        {
            if (extensionData.Parameters.Count > 0) { return null; }

            var extensionDataName = extensionData.Name;
            switch (extensionDataName)
            {
                case XWebkitDeflateFrameExtension:
                case DeflateFrameExtension:
                    return new DeflateFrameClientExtension(this.compressionLevel);

                default:
                    if (string.Equals(XWebkitDeflateFrameExtension, extensionDataName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(DeflateFrameExtension, extensionDataName, StringComparison.OrdinalIgnoreCase))
                    {
                        return new DeflateFrameClientExtension(this.compressionLevel);
                    }
                    return null;
            }
        }

        sealed class DeflateFrameClientExtension : IWebSocketClientExtension
        {
            readonly int compressionLevel;

            public DeflateFrameClientExtension(int compressionLevel)
            {
                this.compressionLevel = compressionLevel;
            }

            public int Rsv => WebSocketRsv.Rsv1;

            public WebSocketExtensionEncoder NewExtensionEncoder() => new PerFrameDeflateEncoder(this.compressionLevel, 15, false);

            public WebSocketExtensionDecoder NewExtensionDecoder() => new PerFrameDeflateDecoder(false);
        }
    }
}
