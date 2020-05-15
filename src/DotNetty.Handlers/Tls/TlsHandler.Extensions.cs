
namespace DotNetty.Handlers.Tls
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Runtime.CompilerServices;
#if DESKTOPCLR
    using System.Security.Authentication;
#endif

    partial class TlsHandler
    {
        public TlsHandler(TlsSettings settings)
          : this(stream => CreateSslStream(settings, stream), settings)
        {
        }

        public TlsHandler(Func<TlsSettings, Stream, SslStream> sslStreamFactory, TlsSettings settings)
          : this(stream => sslStreamFactory(settings, stream), settings)
        {
        }

        private static SslStream CreateSslStream(TlsSettings settings, Stream stream)
        {
            if (settings is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.settings); }

            if (settings is ServerTlsSettings serverSettings)
            {
                // Enable client certificate function only if ClientCertificateRequired is true in the configuration
                if (serverSettings.ClientCertificateMode == ClientCertificateMode.NoCertificate)
                {
                    return new SslStream(stream, true);
                }

#if DESKTOPCLR
                // SSL 版本 2 协议不支持客户端证书
                if (serverSettings.EnabledProtocols == SslProtocols.Ssl2)
                {
                    return new SslStream(stream, true);
                }
#endif

                bool LocalClientCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
                {
                    if (certificate is null)
                    {
                        return serverSettings.ClientCertificateMode != ClientCertificateMode.RequireCertificate;
                    }

                    var clientCertificateValidationFunc = serverSettings.ClientCertificateValidation;
                    if (clientCertificateValidationFunc is null)
                    {
                        if (sslPolicyErrors != SslPolicyErrors.None) { return false; }
                    }

                    var certificate2 = ConvertToX509Certificate2(certificate);
                    if (certificate2 is null) { return false; }

                    if (clientCertificateValidationFunc is object)
                    {
                        if (!clientCertificateValidationFunc(certificate2, chain, sslPolicyErrors))
                        {
                            return false;
                        }
                    }

                    return true;
                }
                return new SslStream(stream,
                    leaveInnerStreamOpen: true,
                    userCertificateValidationCallback: new RemoteCertificateValidationCallback(LocalClientCertificateValidation));
            }
            else
            {
                var clientSettings = (ClientTlsSettings)settings;
                bool LocalServerCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
                {
                    var certificateValidation = clientSettings.ServerCertificateValidation;
                    if (certificateValidation is object) { return certificateValidation(certificate, chain, sslPolicyErrors); }

                    var callback = ServicePointManager.ServerCertificateValidationCallback;
                    if (callback is object) { return callback(sender, certificate, chain, sslPolicyErrors); }

                    if (sslPolicyErrors == SslPolicyErrors.None) { return true; }

                    if (clientSettings.AllowNameMismatchCertificate)
                    {
                        sslPolicyErrors = sslPolicyErrors & (~SslPolicyErrors.RemoteCertificateNameMismatch);
                    }

                    if (clientSettings.AllowCertificateChainErrors)
                    {
                        sslPolicyErrors = sslPolicyErrors & (~SslPolicyErrors.RemoteCertificateChainErrors);
                    }

                    if (sslPolicyErrors == SslPolicyErrors.None) { return true; }

                    if (!clientSettings.AllowUnstrustedCertificate)
                    {
                        s_logger.Warn(sslPolicyErrors.ToString());
                        return false;
                    }

                    // not only a remote certificate error
                    if (sslPolicyErrors != SslPolicyErrors.None && sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
                    {
                        s_logger.Warn(sslPolicyErrors.ToString());
                        return false;
                    }

                    if (chain is object && chain.ChainStatus is object)
                    {
                        foreach (X509ChainStatus status in chain.ChainStatus)
                        {
                            if ((certificate.Subject == certificate.Issuer) &&
                                (status.Status == X509ChainStatusFlags.UntrustedRoot))
                            {
                                // Self-signed certificates with an untrusted root are valid. 
                                continue;
                            }
                            else
                            {
                                if (status.Status != X509ChainStatusFlags.NoError)
                                {
                                    s_logger.Warn(sslPolicyErrors.ToString());
                                    // If there are any other errors in the certificate chain, the certificate is invalid,
                                    // so the method returns false.
                                    return false;
                                }
                            }
                        }
                    }

                    // When processing reaches this line, the only errors in the certificate chain are 
                    // untrusted root errors for self-signed certificates. These certificates are valid
                    // for default Exchange server installations, so return true.
                    return true;
                }
                return new SslStream(stream,
                    leaveInnerStreamOpen: true,
                    userCertificateValidationCallback: new RemoteCertificateValidationCallback(LocalServerCertificateValidation)
#if !(NETCOREAPP_2_0_GREATER || NETSTANDARD_2_0_GREATER)
                    , userCertificateSelectionCallback: clientSettings.UserCertSelector is null ? null : new LocalCertificateSelectionCallback((sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) =>
                    {
                        return clientSettings.UserCertSelector(sender as SslStream, targetHost, localCertificates, remoteCertificate, acceptableIssuers);
                    })
#endif
                    );
            }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        private static void EnsureCertificateIsAllowedForServerAuth(X509Certificate2 certificate)
        {
            if (!CertificateLoader.IsCertificateAllowedForServerAuth(certificate))
            {
                ThrowHelper.ThrowInvalidOperationException_InvalidServerCertificateEku(certificate);
            }
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        private static X509Certificate2 ConvertToX509Certificate2(X509Certificate certificate)
        {
            return certificate switch
            {
                null => null,
                X509Certificate2 cert2 => cert2,
                _ => new X509Certificate2(certificate),
            };
        }

#if !DESKTOPCLR && (NET45 || NET451 || NET46 || NET461 || NET462 || NET47 || NET471 || NET472)
  确保编译不出问题
#endif
#if !NETSTANDARD && (NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETSTANDARD2_0 || NETSTANDARD2_1)
  确保编译不出问题
#endif
    }
}
