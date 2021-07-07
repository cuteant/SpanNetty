
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Xunit;

    public class HpackTest
    {
        static readonly string TEST_DIR = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"testdata");

        public static IEnumerable<object[]> GetJsonFiles()
        {
            var dir = new DirectoryInfo(TEST_DIR);
            foreach (var file in dir.GetFiles())
            {
                yield return new object[] { file };
            }
        }

        [Theory]
        [MemberData(nameof(GetJsonFiles), DisableDiscoveryEnumeration = true)]
        public void Test(FileInfo file)
        {
            using (var fs = file.Open(FileMode.Open))
            {
                HpackTestCase hpackTestCase = HpackTestCase.Load(fs);
                hpackTestCase.TestCompress();
                hpackTestCase.TestDecompress();
            }
        }
    }
}
