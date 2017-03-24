// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
  using System.Collections.Generic;
  using System.Linq;
  using System.Security.Authentication;
  using System.Security.Cryptography.X509Certificates;

  public sealed class ClientTlsSettings : TlsSettings
  {
#if NET_4_0_GREATER
    IReadOnlyCollection<X509Certificate2> certificates;
#else
    IList<X509Certificate2> certificates;
#endif

    public ClientTlsSettings(string targetHost)
        : this(targetHost, new List<X509Certificate>())
    {
    }

    public ClientTlsSettings(string targetHost, List<X509Certificate> certificates)
        : this(false, certificates, targetHost)
    {
    }

    public ClientTlsSettings(bool checkCertificateRevocation, List<X509Certificate> certificates, string targetHost)
#if NET_4_0_GREATER
      : this(SslProtocols.Ssl3 | SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, checkCertificateRevocation, certificates, targetHost)
#else
      : this(SslProtocols.Default, checkCertificateRevocation, certificates, targetHost)
#endif
    {
    }

    public ClientTlsSettings(SslProtocols enabledProtocols, bool checkCertificateRevocation, List<X509Certificate> certificates, string targetHost)
        : base(enabledProtocols, checkCertificateRevocation)
    {
      this.X509CertificateCollection = new X509CertificateCollection(certificates.ToArray());
      this.TargetHost = targetHost;
      this.Certificates = certificates.AsReadOnly();
    }

    internal X509CertificateCollection X509CertificateCollection { get; set; }

#if NET_4_0_GREATER
    public IReadOnlyCollection<X509Certificate> Certificates { get; }
#else
    public IList<X509Certificate> Certificates { get; }
#endif

    public string TargetHost { get; }
  }
}