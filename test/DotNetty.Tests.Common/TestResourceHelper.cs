// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Tests.Common
{
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography.X509Certificates;

    public static class TestResourceHelper
    {
        static X509Certificate2 _testCert;
        static X509Certificate2 _testCert2;

        public static X509Certificate2 GetTestCertificate()
        {
            var testCert = _testCert;
            if (testCert is null)
            {
                byte[] certData;
                using (Stream resStream = typeof(TestResourceHelper).GetTypeInfo().Assembly.GetManifestResourceStream(typeof(TestResourceHelper).Namespace + "." + "dotnetty.com.pfx"))
                using (var memStream = new MemoryStream())
                {
                    resStream.CopyTo(memStream);
                    certData = memStream.ToArray();
                }
                testCert = _testCert = new X509Certificate2(certData, "password");
            }

            return testCert;
        }

        public static X509Certificate2 GetTestCertificate2()
        {
            var testCert2 = _testCert2;
            if (testCert2 is null)
            {
                byte[] certData;
                using (Stream resStream = typeof(TestResourceHelper).GetTypeInfo().Assembly.GetManifestResourceStream(typeof(TestResourceHelper).Namespace + "." + "contoso.com.pfx"))
                using (var memStream = new MemoryStream())
                {
                    resStream.CopyTo(memStream);
                    certData = memStream.ToArray();
                }
                testCert2 = _testCert2 = new X509Certificate2(certData, "password");
            }
            return testCert2;
        }
    }
}