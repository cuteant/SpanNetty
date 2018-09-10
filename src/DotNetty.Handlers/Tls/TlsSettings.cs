// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System.Security.Authentication;

    public abstract class TlsSettings
    {
        protected TlsSettings(SslProtocols enabledProtocols, bool checkCertificateRevocation)
        {
            this.EnabledProtocols = enabledProtocols;
            this.CheckCertificateRevocation = checkCertificateRevocation;
        }

        /// <summary>Specifies allowable SSL protocols.</summary>
        public SslProtocols EnabledProtocols { get; }

        /// <summary>Specifies whether the certificate revocation list is checked during authentication.</summary>
        public bool CheckCertificateRevocation { get; }
    }
}