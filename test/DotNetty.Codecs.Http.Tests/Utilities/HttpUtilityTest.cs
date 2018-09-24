using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CuteAnt;
using Xunit;

namespace DotNetty.Codecs.Http.Utilities.Tests
{
    public class HttpUtilityTest
    {
        const char TestMaxChar = (char)0x100;

        #region HtmlAttributeEncode

        public static IEnumerable<object[]> HtmlAttributeEncodeData =>
            new[]
            {
                new object[] {string.Empty, string.Empty},
                new object[] {"&lt;script>", "<script>"},
                new object[] {"&quot;a&amp;b&quot;", "\"a&b\""},
                new object[] {"&#39;string&#39;", "'string'"},
                new object[] {"abc + def!", "abc + def!"},
                new object[] {"This is an &lt;element>!", "This is an <element>!"},
            };

        [Theory]
        [InlineData(null, null)]
        [MemberData(nameof(HtmlAttributeEncodeData))]
        public void HtmlAttributeEncode(string expected, string input)
        {
            Assert.Equal(expected, HttpUtility.HtmlAttributeEncode(input));
        }

        [Fact]
        public void HtmlAttributeEncode_TextWriter_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                HttpUtility.HtmlAttributeEncode("", null);
            });
        }

        [Theory]
        [InlineData("", null)]
        [MemberData(nameof(HtmlAttributeEncodeData))]
        public void HtmlAttributeEncode_TextWriter(string expected, string input)
        {
            var sw = new StringWriter();
            HttpUtility.HtmlAttributeEncode(input, sw);
            Assert.Equal(expected, sw.ToString());
        }

        #endregion HtmlAttributeEncode

        public static IEnumerable<object[]> HtmlEncodeDecodeData =>
            new[]
            {
                new object[] {"", ""},
                new object[] {"<script>", "&lt;script&gt;"},
            };

        #region HtmlDecode

        public static IEnumerable<object[]> HtmlDecodingData =>
            new[]
            {
                new object[] {@"áÁâÂ´", @"&aacute;&Aacute;&acirc;&Acirc;&acute;"},
                new object[] {@"æÆàÀℵ", @"&aelig;&AElig;&agrave;&Agrave;&alefsym;"},
                new object[] {@"αΑ&∧∠", @"&alpha;&Alpha;&amp;&and;&ang;"},
                new object[] {@"åÅ≈ãÃ", @"&aring;&Aring;&asymp;&atilde;&Atilde;"},
                new object[] {@"äÄ„βΒ", @"&auml;&Auml;&bdquo;&beta;&Beta;"},
                new object[] {@"¦•∩çÇ", @"&brvbar;&bull;&cap;&ccedil;&Ccedil;"},
                new object[] {@"¸¢χΧˆ", @"&cedil;&cent;&chi;&Chi;&circ;"},
                new object[] {@"♣≅©↵∪", @"&clubs;&cong;&copy;&crarr;&cup;"},
                new object[] {@"¤†‡↓⇓", @"&curren;&dagger;&Dagger;&darr;&dArr;"},
                new object[] {@"°δΔ♦÷", @"&deg;&delta;&Delta;&diams;&divide;"},
                new object[] {@"éÉêÊè", @"&eacute;&Eacute;&ecirc;&Ecirc;&egrave;"},
                new object[] {@"È∅  ε", @"&Egrave;&empty;&emsp;&ensp;&epsilon;"},
                new object[] {@"Ε≡ηΗð", @"&Epsilon;&equiv;&eta;&Eta;&eth;"},
                new object[] {@"ÐëË€∃", @"&ETH;&euml;&Euml;&euro;&exist;"},
                new object[] {@"ƒ∀½¼¾", @"&fnof;&forall;&frac12;&frac14;&frac34;"},
                new object[] {@"⁄γΓ≥>", @"&frasl;&gamma;&Gamma;&ge;&gt;"},
                new object[] {@"↔⇔♥…í", @"&harr;&hArr;&hearts;&hellip;&iacute;"},
                new object[] {@"ÍîÎ¡ì", @"&Iacute;&icirc;&Icirc;&iexcl;&igrave;"},
                new object[] {@"Ìℑ∞∫ι", @"&Igrave;&image;&infin;&int;&iota;"},
                new object[] {@"Ι¿∈ïÏ", @"&Iota;&iquest;&isin;&iuml;&Iuml;"},
                new object[] {@"κΚλΛ〈", @"&kappa;&Kappa;&lambda;&Lambda;&lang;"},
                new object[] {@"«←⇐⌈“", @"&laquo;&larr;&lArr;&lceil;&ldquo;"},
                new object[] {"≤⌊∗◊\u200E", @"&le;&lfloor;&lowast;&loz;&lrm;"},
                new object[] {@"‹‘<¯—", @"&lsaquo;&lsquo;&lt;&macr;&mdash;"},
                new object[] {@"µ·−μΜ", @"&micro;&middot;&minus;&mu;&Mu;"},
                new object[] {"∇\u00A0–≠∋", @"&nabla;&nbsp;&ndash;&ne;&ni;"},
                new object[] {@"¬∉⊄ñÑ", @"&not;&notin;&nsub;&ntilde;&Ntilde;"},
                new object[] {@"νΝóÓô", @"&nu;&Nu;&oacute;&Oacute;&ocirc;"},
                new object[] {@"ÔœŒòÒ", @"&Ocirc;&oelig;&OElig;&ograve;&Ograve;"},
                new object[] {@"‾ωΩοΟ", @"&oline;&omega;&Omega;&omicron;&Omicron;"},
                new object[] {@"⊕∨ªºø", @"&oplus;&or;&ordf;&ordm;&oslash;"},
                new object[] {@"ØõÕ⊗ö", @"&Oslash;&otilde;&Otilde;&otimes;&ouml;"},
                new object[] {@"Ö¶∂‰⊥", @"&Ouml;&para;&part;&permil;&perp;"},
                new object[] {@"φΦπΠϖ", @"&phi;&Phi;&pi;&Pi;&piv;"},
                new object[] {@"±£′″∏", @"&plusmn;&pound;&prime;&Prime;&prod;"},
                new object[] {@"∝ψΨ""√", @"&prop;&psi;&Psi;&quot;&radic;"},
                new object[] {@"〉»→⇒⌉", @"&rang;&raquo;&rarr;&rArr;&rceil;"},
                new object[] {@"”ℜ®⌋ρ", @"&rdquo;&real;&reg;&rfloor;&rho;"},
                new object[] {"Ρ\u200F›’‚", @"&Rho;&rlm;&rsaquo;&rsquo;&sbquo;"},
                new object[] {"šŠ⋅§\u00AD", @"&scaron;&Scaron;&sdot;&sect;&shy;"},
                new object[] {@"σΣς∼♠", @"&sigma;&Sigma;&sigmaf;&sim;&spades;"},
                new object[] {@"⊂⊆∑⊃¹", @"&sub;&sube;&sum;&sup;&sup1;"},
                new object[] {@"²³⊇ßτ", @"&sup2;&sup3;&supe;&szlig;&tau;"},
                new object[] {@"Τ∴θΘϑ", @"&Tau;&there4;&theta;&Theta;&thetasym;"},
                new object[] {@" þÞ˜×", @"&thinsp;&thorn;&THORN;&tilde;&times;"},
                new object[] {@"™úÚ↑⇑", @"&trade;&uacute;&Uacute;&uarr;&uArr;"},
                new object[] {@"ûÛùÙ¨", @"&ucirc;&Ucirc;&ugrave;&Ugrave;&uml;"},
                new object[] {@"ϒυΥüÜ", @"&upsih;&upsilon;&Upsilon;&uuml;&Uuml;"},
                new object[] {@"℘ξΞýÝ", @"&weierp;&xi;&Xi;&yacute;&Yacute;"},
                new object[] {@"¥ÿŸζΖ", @"&yen;&yuml;&Yuml;&zeta;&Zeta;"},
                new object[] {"\u200D\u200C", @"&zwj;&zwnj;"},
                new object[]
                {
                    @"&aacute&Aacute&acirc&Acirc&acute&aelig&AElig&agrave&Agrave&alefsym&alpha&Alpha&amp&and&ang&aring&Aring&asymp&atilde&Atilde&auml&Auml&bdquo&beta&Beta&brvbar&bull&cap&ccedil&Ccedil&cedil&cent&chi&Chi&circ&clubs&cong&copy&crarr&cup&curren&dagger&Dagger&darr&dArr&deg&delta&Delta&diams&divide&eacute&Eacute&ecirc&Ecirc&egrave&Egrave&empty&emsp&ensp&epsilon&Epsilon&equiv&eta&Eta&eth&ETH&euml&Euml&euro&exist&fnof&forall&frac12&frac14&frac34&frasl&gamma&Gamma&ge&gt&harr&hArr&hearts&hellip&iacute&Iacute&icirc&Icirc&iexcl&igrave&Igrave&image&infin&int&iota&Iota&iquest&isin&iuml&Iuml&kappa&Kappa&lambda&Lambda&lang&laquo&larr&lArr&lceil&ldquo&le&lfloor&lowast&loz&lrm&lsaquo&lsquo&lt&macr&mdash&micro&middot&minus&mu&Mu&nabla&nbsp&ndash&ne&ni&not&notin&nsub&ntilde&Ntilde&nu&Nu&oacute&Oacute&ocirc&Ocirc&oelig&OElig&ograve&Ograve&oline&omega&Omega&omicron&Omicron&oplus&or&ordf&ordm&oslash&Oslash&otilde&Otilde&otimes&ouml&Ouml&para&part&permil&perp&phi&Phi&pi&Pi&piv&plusmn&pound&prime&Prime&prod&prop&psi&Psi&quot&radic&rang&raquo&rarr&rArr&rceil&rdquo&real&reg&rfloor&rho&Rho&rlm&rsaquo&rsquo&sbquo&scaron&Scaron&sdot&sect&shy&sigma&Sigma&sigmaf&sim&spades&sub&sube&sum&sup&sup1&sup2&sup3&supe&szlig&tau&Tau&there4&theta&Theta&thetasym&thinsp&thorn&THORN&tilde&times&trade&uacute&Uacute&uarr&uArr&ucirc&Ucirc&ugrave&Ugrave&uml&upsih&upsilon&Upsilon&uuml&Uuml&weierp&xi&Xi&yacute&Yacute&yen&yuml&Yuml&zeta&Zeta&zwj&zwnj",
                    @"&aacute&Aacute&acirc&Acirc&acute&aelig&AElig&agrave&Agrave&alefsym&alpha&Alpha&amp&and&ang&aring&Aring&asymp&atilde&Atilde&auml&Auml&bdquo&beta&Beta&brvbar&bull&cap&ccedil&Ccedil&cedil&cent&chi&Chi&circ&clubs&cong&copy&crarr&cup&curren&dagger&Dagger&darr&dArr&deg&delta&Delta&diams&divide&eacute&Eacute&ecirc&Ecirc&egrave&Egrave&empty&emsp&ensp&epsilon&Epsilon&equiv&eta&Eta&eth&ETH&euml&Euml&euro&exist&fnof&forall&frac12&frac14&frac34&frasl&gamma&Gamma&ge&gt&harr&hArr&hearts&hellip&iacute&Iacute&icirc&Icirc&iexcl&igrave&Igrave&image&infin&int&iota&Iota&iquest&isin&iuml&Iuml&kappa&Kappa&lambda&Lambda&lang&laquo&larr&lArr&lceil&ldquo&le&lfloor&lowast&loz&lrm&lsaquo&lsquo&lt&macr&mdash&micro&middot&minus&mu&Mu&nabla&nbsp&ndash&ne&ni&not&notin&nsub&ntilde&Ntilde&nu&Nu&oacute&Oacute&ocirc&Ocirc&oelig&OElig&ograve&Ograve&oline&omega&Omega&omicron&Omicron&oplus&or&ordf&ordm&oslash&Oslash&otilde&Otilde&otimes&ouml&Ouml&para&part&permil&perp&phi&Phi&pi&Pi&piv&plusmn&pound&prime&Prime&prod&prop&psi&Psi&quot&radic&rang&raquo&rarr&rArr&rceil&rdquo&real&reg&rfloor&rho&Rho&rlm&rsaquo&rsquo&sbquo&scaron&Scaron&sdot&sect&shy&sigma&Sigma&sigmaf&sim&spades&sub&sube&sum&sup&sup1&sup2&sup3&supe&szlig&tau&Tau&there4&theta&Theta&thetasym&thinsp&thorn&THORN&tilde&times&trade&uacute&Uacute&uarr&uArr&ucirc&Ucirc&ugrave&Ugrave&uml&upsih&upsilon&Upsilon&uuml&Uuml&weierp&xi&Xi&yacute&Yacute&yen&yuml&Yuml&zeta&Zeta&zwj&zwnj",
                },
                new object[]
                {
                    "\u00A0¡¢£¤¥¦§¨©ª«¬­®¯°±²³´µ¶·¸¹º»¼½¾¿ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞßàáâãäåæçèéêëìíîïðñòóôõö÷øùúûüýþÿ",
                    @"&#160;&#161;&#162;&#163;&#164;&#165;&#166;&#167;&#168;&#169;&#170;&#171;&#172;&#173;&#174;&#175;&#176;&#177;&#178;&#179;&#180;&#181;&#182;&#183;&#184;&#185;&#186;&#187;&#188;&#189;&#190;&#191;&#192;&#193;&#194;&#195;&#196;&#197;&#198;&#199;&#200;&#201;&#202;&#203;&#204;&#205;&#206;&#207;&#208;&#209;&#210;&#211;&#212;&#213;&#214;&#215;&#216;&#217;&#218;&#219;&#220;&#221;&#222;&#223;&#224;&#225;&#226;&#227;&#228;&#229;&#230;&#231;&#232;&#233;&#234;&#235;&#236;&#237;&#238;&#239;&#240;&#241;&#242;&#243;&#244;&#245;&#246;&#247;&#248;&#249;&#250;&#251;&#252;&#253;&#254;&#255;",
                },
                new object[]
                {
                    "\0\x1\x2\x3\x4\x5\x6\x7\x8\x9\xa\xb\xc\xd\xe\xf\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f ",
                    @"&#000;&#001;&#002;&#003;&#004;&#005;&#006;&#007;&#008;&#009;&#010;&#011;&#012;&#013;&#014;&#015;&#016;&#017;&#018;&#019;&#020;&#021;&#022;&#023;&#024;&#025;&#026;&#027;&#028;&#029;&#030;&#031;&#032;",
                },
                new object[]
                {
                    "\0\x1\x2\x3\x4\x5\x6\x7\x8\x9\xa\xb\xc\xd\xe\xf\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f ",
                    @"&#x00;&#x01;&#x02;&#x03;&#x04;&#x05;&#x06;&#x07;&#x08;&#x09;&#x0A;&#x0B;&#x0C;&#x0D;&#x0E;&#x0F;&#x10;&#x11;&#x12;&#x13;&#x14;&#x15;&#x16;&#x17;&#x18;&#x19;&#x1A;&#x1B;&#x1C;&#x1D;&#x1E;&#x1F;&#x20;",
                },
                new object[]
                {
                    "\u00A0¡¢£¤¥¦§¨©ª«¬­®¯°±²³´µ¶·¸¹º»¼½¾¿ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞßàáâãäåæçèéêëìíîïðñòóôõö÷øùúûüýþÿ",
                    @"&#xA0;&#xA1;&#xA2;&#xA3;&#xA4;&#xA5;&#xA6;&#xA7;&#xA8;&#xA9;&#xAA;&#xAB;&#xAC;&#xAD;&#xAE;&#xAF;&#xB0;&#xB1;&#xB2;&#xB3;&#xB4;&#xB5;&#xB6;&#xB7;&#xB8;&#xB9;&#xBA;&#xBB;&#xBC;&#xBD;&#xBE;&#xBF;&#xC0;&#xC1;&#xC2;&#xC3;&#xC4;&#xC5;&#xC6;&#xC7;&#xC8;&#xC9;&#xCA;&#xCB;&#xCC;&#xCD;&#xCE;&#xCF;&#xD0;&#xD1;&#xD2;&#xD3;&#xD4;&#xD5;&#xD6;&#xD7;&#xD8;&#xD9;&#xDA;&#xDB;&#xDC;&#xDD;&#xDE;&#xDF;&#xE0;&#xE1;&#xE2;&#xE3;&#xE4;&#xE5;&#xE6;&#xE7;&#xE8;&#xE9;&#xEA;&#xEB;&#xEC;&#xED;&#xEE;&#xEF;&#xF0;&#xF1;&#xF2;&#xF3;&#xF4;&#xF5;&#xF6;&#xF7;&#xF8;&#xF9;&#xFA;&#xFB;&#xFC;&#xFD;&#xFE;&#xFF;",
                },
            };

        [Theory]
        [MemberData(nameof(HtmlEncodeDecodeData))]
        [MemberData(nameof(HtmlDecodingData))]
        public void HtmlDecode(string decoded, string encoded)
        {
            Assert.Equal(decoded, HttpUtility.HtmlDecode(encoded));
        }

        [Fact]
        public void HtmlDecode_TextWriter_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                HttpUtility.HtmlDecode("", null);
            });
        }

        [Theory]
        [MemberData(nameof(HtmlEncodeDecodeData))]
        [MemberData(nameof(HtmlDecodingData))]
        public void HtmlDecode_TextWriter(string decoded, string encoded)
        {
            var sw = new StringWriter();
            HttpUtility.HtmlDecode(encoded, sw);
            Assert.Equal(decoded, sw.ToString());
        }

        #endregion HtmlDecode

        #region ParseQueryString

        private static string UnicodeStr
            => new string(new[] { '\u304a', '\u75b2', '\u308c', '\u69d8', '\u3067', '\u3059' });

        public static IEnumerable<object[]> ParseQueryStringData =>
            new[]
            {
                new object[] {"name=value", new[] {"name"}, new[] {new[] {"value"}}},
                new object[] {"name=value&foo=bar", new[] {"name", "foo"}, new[] {new[] {"value"}, new[] {"bar"}}},
                new object[] {"name=value&name=bar", new[] {"name"}, new[] {new[] {"value", "bar"}}},
                new object[] {"value", new string[] {null}, new[] {new[] {"value"}}},
                new object[] {"name=value&bar", new[] {"name", null}, new[] {new[] {"value"}, new[] {"bar"}}},
                new object[] {"bar&name=value", new[] {null, "name"}, new[] {new[] {"bar"}, new[] {"value"}}},
                new object[] {"value&bar", new string[] {null}, new[] {new[] {"value", "bar"}}},
                new object[] {"", new string[] {}, new string[][] {}},
                new object[] {"=", new[] {""}, new[] {new[] {""}}},
                new object[] {"&", new string[] {null}, new[] {new[] {"", ""}}},
                new object[]
                {
                    HttpUtility.UrlEncode(UnicodeStr) + "=" + HttpUtility.UrlEncode(UnicodeStr),
                    new[] {UnicodeStr},
                    new[] {new[] {UnicodeStr}}
                },
                new object[] {"name=value=test", new[] {"name"}, new[] {new[] {"value=test"}}},
                new object[] { "name=value&#xe9;", new[] {"name", null}, new[] {new[] {"value"}, new[] { "#xe9;" } }},
                new object[] { "name=value&amp;name2=value2", new[] {"name", "amp;name2"}, new[] {new[] {"value"}, new[] { "value2" } }},
                new object[] {"name=value=test+phrase", new[] {"name"}, new[] {new[] {"value=test phrase"}}},
            };

        public static IEnumerable<object[]> ParseQueryStringDataQ =>
            ParseQueryStringData.Select(a => new object[] { "?" + (string)a[0] }.Concat(a.Skip(1)).ToArray())
                .Concat(new[]
                    {
                        new object[] { "??name=value=test", new[] { "?name" }, new[] { new[] { "value=test" }}},
                        new object[] { "?", EmptyArray<string>.Instance, EmptyArray<IList<string>>.Instance}
                    });

        [Theory]
        [MemberData(nameof(ParseQueryStringData))]
        [MemberData(nameof(ParseQueryStringDataQ))]
        public void ParseQueryString(string input, IList<string> keys, IList<IList<string>> values)
        {
            var parsed = HttpUtility.ParseQueryString(input);
            Assert.Equal(keys.Count, parsed.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                Assert.Equal(keys[i], parsed.GetKey(i));
                string[] actualValues = parsed.GetValues(i);
                Assert.Equal<string>(values[i], actualValues);
            }
        }

        [Fact]
        public void ParseQueryString_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                HttpUtility.ParseQueryString(null);
            });
        }

        [Fact]
        public void ParseQueryString_Encoding_null()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                HttpUtility.ParseQueryString("", null);
            });
        }

        [Fact]
        public void ParseQueryString_ToString()
        {
            var parsed = HttpUtility.ParseQueryString("");
            Assert.Empty(parsed.ToString());
            parsed.Add("ReturnUrl", @"http://localhost/login/authenticate?ReturnUrl=http://localhost/secured_area&__provider__=google");

            var expected = "ReturnUrl=http%3a%2f%2flocalhost%2flogin%2fauthenticate%3fReturnUrl%3dhttp%3a%2f%2flocalhost%2fsecured_area%26__provider__%3dgoogle";
            Assert.Equal(expected, parsed.ToString());
            Assert.Equal(expected, HttpUtility.ParseQueryString(expected).ToString(), StringComparer.OrdinalIgnoreCase);
        }

        #endregion ParseQueryString

        #region UrlDecode(ToBytes)

        public static IEnumerable<object[]> UrlDecodeData =>
            new[]
            {
                new object[] { "http://127.0.0.1:8080/appDir/page.aspx?foo=bar", "http://127.0.0.1:8080/appDir/page.aspx?foo=b%61r"},
                new object[] {"http://127.0.0.1:8080/appDir/page.aspx?foo=b%ar", "http://127.0.0.1:8080/appDir/page.aspx?foo=b%%61r"},
                new object[] {"http://127.0.0.1:8080/app%Dir/page.aspx?foo=b%ar", "http://127.0.0.1:8080/app%Dir/page.aspx?foo=b%%61r"},
                new object[] {"http://127.0.0.1:8080/app%%Dir/page.aspx?foo=b%%r", "http://127.0.0.1:8080/app%%Dir/page.aspx?foo=b%%r"},
                new object[] {"http://127.0.0.1:8080/appDir/page.aspx?foo=ba%r", "http://127.0.0.1:8080/appDir/page.aspx?foo=b%61%r"},
                new object[] {"http://127.0.0.1:8080/appDir/page.aspx?foo=bar", "http://127.0.0.1:8080/appDir/page.aspx?foo=b%u0061r"},
                new object[] {"http://127.0.0.1:8080/appDir/page.aspx?foo=b%ar", "http://127.0.0.1:8080/appDir/page.aspx?foo=b%%u0061r"},
                new object[] {"http://127.0.0.1:8080/appDir/page.aspx?foo=b%uu0061r", "http://127.0.0.1:8080/appDir/page.aspx?foo=b%uu0061r"},
                new object[] {"http://127.0.0.1:8080/appDir/page.aspx?foo=bar baz", "http://127.0.0.1:8080/appDir/page.aspx?foo=bar+baz"},
                new object[] { "http://example.net/\U00010000", "http://example.net/\U00010000" },
                new object[] { "http://example.net/\uFFFD", "http://example.net/\uD800" },
                new object[] { "http://example.net/\uFFFDa", "http://example.net/\uD800a" },
                new object[] { "http://example.net/\uFFFD", "http://example.net/\uDC00" },
                new object[] { "http://example.net/\uFFFDa", "http://example.net/\uDC00a" }
            };

        public static IEnumerable<object[]> UrlDecodeDataToBytes =>
            new[]
            {
                new object[] { "http://127.0.0.1:8080/appDir/page.aspx?foo=bar", "http://127.0.0.1:8080/appDir/page.aspx?foo=b%61r"},
                new object[] {"http://127.0.0.1:8080/appDir/page.aspx?foo=b%ar", "http://127.0.0.1:8080/appDir/page.aspx?foo=b%%61r"},
                new object[] {"http://127.0.0.1:8080/app%Dir/page.aspx?foo=b%ar", "http://127.0.0.1:8080/app%Dir/page.aspx?foo=b%%61r"},
                new object[] {"http://127.0.0.1:8080/app%%Dir/page.aspx?foo=b%%r", "http://127.0.0.1:8080/app%%Dir/page.aspx?foo=b%%r"},
                new object[] {"http://127.0.0.1:8080/appDir/page.aspx?foo=ba%r", "http://127.0.0.1:8080/appDir/page.aspx?foo=b%61%r"},
                new object[] {"http://127.0.0.1:8080/appDir/page.aspx?foo=b%uu0061r", "http://127.0.0.1:8080/appDir/page.aspx?foo=b%uu0061r"},
                new object[] {"http://127.0.0.1:8080/appDir/page.aspx?foo=b%u0061r", "http://127.0.0.1:8080/appDir/page.aspx?foo=b%u0061r"},
                new object[] {"http://127.0.0.1:8080/appDir/page.aspx?foo=b%%u0061r", "http://127.0.0.1:8080/appDir/page.aspx?foo=b%%u0061r"},
                new object[] {"http://127.0.0.1:8080/appDir/page.aspx?foo=bar baz", "http://127.0.0.1:8080/appDir/page.aspx?foo=bar+baz"},
                new object[] { "http://example.net/\U00010000", "http://example.net/\U00010000" },
                new object[] { "http://example.net/\uFFFD", "http://example.net/\uD800" },
                new object[] { "http://example.net/\uFFFDa", "http://example.net/\uD800a" },
                new object[] { "http://example.net/\uFFFD", "http://example.net/\uDC00" },
                new object[] { "http://example.net/\uFFFDa", "http://example.net/\uDC00a" }
            };

        [Theory]
        [MemberData(nameof(UrlDecodeData))]
        public void UrlDecode(string decoded, string encoded)
        {
            Assert.Equal(decoded, HttpUtility.UrlDecode(encoded));
        }

        [Fact]
        public void UrlDecode_null()
        {
            Assert.Null(HttpUtility.UrlDecode(default(string), Encoding.UTF8));
            Assert.Null(HttpUtility.UrlDecode(default(byte[]), Encoding.UTF8));
            Assert.Null(HttpUtility.UrlDecode(null));
            Assert.Null(HttpUtility.UrlDecode(null, 2, 0, Encoding.UTF8));
            //Assert.Throws<ArgumentNullException>("bytes", () => HttpUtility.UrlDecode(null, 2, 3, Encoding.UTF8));
        }

        [Fact]
        public void UrlDecode_OutOfRange()
        {
            byte[] bytes = { 0, 1, 2 };
            Assert.Throws<ArgumentOutOfRangeException>("offset", () => HttpUtility.UrlDecode(bytes, -1, 2, Encoding.UTF8));
            Assert.Throws<ArgumentOutOfRangeException>("offset", () => HttpUtility.UrlDecode(bytes, 14, 2, Encoding.UTF8));
            Assert.Throws<ArgumentOutOfRangeException>("count", () => HttpUtility.UrlDecode(bytes, 1, 12, Encoding.UTF8));
            Assert.Throws<ArgumentOutOfRangeException>("count", () => HttpUtility.UrlDecode(bytes, 1, -12, Encoding.UTF8));
        }

        [Theory]
        [MemberData(nameof(UrlDecodeDataToBytes))]
        public void UrlDecodeToBytes(string decoded, string encoded)
        {
            Assert.Equal(decoded, Encoding.UTF8.GetString(HttpUtility.UrlDecodeToBytes(encoded, Encoding.UTF8)));
        }

        [Theory]
        [MemberData(nameof(UrlDecodeDataToBytes))]
        public void UrlDecodeToBytes_DefaultEncoding(string decoded, string encoded)
        {
            Assert.Equal(decoded, Encoding.UTF8.GetString(HttpUtility.UrlDecodeToBytes(encoded)));
        }

        [Fact]
        public void UrlDecodeToBytes_null()
        {
            Assert.Null(HttpUtility.UrlDecodeToBytes(default(byte[])));
            Assert.Null(HttpUtility.UrlDecodeToBytes(default(string)));
            Assert.Null(HttpUtility.UrlDecodeToBytes(default(string), Encoding.UTF8));
            Assert.Null(HttpUtility.UrlDecodeToBytes(default(byte[]), 2, 0));
            //Assert.Throws<ArgumentNullException>("bytes", () => HttpUtility.UrlDecodeToBytes(default(byte[]), 2, 3));
        }

        [Fact]
        public void UrlDecodeToBytes_OutOfRange()
        {
            byte[] bytes = { 0, 1, 2 };
            Assert.Throws<ArgumentOutOfRangeException>("offset", () => HttpUtility.UrlDecodeToBytes(bytes, -1, 2));
            Assert.Throws<ArgumentOutOfRangeException>("offset", () => HttpUtility.UrlDecodeToBytes(bytes, 14, 2));
            Assert.Throws<ArgumentOutOfRangeException>("count", () => HttpUtility.UrlDecodeToBytes(bytes, 1, 12));
            Assert.Throws<ArgumentOutOfRangeException>("count", () => HttpUtility.UrlDecodeToBytes(bytes, 1, -12));
        }

        [Theory]
        [MemberData(nameof(UrlDecodeData))]
        public void UrlDecode_ByteArray(string decoded, string encoded)
        {
            Assert.Equal(decoded, HttpUtility.UrlDecode(Encoding.UTF8.GetBytes(encoded), Encoding.UTF8));
        }

        #endregion UrlDecode(ToBytes)

        #region UrlEncode(ToBytes)

        static bool IsUrlSafeChar(char c)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
            {
                return true;
            }
            switch (c)
            {
                case '-':
                case '_':
                case '.':
                case '!':
                case '*':
                case '(':
                case ')':
                    return true;
            }
            return false;
        }

        static string UrlEncodeChar(char c)
        {
            if (IsUrlSafeChar(c))
            {
                return c.ToString();
            }
            if (c == ' ')
            {
                return "+";
            }
            byte[] bytes = Encoding.UTF8.GetBytes(c.ToString());
            return string.Join("", bytes.Select(b => $"%{b:x2}"));
        }

        public static IEnumerable<object[]> UrlEncodeData
        {
            get
            {
                yield return new object[] { "", "" };
                for (char c = char.MinValue; c < TestMaxChar; c++)
                {
                    yield return new object[] { c.ToString(), UrlEncodeChar(c) };
                }
            }
        }

        [Theory]
        [InlineData(null, null)]
        [MemberData(nameof(UrlEncodeData))]
        public void UrlEncode(string decoded, string encoded)
        {
            Assert.Equal(encoded, HttpUtility.UrlEncode(decoded, Encoding.UTF8));
        }

        [Theory]
        [MemberData(nameof(UrlEncodeData))]
        public void UrlEncodeToBytes(string decoded, string encoded)
        {
            Assert.Equal(encoded, Encoding.UTF8.GetString(HttpUtility.UrlEncodeToBytes(decoded, Encoding.UTF8)));
        }

        [Theory]
        [MemberData(nameof(UrlEncodeData))]
        public void UrlEncodeToBytes_DefaultEncoding(string decoded, string encoded)
        {
            Assert.Equal(encoded, Encoding.UTF8.GetString(HttpUtility.UrlEncodeToBytes(decoded)));
        }

        [Theory, MemberData(nameof(UrlEncodeData))]
        public void UrlEncodeToBytesExplicitSize(string decoded, string encoded)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(decoded);
            Assert.Equal(encoded, Encoding.UTF8.GetString(HttpUtility.UrlEncodeToBytes(bytes, 0, bytes.Length)));
        }


        [Theory]
        [InlineData(" abc defgh", "abc+def", 1, 7)]
        [InlineData(" abc defgh", "", 1, 0)]
        public void UrlEncodeToBytesExplicitSize0(string decoded, string encoded, int offset, int count)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(decoded);
            Assert.Equal(encoded, Encoding.UTF8.GetString(HttpUtility.UrlEncodeToBytes(bytes, offset, count)));
        }

        [Theory]
        [InlineData("abc def", " abc+defgh", 1, 7)]
        [InlineData("", " abc defgh", 1, 0)]
        public void UrlDecodeToBytesExplicitSize(string decoded, string encoded, int offset, int count)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(encoded);
            Assert.Equal(decoded, Encoding.UTF8.GetString(HttpUtility.UrlDecodeToBytes(bytes, offset, count)));
        }

        [Fact]
        public void UrlEncodeToBytes_null()
        {
            Assert.Null(HttpUtility.UrlEncodeToBytes(null, Encoding.UTF8));
            Assert.Null(HttpUtility.UrlEncodeToBytes(default(byte[])));
            Assert.Null(HttpUtility.UrlEncodeToBytes(default(string)));
            Assert.Null(HttpUtility.UrlEncodeToBytes(null, 2, 0));
            //Assert.Throws<ArgumentNullException>("bytes", () => HttpUtility.UrlEncodeToBytes(null, 2, 3));
        }

        [Fact]
        public void UrlEncodeToBytes_OutOfRange()
        {
            byte[] bytes = { 0, 1, 2 };
            Assert.Throws<ArgumentOutOfRangeException>("offset", () => HttpUtility.UrlEncodeToBytes(bytes, -1, 2));
            Assert.Throws<ArgumentOutOfRangeException>("offset", () => HttpUtility.UrlEncodeToBytes(bytes, 14, 2));
            Assert.Throws<ArgumentOutOfRangeException>("count", () => HttpUtility.UrlEncodeToBytes(bytes, 1, 12));
            Assert.Throws<ArgumentOutOfRangeException>("count", () => HttpUtility.UrlEncodeToBytes(bytes, 1, -12));
        }

        [Theory]
        [MemberData(nameof(UrlEncodeData))]
        public void UrlEncode_ByteArray(string decoded, string encoded)
        {
            Assert.Equal(encoded, HttpUtility.UrlEncode(Encoding.UTF8.GetBytes(decoded)));
        }

        [Fact]
        public void UrlEncode_null()
        {
            Assert.Null(HttpUtility.UrlEncode((byte[])null));
            Assert.Null(HttpUtility.UrlEncode((string)null));
            Assert.Null(HttpUtility.UrlEncode(null, Encoding.UTF8));
            Assert.Null(HttpUtility.UrlEncode(null, 2, 3));
        }

        [Fact]
        public void UrlEncode_OutOfRange()
        {
            byte[] bytes = { 0, 1, 2 };
            Assert.Throws<ArgumentOutOfRangeException>("offset", () => HttpUtility.UrlEncode(bytes, -1, 2));
            Assert.Throws<ArgumentOutOfRangeException>("offset", () => HttpUtility.UrlEncode(bytes, 14, 2));
            Assert.Throws<ArgumentOutOfRangeException>("count", () => HttpUtility.UrlEncode(bytes, 1, 12));
            Assert.Throws<ArgumentOutOfRangeException>("count", () => HttpUtility.UrlEncode(bytes, 1, -12));
        }

        #endregion UrlEncode(ToBytes)

        [Theory]
        [InlineData(null, null)]
        [InlineData(" ", "%20")]
        [InlineData("\n", "%0a")]
        [InlineData("default.xxx?sdsd=sds", "default.xxx?sdsd=sds")]
        [InlineData("?sdsd=sds", "?sdsd=sds")]
        [InlineData("", "")]
        [InlineData("http://example.net/default.xxx?sdsd=sds", "http://example.net/default.xxx?sdsd=sds")]
        [InlineData("http://example.net:8080/default.xxx?sdsd=sds", "http://example.net:8080/default.xxx?sdsd=sds")]
        [InlineData("http://eXample.net:80/default.xxx?sdsd=sds", "http://eXample.net:80/default.xxx?sdsd=sds")]
        [InlineData("http://EXAMPLE.NET/default.xxx?sdsd=sds", "http://EXAMPLE.NET/default.xxx?sdsd=sds")]
        [InlineData("http://EXAMPLE.NET/défault.xxx?sdsd=sds", "http://EXAMPLE.NET/d%c3%a9fault.xxx?sdsd=sds")]
        [InlineData("file:///C/Users", "file:///C/Users")]
        [InlineData("mailto:user@example.net", "mailto:user@example.net")]
        [InlineData("http://example\u200E.net/", "http://example%e2%80%8e.net/")]
        public void UrlPathEncode(string decoded, string encoded)
        {
            Assert.Equal(encoded, HttpUtility.UrlPathEncode(decoded));
        }
    }
}
