// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    using System;
    using System.Collections.Generic;

    public sealed class DeflateFrameServerExtensionHandshaker : IWebSocketServerExtensionHandshaker
    {
        internal const string XWebkitDeflateFrameExtension = "x-webkit-deflate-frame";
        internal const string DeflateFrameExtension = "deflate-frame";

        readonly int compressionLevel;

        public DeflateFrameServerExtensionHandshaker()
            : this(6)
        {
        }

        public DeflateFrameServerExtensionHandshaker(int compressionLevel)
        {
            if (compressionLevel < 0 || compressionLevel > 9)
            {
                ThrowHelper.ThrowArgumentException_CompressionLevel(compressionLevel);
            }
            this.compressionLevel = compressionLevel;
        }

        public IWebSocketServerExtension HandshakeExtension(WebSocketExtensionData extensionData)
        {
            //if (!string.Equals(XWebkitDeflateFrameExtension, extensionData.Name, StringComparison.Ordinal) 
            //    && !string.Equals(DeflateFrameExtension, extensionData.Name, StringComparison.Ordinal))
            //{
            //    return null;
            //}

            //if (extensionData.Parameters.Count == 0)
            //{
            //    return new DeflateFrameServerExtension(this.compressionLevel, extensionData.Name);
            //}
            //else
            //{
            //    return null;
            //}
            var extensionDataName = extensionData.Name;
            switch (extensionDataName)
            {
                case XWebkitDeflateFrameExtension:
                case DeflateFrameExtension:
                    if (extensionData.Parameters.Count == 0)
                    {
                        return new DeflateFrameServerExtension(this.compressionLevel, extensionData.Name);
                    }
                    else
                    {
                        return null;
                    }

                default:
                    return null;
            }
        }

        sealed class DeflateFrameServerExtension : IWebSocketServerExtension
        {
            readonly string extensionName;
            readonly int compressionLevel;

            public DeflateFrameServerExtension(int compressionLevel, string extensionName)
            {
                this.extensionName = extensionName;
                this.compressionLevel = compressionLevel;
            }

            public int Rsv => WebSocketRsv.Rsv1;

            public WebSocketExtensionEncoder NewExtensionEncoder() => new PerFrameDeflateEncoder(this.compressionLevel, 15, false);

            public WebSocketExtensionDecoder NewExtensionDecoder() => new PerFrameDeflateDecoder(false);

            public WebSocketExtensionData NewReponseData() => new WebSocketExtensionData(this.extensionName, new Dictionary<string, string>(StringComparer.Ordinal));
        }
    }
}
