// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace DotNetty.Handlers.Tls
{
    // https://github.com/aspnet/KestrelHttpServer/blob/master/src/Kestrel.Core/Internal/CertificateLoader.cs
    public static class CertificateLoader
    {
        // See http://oid-info.com/get/1.3.6.1.5.5.7.3.1
        // Indicates that a certificate can be used as a SSL server certificate
        private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

        public static X509Certificate2 LoadFromStoreCert(string subject, string storeName, StoreLocation storeLocation, bool allowInvalid)
        {
#if NET40 || NET451
            var store = new X509Store(storeName, storeLocation);
#else
            using (var store = new X509Store(storeName, storeLocation))
#endif
            {
                X509Certificate2Collection storeCertificates = null;
                X509Certificate2 foundCertificate = null;

                try
                {
                    store.Open(OpenFlags.ReadOnly);
                    storeCertificates = store.Certificates;
                    var foundCertificates = storeCertificates.Find(X509FindType.FindBySubjectName, subject, !allowInvalid);
                    foundCertificate = foundCertificates
                        .OfType<X509Certificate2>()
                        .Where(IsCertificateAllowedForServerAuth)
                        .OrderByDescending(certificate => certificate.NotAfter)
                        .FirstOrDefault();

                    if (foundCertificate == null)
                    {
                        throw new InvalidOperationException($"The requested certificate {subject} could not be found in {storeLocation}/{storeName} with AllowInvalid setting: {allowInvalid}.");
                    }

                    return foundCertificate;
                }
                finally
                {
#if NET40 || NET451
                    store.Close();
#else
                    DisposeCertificates(storeCertificates, except: foundCertificate);
#endif
                }
            }
        }

        internal static bool IsCertificateAllowedForServerAuth(X509Certificate2 certificate)
        {
            /* If the Extended Key Usage extension is included, then we check that the serverAuth usage is included. (http://oid-info.com/get/1.3.6.1.5.5.7.3.1)
             * If the Extended Key Usage extension is not included, then we assume the certificate is allowed for all usages.
             *
             * See also https://blogs.msdn.microsoft.com/kaushal/2012/02/17/client-certificates-vs-server-certificates/
             *
             * From https://tools.ietf.org/html/rfc3280#section-4.2.1.13 "Certificate Extensions: Extended Key Usage"
             *
             * If the (Extended Key Usage) extension is present, then the certificate MUST only be used
             * for one of the purposes indicated.  If multiple purposes are
             * indicated the application need not recognize all purposes indicated,
             * as long as the intended purpose is present.  Certificate using
             * applications MAY require that a particular purpose be indicated in
             * order for the certificate to be acceptable to that application.
             */

            var hasEkuExtension = false;

            foreach (var extension in certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>())
            {
                hasEkuExtension = true;
                foreach (var oid in extension.EnhancedKeyUsages)
                {
                    if (string.Equals(oid.Value, ServerAuthenticationOid
#if NETCOREAPP_3_0_GREATER || NETSTANDARD_2_0_GREATER
                        ))
#else
                        , StringComparison.Ordinal))
#endif
                    {
                        return true;
                    }
                }
            }

            return !hasEkuExtension;
        }

#if !(NET40 || NET451)
        private static void DisposeCertificates(X509Certificate2Collection certificates, X509Certificate2 except)
        {
            if (certificates is object)
            {
                foreach (var certificate in certificates)
                {
                    if (!certificate.Equals(except))
                    {
                        certificate.Dispose();
                    }
                }
            }
        }
#endif
    }
}
