// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Net.Security;
    using System.Runtime.InteropServices;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using DotNetty.Transport.Channels;

    public sealed class ServerTlsSettings : TlsSettings
    {
        private static readonly SslProtocols s_defaultServerProtocol;
        static ServerTlsSettings()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                s_defaultServerProtocol = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;
            }
            else
            {
                s_defaultServerProtocol = SslProtocols.Tls12 | SslProtocols.Tls11;
            }
        }

        public ServerTlsSettings(X509Certificate certificate)
            : this(certificate, ClientCertificateMode.NoCertificate)
        {
        }

        public ServerTlsSettings(X509Certificate certificate, bool negotiateClientCertificate)
            : this(certificate, negotiateClientCertificate, false)
        {
        }

        public ServerTlsSettings(X509Certificate certificate, bool negotiateClientCertificate, bool checkCertificateRevocation)
            : this(certificate, negotiateClientCertificate, checkCertificateRevocation, s_defaultServerProtocol)
        {
        }

        public ServerTlsSettings(X509Certificate certificate, bool negotiateClientCertificate, bool checkCertificateRevocation, SslProtocols enabledProtocols)
          : base(enabledProtocols, checkCertificateRevocation)
        {
            Certificate = certificate;
            NegotiateClientCertificate = negotiateClientCertificate;
            ClientCertificateMode = negotiateClientCertificate ? ClientCertificateMode.AllowCertificate : ClientCertificateMode.NoCertificate;
        }

        public ServerTlsSettings(X509Certificate certificate, ClientCertificateMode clientCertificateMode)
            : this(certificate, clientCertificateMode, false)
        {
        }

        public ServerTlsSettings(X509Certificate certificate, ClientCertificateMode clientCertificateMode, bool checkCertificateRevocation)
            : this(certificate, clientCertificateMode, checkCertificateRevocation, s_defaultServerProtocol)
        {
        }

        public ServerTlsSettings(X509Certificate certificate, ClientCertificateMode clientCertificateMode, bool checkCertificateRevocation, SslProtocols enabledProtocols)
            : base(enabledProtocols, checkCertificateRevocation)
        {
            Certificate = certificate;
            NegotiateClientCertificate = clientCertificateMode != ClientCertificateMode.NoCertificate;
            ClientCertificateMode = clientCertificateMode;
        }

        /// <summary>
        /// <para>
        /// Specifies the server certificate used to authenticate Tls/Ssl connections. This is ignored if ServerCertificateSelector is set.
        /// </para>
        /// <para>
        /// If the server certificate has an Extended Key Usage extension, the usages must include Server Authentication (OID 1.3.6.1.5.5.7.3.1).
        /// </para>
        /// </summary>
        public X509Certificate Certificate { get; }

        internal readonly bool NegotiateClientCertificate;

        /// <summary>
        /// Specifies the client certificate requirements for a HTTPS connection. Defaults to <see cref="ClientCertificateMode.NoCertificate"/>.
        /// </summary>
        public ClientCertificateMode ClientCertificateMode { get; set; } = ClientCertificateMode.NoCertificate;

        /// <summary>
        /// Specifies a callback for additional client certificate validation that will be invoked during authentication.
        /// </summary>
        public Func<X509Certificate2, X509Chain, SslPolicyErrors, bool> ClientCertificateValidation { get; set; }

        /// <summary>
        /// <para>
        /// A callback that will be invoked to dynamically select a server certificate. This is higher priority than ServerCertificate.
        /// If SNI is not avialable then the name parameter will be null.
        /// </para>
        /// <para>
        /// If the server certificate has an Extended Key Usage extension, the usages must include Server Authentication (OID 1.3.6.1.5.5.7.3.1).
        /// </para>
        /// </summary>
        public Func<IChannelHandlerContext, string, X509Certificate2> ServerCertificateSelector { get; set; }

#if NETCOREAPP_2_0_GREATER || NETSTANDARD_2_0_GREATER
        public System.Collections.Generic.List<SslApplicationProtocol> ApplicationProtocols { get; set; }
#endif
    }
}