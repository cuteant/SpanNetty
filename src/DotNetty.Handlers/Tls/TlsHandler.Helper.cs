/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;
    using System.Security.Cryptography.X509Certificates;
    using DotNetty.Buffers;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;

    partial class TlsHandler
    {
        private static readonly IInternalLogger s_logger = InternalLoggerFactory.GetInstance<TlsHandler>();
        private static readonly Exception s_sslStreamClosedException = new IOException("SSLStream closed already");

        public static TlsHandler Client(string targetHost) => new TlsHandler(new ClientTlsSettings(targetHost));

        public static TlsHandler Client(string targetHost, X509Certificate clientCertificate) => new TlsHandler(new ClientTlsSettings(targetHost, new List<X509Certificate> { clientCertificate }));

        public static TlsHandler Server(X509Certificate certificate) => new TlsHandler(new ServerTlsSettings(certificate));

        private static SslStream CreateSslStream(TlsSettings settings, Stream stream)
        {
            if (settings is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.settings); }

            if (settings is ServerTlsSettings serverSettings)
            {
                // Enable client certificate function only if ClientCertificateRequired is true in the configuration
                if (serverSettings.ClientCertificateMode == ClientCertificateMode.NoCertificate)
                {
                    return new SslStream(stream, leaveInnerStreamOpen: true);
                }

#if NETFRAMEWORK
                // SSL 版本 2 协议不支持客户端证书
                if (serverSettings.EnabledProtocols == System.Security.Authentication.SslProtocols.Ssl2)
                {
                    return new SslStream(stream, leaveInnerStreamOpen: true);
                }
#endif

                return new SslStream(stream,
                    leaveInnerStreamOpen: true,
                    userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) => ClientCertificateValidation(certificate, chain, sslPolicyErrors, serverSettings));
            }
            else if (settings is ClientTlsSettings clientSettings)
            {
                return new SslStream(stream,
                    leaveInnerStreamOpen: true,
                    userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) => ServerCertificateValidation(sender, certificate, chain, sslPolicyErrors, clientSettings)
#if !(NETCOREAPP_2_0_GREATER || NETSTANDARD_2_0_GREATER)
                    , userCertificateSelectionCallback: clientSettings.UserCertSelector is null ? null : new LocalCertificateSelectionCallback((sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) =>
                    {
                        return clientSettings.UserCertSelector(sender as SslStream, targetHost, localCertificates, remoteCertificate, acceptableIssuers);
                    })
#endif
                    );
            }
            else
            {
                return new SslStream(stream, leaveInnerStreamOpen: true);
            }
        }

        private static bool ClientCertificateValidation(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors, ServerTlsSettings serverSettings)
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

        /// <summary>Validates the remote certificate.</summary>
        /// <remarks>Code take from SuperSocket.ClientEngine(See https://github.com/kerryjiang/SuperSocket.ClientEngine/blob/b46a0ededbd6249f4e28b8d77f55dea3fa23283e/Core/SslStreamTcpSession.cs#L101). </remarks>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <param name="clientSettings"></param>
        /// <returns></returns>
        private static bool ServerCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors, ClientTlsSettings clientSettings)
        {
            var certificateValidation = clientSettings.ServerCertificateValidation;
            if (certificateValidation is object) { return certificateValidation(certificate, chain, sslPolicyErrors); }

            var callback = ServicePointManager.ServerCertificateValidationCallback;
            if (callback is object) { return callback(sender, certificate, chain, sslPolicyErrors); }

            if (sslPolicyErrors == SslPolicyErrors.None) { return true; }

            if (clientSettings.AllowNameMismatchCertificate)
            {
                sslPolicyErrors &= (~SslPolicyErrors.RemoteCertificateNameMismatch);
            }

            if (clientSettings.AllowCertificateChainErrors)
            {
                sslPolicyErrors &= (~SslPolicyErrors.RemoteCertificateChainErrors);
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

        [MethodImpl(InlineMethod.AggressiveInlining)]
        private static X509Certificate2 ConvertToX509Certificate2(X509Certificate certificate) => certificate switch
        {
            null => null,
            X509Certificate2 cert2 => cert2,
            _ => new X509Certificate2(certificate),
        };

        /// <summary>
        /// Each call to SSL_write will introduce about ~100 bytes of overhead. This coalescing queue attempts to increase
        /// goodput by aggregating the plaintext in chunks of <see cref="v_wrapDataSize"/>. If many small chunks are written
        /// this can increase goodput, decrease the amount of calls to SSL_write, and decrease overall encryption operations.
        /// </summary>
        private sealed class SslHandlerCoalescingBufferQueue : AbstractCoalescingBufferQueue
        {
            private readonly TlsHandler _owner;

            public SslHandlerCoalescingBufferQueue(TlsHandler owner, IChannel channel, int initSize)
                : base(channel, initSize)
            {
                _owner = owner;
            }

            protected override IByteBuffer Compose(IByteBufferAllocator alloc, IByteBuffer cumulation, IByteBuffer next)
            {
                int wrapDataSize = _owner.v_wrapDataSize;
                if (cumulation is CompositeByteBuffer composite)
                {
                    int numComponents = composite.NumComponents;
                    if (0u >= (uint)numComponents ||
                        !AttemptCopyToCumulation(composite.InternalComponent(numComponents - 1), next, wrapDataSize))
                    {
                        composite.AddComponent(true, next);
                    }
                    return composite;
                }
                return AttemptCopyToCumulation(cumulation, next, wrapDataSize)
                    ? cumulation
                    : CopyAndCompose(alloc, cumulation, next);
            }

            protected override IByteBuffer ComposeFirst(IByteBufferAllocator allocator, IByteBuffer first)
            {
                if (first is CompositeByteBuffer composite)
                {
                    first = allocator.DirectBuffer(composite.ReadableBytes);
                    try
                    {
                        first.WriteBytes(composite);
                    }
                    catch (Exception cause)
                    {
                        first.Release();
                        ExceptionDispatchInfo.Capture(cause).Throw();
                    }
                    composite.Release();
                }
                return first;
            }

            protected override IByteBuffer RemoveEmptyValue()
            {
                return null;
            }

            private static bool AttemptCopyToCumulation(IByteBuffer cumulation, IByteBuffer next, int wrapDataSize)
            {
                int inReadableBytes = next.ReadableBytes;
                int cumulationCapacity = cumulation.Capacity;
                if (wrapDataSize - cumulation.ReadableBytes >= inReadableBytes &&
                    // Avoid using the same buffer if next's data would make cumulation exceed the wrapDataSize.
                    // Only copy if there is enough space available and the capacity is large enough, and attempt to
                    // resize if the capacity is small.
                    ((cumulation.IsWritable(inReadableBytes) && cumulationCapacity >= wrapDataSize) ||
                    (cumulationCapacity < wrapDataSize && ByteBufferUtil.EnsureWritableSuccess(cumulation.EnsureWritable(inReadableBytes, false)))))
                {
                    cumulation.WriteBytes(next);
                    next.Release();
                    return true;
                }
                return false;
            }
        }

#if !DESKTOPCLR && (NET45 || NET451 || NET46 || NET461 || NET462 || NET47 || NET471 || NET472)
#error 确保编译不出问题
#endif
#if !NETSTANDARD && (NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETSTANDARD2_0 || NETSTANDARD2_1)
#error 确保编译不出问题
#endif
    }
}
