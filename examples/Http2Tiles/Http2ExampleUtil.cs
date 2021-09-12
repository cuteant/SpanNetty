namespace Http2Tiles
{
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using System.IO;

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
            using var ms = new MemoryStream();
            input.CopyTo(ms);
            return Unpooled.WrappedBuffer(ms.ToArray());
        }

        public static string FirstValue(QueryStringDecoder query, string key)
        {
            if (null == query)
            {
                return null;
            }

            if (!query.Parameters.TryGetValue(key, out var values))
            {
                return null;
            }

            return values[0];
        }
    }
}
