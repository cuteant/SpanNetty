
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;

    sealed class HpackTestCase
    {
#pragma warning disable 0649
        [JsonProperty("max_header_table_size")]
        int maxHeaderTableSize = -1;
        [JsonProperty("sensitive_headers")]
        bool sensitiveHeaders;

        [JsonProperty("header_blocks")]
        List<HeaderBlock> headerBlocks;
#pragma warning restore 0649

        internal static HpackTestCase Load(Stream stream)
        {
            var jsongSettings = new JsonSerializerSettings();
            jsongSettings.Converters.Add(new HpackHeaderFieldConverter());
            var jsonSerializer = JsonSerializer.Create(jsongSettings);
            using (var reader = new JsonTextReader(new StreamReader(stream)))
            {
                var result = jsonSerializer.Deserialize<HpackTestCase>(reader);
                foreach (var headerBlock in result.headerBlocks)
                {
                    headerBlock.encodedBytes = StringUtil.DecodeHexDump(headerBlock.GetEncodedStr());
                }
                return result;
            }
        }

        internal void TestCompress()
        {
            HpackEncoder hpackEncoder = this.CreateEncoder();
            foreach (HeaderBlock headerBlock in this.headerBlocks)
            {
                byte[] actual =
                        Encode(hpackEncoder, headerBlock.GetHeaders(), headerBlock.GetMaxHeaderTableSize(),
                                this.sensitiveHeaders);

                if (!(actual.Length == headerBlock.encodedBytes.Length && PlatformDependent.ByteArrayEquals(actual, 0, headerBlock.encodedBytes, 0, actual.Length)))
                {
                    throw new Exception(
                            "\nEXPECTED:\n" + headerBlock.GetEncodedStr() +
                                    "\nACTUAL:\n" + StringUtil.ToHexString(actual));
                }

                List<HpackHeaderField> actualDynamicTable = new List<HpackHeaderField>();
                for (int index = 0; index < hpackEncoder.Length(); index++)
                {
                    actualDynamicTable.Add(hpackEncoder.GetHeaderField(index));
                }

                List<HpackHeaderField> expectedDynamicTable = headerBlock.GetDynamicTable();

                if (!HeadersEqual(expectedDynamicTable, actualDynamicTable))
                {
                    throw new Exception(
                            "\nEXPECTED DYNAMIC TABLE:\n" + expectedDynamicTable +
                                    "\nACTUAL DYNAMIC TABLE:\n" + actualDynamicTable);
                }

                if (headerBlock.GetTableSize() != hpackEncoder.Size())
                {
                    throw new Exception(
                            "\nEXPECTED TABLE SIZE: " + headerBlock.GetTableSize() +
                                    "\n ACTUAL TABLE SIZE : " + hpackEncoder.Size());
                }
            }
        }

        internal void TestDecompress()
        {
            HpackDecoder hpackDecoder = this.CreateDecoder();

            foreach (HeaderBlock headerBlock in this.headerBlocks)
            {

                List<HpackHeaderField> actualHeaders = Decode(hpackDecoder, headerBlock.encodedBytes);

                List<HpackHeaderField> expectedHeaders = new List<HpackHeaderField>();
                foreach (HpackHeaderField h in headerBlock.GetHeaders())
                {
                    expectedHeaders.Add(new HpackHeaderField(h._name, h._value));
                }

                if (!HeadersEqual(expectedHeaders, actualHeaders))
                {
                    throw new Exception(
                            "\nEXPECTED:\n" + expectedHeaders +
                                    "\nACTUAL:\n" + actualHeaders);
                }

                List<HpackHeaderField> actualDynamicTable = new List<HpackHeaderField>();
                for (int index = 0; index < hpackDecoder.Length(); index++)
                {
                    actualDynamicTable.Add(hpackDecoder.GetHeaderField(index));
                }

                List<HpackHeaderField> expectedDynamicTable = headerBlock.GetDynamicTable();

                if (!HeadersEqual(expectedDynamicTable, actualDynamicTable))
                {
                    throw new Exception(
                            "\nEXPECTED DYNAMIC TABLE:\n" + expectedDynamicTable +
                                    "\nACTUAL DYNAMIC TABLE:\n" + actualDynamicTable);
                }

                if (headerBlock.GetTableSize() != hpackDecoder.Size())
                {
                    throw new Exception(
                            "\nEXPECTED TABLE SIZE: " + headerBlock.GetTableSize() +
                                    "\n ACTUAL TABLE SIZE : " + hpackDecoder.Size());
                }
            }
        }

        private HpackEncoder CreateEncoder()
        {
            int maxHeaderTableSize = this.maxHeaderTableSize;
            if (maxHeaderTableSize == -1)
            {
                maxHeaderTableSize = int.MaxValue;
            }

            try
            {
                return Http2TestUtil.NewTestEncoder(true, Http2CodecUtil.MaxHeaderListSize, maxHeaderTableSize);
            }
            catch (Http2Exception e)
            {
                throw new Exception("invalid initial values!", e);
            }
        }

        private HpackDecoder CreateDecoder()
        {
            int maxHeaderTableSize = this.maxHeaderTableSize;
            if (maxHeaderTableSize == -1)
            {
                maxHeaderTableSize = Http2CodecUtil.DefaultHeaderTableSize; // TODO int.MaxValue sometimes throw OutOfMemoryException
            }

            return new HpackDecoder(Http2CodecUtil.DefaultHeaderListSize, maxHeaderTableSize);
        }

        private static byte[] Encode(HpackEncoder hpackEncoder, List<HpackHeaderField> headers, int maxHeaderTableSize, bool sensitive)
        {
            IHttp2Headers http2Headers = ToHttp2Headers(headers);
            ISensitivityDetector sensitivityDetector = sensitive ? AlwaysSensitiveDetector.Instance : NeverSensitiveDetector.Instance;
            var buffer = Unpooled.Buffer();
            try
            {
                if (maxHeaderTableSize != -1)
                {
                    hpackEncoder.SetMaxHeaderTableSize(buffer, maxHeaderTableSize);
                }

                hpackEncoder.EncodeHeaders(3 /* randomly chosen */, buffer, http2Headers, sensitivityDetector);
                byte[] bytes = new byte[buffer.ReadableBytes];
                buffer.ReadBytes(bytes);
                return bytes;
            }
            finally
            {
                buffer.Release();
            }
        }

        private static IHttp2Headers ToHttp2Headers(List<HpackHeaderField> inHeaders)
        {
            IHttp2Headers headers = new DefaultHttp2Headers(false);
            foreach (HpackHeaderField e in inHeaders)
            {
                headers.Add(e._name, e._value);
            }
            return headers;
        }

        private static List<HpackHeaderField> Decode(HpackDecoder hpackDecoder, byte[] expected)
        {
            var input = Unpooled.WrappedBuffer(expected);
            try
            {
                List<HpackHeaderField> headers = new List<HpackHeaderField>();
                TestHeaderListener listener = new TestHeaderListener(headers);
                hpackDecoder.Decode(0, input, listener, true);
                return headers;
            }
            finally
            {
                input.Release();
            }
        }

        private static string Concat(List<string> l)
        {
            StringBuilder ret = new StringBuilder();
            foreach (var s in l)
            {
                ret.Append(s);
            }
            return ret.ToString();
        }

        sealed class HpackHeaderFieldConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(HpackHeaderField);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var obj = JObject.Load(reader);
                var ps = obj.Properties().ToArray();

                var result = new HpackHeaderField((AsciiString)ps[0].Name, (AsciiString)(ps[0].Value.ToString()));
                return result;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                //
            }
        }

        private static bool HeadersEqual(List<HpackHeaderField> expected, List<HpackHeaderField> actual)
        {
            if (expected.Count != actual.Count)
            {
                return false;
            }
            for (int i = 0; i < expected.Count; i++)
            {
                if (!expected[i].EqualsForTest(actual[i]))
                {
                    return false;
                }
            }
            return true;
        }

        internal sealed class HeaderBlock
        {
#pragma warning disable 0649
            [JsonProperty("max_header_table_size")]
            internal int maxHeaderTableSize = -1;
            internal byte[] encodedBytes;
            [JsonProperty("encoded")]
            internal List<string> encoded;
            [JsonProperty("headers")]
            internal List<HpackHeaderField> headers;
            [JsonProperty("dynamic_table")]
            internal List<HpackHeaderField> dynamicTable;
            [JsonProperty("table_size")]
            internal int tableSize;
#pragma warning restore 0649

            internal int GetMaxHeaderTableSize()
            {
                return this.maxHeaderTableSize;
            }

            public string GetEncodedStr()
            {
                return Concat(this.encoded).Replace(" ", "");
            }

            public List<HpackHeaderField> GetHeaders()
            {
                return this.headers;
            }

            public List<HpackHeaderField> GetDynamicTable()
            {
                return this.dynamicTable;
            }

            public int GetTableSize()
            {
                return this.tableSize;
            }
        }
    }
}
