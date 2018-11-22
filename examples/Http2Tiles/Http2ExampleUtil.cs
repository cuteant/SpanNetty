// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Http2Tiles
{
    using System;
    using System.IO;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;

    /// <summary>
    /// Utility methods used by the example client and server.
    /// </summary>
    public sealed class Http2ExampleUtil
    {
        /// <summary>
        /// Response header sent in response to the http-&gt;http2 cleartext upgrade request.
        /// </summary>
        public const string UPGRADE_RESPONSE_HEADER = "http-to-http2-upgrade";

        /// <summary>
        /// Size of the block to be read from the input stream.
        /// </summary>
        const int BLOCK_SIZE = 1024;

        /// <summary>
        /// Returns the integer value of a string or the default value, if the string is either null or empty.
        /// </summary>
        /// <param name="str">the string to be converted to an integer.</param>
        /// <param name="defaultValue">the default value</param>
        public static int ToInt(string str, int defaultValue)
        {
            if (!string.IsNullOrEmpty(str))
            {
                return int.Parse(str);
            }
            return defaultValue;
        }

        public static IByteBuffer ToByteBuffer(Stream input)
        {
            var ms = new MemoryStream();
            input.CopyTo(ms);
            return Unpooled.WrappedBuffer(ms.ToArray());
        }

        public static string FirstValue(QueryStringDecoder query, string key)
        {
            if (null == query) { return null; }

            if (!query.Parameters.TryGetValue(key, out var values))
            {
                return null;
            }
            return values[0];
        }
    }
}
