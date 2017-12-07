// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
  using System;
  using System.Net.Security;
  using System.Security.Authentication;
  using System.Security.Cryptography.X509Certificates;

  public sealed class ServerTlsSettings : TlsSettings
  {
    public ServerTlsSettings(X509Certificate certificate)
      : this(certificate, false)
    {
    }

    public ServerTlsSettings(X509Certificate certificate, bool negotiateClientCertificate)
      : this(certificate, negotiateClientCertificate, false)
    {
    }

    public ServerTlsSettings(X509Certificate certificate, bool negotiateClientCertificate, bool checkCertificateRevocation)
      : this(certificate, negotiateClientCertificate, checkCertificateRevocation,
#if NET40
          SslProtocols.Default)
#elif DESKTOPCLR
          SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12)
#else
#if DEBUG
          SslProtocols.Tls12 | SslProtocols.Tls11)  // 注意 xunit.2.3.1 tls1.0测试无法通过
#else
          SslProtocols.Tls | SslProtocols.Tls12 | SslProtocols.Tls11)
#endif
#endif
    {
    }

    public ServerTlsSettings(X509Certificate certificate, bool negotiateClientCertificate, bool checkCertificateRevocation, SslProtocols enabledProtocols)
      : base(enabledProtocols, checkCertificateRevocation)
    {
      this.Certificate = certificate;
      this.NegotiateClientCertificate = negotiateClientCertificate;
      ClientCertificateMode = negotiateClientCertificate ? ClientCertificateMode.AllowCertificate : ClientCertificateMode.NoCertificate;
    }

    public X509Certificate Certificate { get; }

    internal bool NegotiateClientCertificate { get; }

    public ClientCertificateMode ClientCertificateMode { get; set; } = ClientCertificateMode.NoCertificate;

    public Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> ClientCertificateValidation { get; set; }
  }
}