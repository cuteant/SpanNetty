
namespace DotNetty.Handlers.Tls
{
  /// <summary>Describes the client certificate requirements for a HTTPS connection.</summary>
  public enum ClientCertificateMode
  {
    /// <summary>A client certificate is not required and will not be requested from clients.</summary>
    NoCertificate,

    /// <summary>A client certificate will be requested; however,
    /// authentication will not fail if a certificate is not provided by the client.</summary>
    AllowCertificate,

    /// <summary>A client certificate will be requested, and the client must provide a valid certificate for authentication to succeed.</summary>
    RequireCertificate
  }
}
