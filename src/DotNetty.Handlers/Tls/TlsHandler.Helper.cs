
namespace DotNetty.Handlers.Tls
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography.X509Certificates;
    using DotNetty.Common.Internal.Logging;

    partial class TlsHandler
    {
        private static readonly IInternalLogger s_logger = InternalLoggerFactory.GetInstance<TlsHandler>();

        public static TlsHandler Client(string targetHost) => new TlsHandler(new ClientTlsSettings(targetHost));

        public static TlsHandler Client(string targetHost, X509Certificate clientCertificate) => new TlsHandler(new ClientTlsSettings(targetHost, new List<X509Certificate> { clientCertificate }));

        public static TlsHandler Server(X509Certificate certificate) => new TlsHandler(new ServerTlsSettings(certificate));

        private static SslStream CreateSslStream(TlsSettings settings, Stream stream)
        {
            if (settings is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.settings); }

            return new SslStream(stream, true);
        }

        [MethodImpl(InlineMethod.AggressiveInlining)]
        private static X509Certificate2 ConvertToX509Certificate2(X509Certificate certificate) => certificate switch
        {
            null => null,
            X509Certificate2 cert2 => cert2,
            _ => new X509Certificate2(certificate),
        };

#if !DESKTOPCLR && (NET45 || NET451 || NET46 || NET461 || NET462 || NET47 || NET471 || NET472)
  确保编译不出问题
#endif
#if !NETSTANDARD && (NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETSTANDARD2_0 || NETSTANDARD2_1)
  确保编译不出问题
#endif
    }
}
