// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    static class HpackStaticTable
    {
        // Appendix A: Static Table
        // http://tools.ietf.org/html/rfc7541#appendix-A
        static readonly HpackHeaderField[] StaticTable;

        /// <summary>
        /// The number of header fields in the static table.
        /// </summary>
        internal static readonly int Length;

        static readonly CharSequenceMap<int> StaticIndexByName;

        static HpackStaticTable()
        {
            StaticTable = new[]
            {
                /*  1 */ NewEmptyHeaderField(":authority"),
                /*  2 */ NewHeaderField(":method", "GET"),
                /*  3 */ NewHeaderField(":method", "POST"),
                /*  4 */ NewHeaderField(":path", "/"),
                /*  5 */ NewHeaderField(":path", "/index.html"),
                /*  6 */ NewHeaderField(":scheme", "http"),
                /*  7 */ NewHeaderField(":scheme", "https"),
                /*  8 */ NewHeaderField(":status", "200"),
                /*  9 */ NewHeaderField(":status", "204"),
                /* 10 */ NewHeaderField(":status", "206"),
                /* 11 */ NewHeaderField(":status", "304"),
                /* 12 */ NewHeaderField(":status", "400"),
                /* 13 */ NewHeaderField(":status", "404"),
                /* 14 */ NewHeaderField(":status", "500"),
                /* 15 */ NewEmptyHeaderField("accept-charset"),
                /* 16 */ NewHeaderField("accept-encoding", "gzip, deflate"),
                /* 17 */ NewEmptyHeaderField("accept-language"),
                /* 18 */ NewEmptyHeaderField("accept-ranges"),
                /* 19 */ NewEmptyHeaderField("accept"),
                /* 20 */ NewEmptyHeaderField("access-control-allow-origin"),
                /* 21 */ NewEmptyHeaderField("age"),
                /* 22 */ NewEmptyHeaderField("allow"),
                /* 23 */ NewEmptyHeaderField("authorization"),
                /* 24 */ NewEmptyHeaderField("cache-control"),
                /* 25 */ NewEmptyHeaderField("content-disposition"),
                /* 26 */ NewEmptyHeaderField("content-encoding"),
                /* 27 */ NewEmptyHeaderField("content-language"),
                /* 28 */ NewEmptyHeaderField("content-length"),
                /* 29 */ NewEmptyHeaderField("content-location"),
                /* 30 */ NewEmptyHeaderField("content-range"),
                /* 31 */ NewEmptyHeaderField("content-type"),
                /* 32 */ NewEmptyHeaderField("cookie"),
                /* 33 */ NewEmptyHeaderField("date"),
                /* 34 */ NewEmptyHeaderField("etag"),
                /* 35 */ NewEmptyHeaderField("expect"),
                /* 36 */ NewEmptyHeaderField("expires"),
                /* 37 */ NewEmptyHeaderField("from"),
                /* 38 */ NewEmptyHeaderField("host"),
                /* 39 */ NewEmptyHeaderField("if-match"),
                /* 40 */ NewEmptyHeaderField("if-modified-since"),
                /* 41 */ NewEmptyHeaderField("if-none-match"),
                /* 42 */ NewEmptyHeaderField("if-range"),
                /* 43 */ NewEmptyHeaderField("if-unmodified-since"),
                /* 44 */ NewEmptyHeaderField("last-modified"),
                /* 45 */ NewEmptyHeaderField("link"),
                /* 46 */ NewEmptyHeaderField("location"),
                /* 47 */ NewEmptyHeaderField("max-forwards"),
                /* 48 */ NewEmptyHeaderField("proxy-authenticate"),
                /* 49 */ NewEmptyHeaderField("proxy-authorization"),
                /* 50 */ NewEmptyHeaderField("range"),
                /* 51 */ NewEmptyHeaderField("referer"),
                /* 52 */ NewEmptyHeaderField("refresh"),
                /* 53 */ NewEmptyHeaderField("retry-after"),
                /* 54 */ NewEmptyHeaderField("server"),
                /* 55 */ NewEmptyHeaderField("set-cookie"),
                /* 56 */ NewEmptyHeaderField("strict-transport-security"),
                /* 57 */ NewEmptyHeaderField("transfer-encoding"),
                /* 58 */ NewEmptyHeaderField("user-agent"),
                /* 59 */ NewEmptyHeaderField("vary"),
                /* 60 */ NewEmptyHeaderField("via"),
                /* 61 */ NewEmptyHeaderField("www-authenticate")
            };

            Length = StaticTable.Length;

            StaticIndexByName = CreateMap();
        }

        static HpackHeaderField NewEmptyHeaderField(string name)
        {
            return new HpackHeaderField(AsciiString.Cached(name), AsciiString.Empty);
        }

        static HpackHeaderField NewHeaderField(string name, string value)
        {
            return new HpackHeaderField(AsciiString.Cached(name), AsciiString.Cached(value));
        }

        /// <summary>
        /// Return the header field at the given index value.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        internal static HpackHeaderField GetEntry(int index)
        {
            return StaticTable[index - 1];
        }

        /// <summary>
        /// Returns the lowest index value for the given header field name in the static table. Returns
        /// -1 if the header field name is not in the static table.
        /// </summary>
        /// <param name="name"></param>
        internal static int GetIndex(ICharSequence name)
        {
            return StaticIndexByName.TryGet(name, out var index) ? index : -1;
        }

        /// <summary>
        /// Returns the index value for the given header field in the static table. Returns -1 if the
        /// header field is not in the static table.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static int GetIndex(ICharSequence name, ICharSequence value)
        {
            int index = GetIndex(name);
            if ((uint)index > SharedConstants.TooBigOrNegative/* == -1*/) { return -1; }

            // Note this assumes all entries for a given header field are sequential.
            while ((uint)index <= (uint)Length)
            {
                HpackHeaderField entry = GetEntry(index);
                if (0u >= (uint)HpackUtil.EqualsConstantTime(name, entry.name))
                {
                    break;
                }

                if (HpackUtil.EqualsConstantTime(value, entry.value) != 0)
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        /// <summary>
        /// create a map CharSequenceMap header name to index value to allow quick lookup
        /// </summary>
        static CharSequenceMap<int> CreateMap()
        {
            int length = StaticTable.Length;
            CharSequenceMap<int> ret = new CharSequenceMap<int>(true, UnsupportedValueConverter<int>.Instance, length);
            // Iterate through the static table in reverse order to
            // save the smallest index for a given name in the map.
            for (int index = length; index > 0; index--)
            {
                HpackHeaderField entry = GetEntry(index);
                ICharSequence name = entry.name;
                ret.Set(name, index);
            }

            return ret;
        }
    }
}