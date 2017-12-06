
namespace DotNetty.Handlers.Tls
{
  using System;
  using System.IO;
  using System.Linq;
  using System.Net;
  using System.Net.Security;
  using System.Security.Authentication;
  using System.Security.Cryptography.X509Certificates;
  using System.Threading.Tasks;

  partial class TlsHandler
  {
    // See http://oid-info.com/get/1.3.6.1.5.5.7.3.1
    // Indicates that a certificate can be used as a SSL server certificate
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

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
      if (settings is ServerTlsSettings serverSettings)
      {
        EnsureCertificateIsAllowedForServerAuth(ConvertToX509Certificate2(serverSettings.Certificate));

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

        return new SslStream(stream,
            leaveInnerStreamOpen: true,
            userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) =>
            {
              if (certificate == null)
              {
                return serverSettings.ClientCertificateMode != ClientCertificateMode.RequireCertificate;
              }

              if (serverSettings.ClientCertificateValidation == null)
              {
                if (sslPolicyErrors != SslPolicyErrors.None) { return false; }
              }

              var certificate2 = ConvertToX509Certificate2(certificate);
              if (certificate2 == null) { return false; }

              if (serverSettings.ClientCertificateValidation != null)
              {
                if (!serverSettings.ClientCertificateValidation(certificate2, chain, sslPolicyErrors))
                {
                  return false;
                }
              }

              // Return the native validation result
              //return sslPolicyErrors == SslPolicyErrors.None;
              return true;
            });
      }
      else
      {
        var clientSettings = (ClientTlsSettings)settings;
        return new SslStream(stream,
            leaveInnerStreamOpen: true,
            userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) =>
            {
              if (clientSettings.ServerCertificateValidation != null)
              {
                if (!clientSettings.ServerCertificateValidation(certificate, chain, sslPolicyErrors))
                {
                  return false;
                }
              }
              var callback = ServicePointManager.ServerCertificateValidationCallback;
              if (callback != null) { return callback(sender, certificate, chain, sslPolicyErrors); }

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
                //OnError(new Exception(sslPolicyErrors.ToString()));
                s_logger.Warn(sslPolicyErrors.ToString());
                return false;
              }

              // not only a remote certificate error
              if (sslPolicyErrors != SslPolicyErrors.None && sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
              {
                //OnError(new Exception(sslPolicyErrors.ToString()));
                s_logger.Warn(sslPolicyErrors.ToString());
                return false;
              }

              if (chain != null && chain.ChainStatus != null)
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
                      //OnError(new Exception(sslPolicyErrors.ToString()));
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
            });
      }
    }

    private static void EnsureCertificateIsAllowedForServerAuth(X509Certificate2 certificate)
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
          if (oid.Value.Equals(ServerAuthenticationOid, StringComparison.Ordinal))
          {
            return;
          }
        }
      }

      if (hasEkuExtension)
      {
        throw new InvalidOperationException(
          $"Certificate {certificate.Thumbprint} cannot be used as an SSL server certificate. It has an Extended Key Usage extension but the usages do not include Server Authentication (OID 1.3.6.1.5.5.7.3.1).");
      }
    }

    private static X509Certificate2 ConvertToX509Certificate2(X509Certificate certificate)
    {
      if (null == certificate) { return null; }

      if (certificate is X509Certificate2 cert2) { return cert2; }

      return new X509Certificate2(certificate);
    }

    private static void ObserveTaskException(Task task)
    {
      _ = task.ContinueWith(t =>
      {
        _ = t.Exception;
      });
    }
  }
}
