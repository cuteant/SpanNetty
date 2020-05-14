// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Collections.Generic;
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using DotNetty.Transport.Channels;
#if NET40
    using System.Collections.ObjectModel;
#endif

    public sealed class ClientTlsSettings : TlsSettings
    {
        public ClientTlsSettings(string targetHost)
          : this(targetHost, new List<X509Certificate>())
        {
        }

        public ClientTlsSettings(string targetHost, List<X509Certificate> certificates)
          : this(false, certificates, targetHost)
        {
        }

        public ClientTlsSettings(bool checkCertificateRevocation, List<X509Certificate> certificates, string targetHost)
          : this(
#if NET40
            SslProtocols.Default
#else
//#if NETCOREAPP_3_0_GREATER
//            SslProtocols.Tls13 |
//#endif
            SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls
#endif
            , checkCertificateRevocation, certificates, targetHost)
        {
        }

        public ClientTlsSettings(SslProtocols enabledProtocols, bool checkCertificateRevocation, List<X509Certificate> certificates, string targetHost)
          : base(enabledProtocols, checkCertificateRevocation)
        {
            X509CertificateCollection = new X509CertificateCollection(certificates.ToArray());
            TargetHost = targetHost;
            Certificates = certificates.AsReadOnly();
        }

        internal X509CertificateCollection X509CertificateCollection { get; set; }

#if NET40
        public ReadOnlyCollection<X509Certificate> Certificates { get; }
#else
        public IReadOnlyCollection<X509Certificate> Certificates { get; }
#endif

        public string TargetHost { get; }

        /// <summary>Whether allow untrusted certificate</summary>
        public bool AllowUnstrustedCertificate { get; set; }

        /// <summary>Whether allow the certificate whose name doesn't match current remote endpoint's host name</summary>
        public bool AllowNameMismatchCertificate { get; set; }

        /// <summary>Whether allow the certificate chain errors</summary>
        public bool AllowCertificateChainErrors { get; set; }

        public Func<X509Certificate, X509Chain, SslPolicyErrors, bool> ServerCertificateValidation { get; set; }

#if NETCOREAPP_2_0_GREATER || NETSTANDARD_2_0_GREATER
        public System.Collections.Generic.List<SslApplicationProtocol> ApplicationProtocols { get; set; }

        public Func<IChannelHandlerContext, string, X509CertificateCollection, X509Certificate, string[], X509Certificate2> UserCertSelector { get; set; }
#else
        public Func<SslStream, string, X509CertificateCollection, X509Certificate, string[], X509Certificate2> UserCertSelector { get; set; }
#endif
    }
}