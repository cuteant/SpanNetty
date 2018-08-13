// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Bootstrapping
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Threading.Tasks;
    using CuteAnt.Pool;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// A <see cref="Bootstrap"/> that makes it easy to bootstrap an <see cref="IChannel"/> to use for clients.
    /// 
    /// The <see cref="AbstractBootstrap{TBootstrap,TChannel}.BindAsync(EndPoint)"/> methods are useful
    /// in combination with connectionless transports such as datagram (UDP). For regular TCP connections,
    /// please use the provided <see cref="ConnectAsync(EndPoint,EndPoint)"/> methods.
    /// </summary>
    public class Bootstrap : AbstractBootstrap<Bootstrap, IChannel>
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<Bootstrap>();

        static readonly INameResolver DefaultResolver = new DefaultNameResolver();

        volatile INameResolver resolver = DefaultResolver;
        volatile EndPoint remoteAddress;

        public Bootstrap()
        {
        }

        Bootstrap(Bootstrap bootstrap)
            : base(bootstrap)
        {
            this.resolver = bootstrap.resolver;
            this.remoteAddress = bootstrap.remoteAddress;
        }

        /// <summary>
        /// Sets the <see cref="INameResolver"/> which will resolve the address of the unresolved named address.
        /// </summary>
        /// <param name="resolver">The <see cref="INameResolver"/> which will resolve the address of the unresolved named address.</param>
        /// <returns>The <see cref="Bootstrap"/> instance.</returns>
        public Bootstrap Resolver(INameResolver resolver)
        {
            Contract.Requires(resolver != null);
            this.resolver = resolver;
            return this;
        }

        /// <summary>
        /// Assigns the remote <see cref="EndPoint"/> to connect to once the <see cref="ConnectAsync()"/> method is called.
        /// </summary>
        /// <param name="remoteAddress">The remote <see cref="EndPoint"/> to connect to.</param>
        /// <returns>The <see cref="Bootstrap"/> instance.</returns>
        public Bootstrap RemoteAddress(EndPoint remoteAddress)
        {
            this.remoteAddress = remoteAddress;
            return this;
        }

        /// <summary>
        /// Assigns the remote <see cref="EndPoint"/> to connect to once the <see cref="ConnectAsync()"/> method is called.
        /// </summary>
        /// <param name="inetHost">The hostname of the endpoint to connect to.</param>
        /// <param name="inetPort">The port at the remote host to connect to.</param>
        /// <returns>The <see cref="Bootstrap"/> instance.</returns>
        public Bootstrap RemoteAddress(string inetHost, int inetPort)
        {
            this.remoteAddress = new DnsEndPoint(inetHost, inetPort);
            return this;
        }

        /// <summary>
        /// Assigns the remote <see cref="EndPoint"/> to connect to once the <see cref="ConnectAsync()"/> method is called.
        /// </summary>
        /// <param name="inetHost">The <see cref="IPAddress"/> of the endpoint to connect to.</param>
        /// <param name="inetPort">The port at the remote host to connect to.</param>
        /// <returns>The <see cref="Bootstrap"/> instance.</returns>
        public Bootstrap RemoteAddress(IPAddress inetHost, int inetPort)
        {
            this.remoteAddress = new IPEndPoint(inetHost, inetPort);
            return this;
        }

        /// <summary>
        /// Connects an <see cref="IChannel"/> to the remote peer.
        /// </summary>
        /// <returns>The <see cref="IChannel"/>.</returns>
        public Task<IChannel> ConnectAsync()
        {
            this.Validate();
            EndPoint remoteAddress = this.remoteAddress;
            if (remoteAddress == null)
            {
                ThrowHelper.ThrowInvalidOperationException_RemoteAddrNotSet();
            }

            return this.DoResolveAndConnectAsync(remoteAddress, this.LocalAddress());
        }

        /// <summary>
        /// Connects an <see cref="IChannel"/> to the remote peer.
        /// </summary>
        /// <param name="inetHost">The hostname of the endpoint to connect to.</param>
        /// <param name="inetPort">The port at the remote host to connect to.</param>
        /// <returns>The <see cref="IChannel"/>.</returns>
        public Task<IChannel> ConnectAsync(string inetHost, int inetPort) => this.ConnectAsync(new DnsEndPoint(inetHost, inetPort));

        /// <summary>
        /// Connects an <see cref="IChannel"/> to the remote peer.
        /// </summary>
        /// <param name="inetHost">The <see cref="IPAddress"/> of the endpoint to connect to.</param>
        /// <param name="inetPort">The port at the remote host to connect to.</param>
        /// <returns>The <see cref="IChannel"/>.</returns>
        public Task<IChannel> ConnectAsync(IPAddress inetHost, int inetPort) => this.ConnectAsync(new IPEndPoint(inetHost, inetPort));

        /// <summary>
        /// Connects an <see cref="IChannel"/> to the remote peer.
        /// </summary>
        /// <param name="remoteAddress">The remote <see cref="EndPoint"/> to connect to.</param>
        /// <returns>The <see cref="IChannel"/>.</returns>
        public Task<IChannel> ConnectAsync(EndPoint remoteAddress)
        {
            Contract.Requires(remoteAddress != null);

            this.Validate();
            return this.DoResolveAndConnectAsync(remoteAddress, this.LocalAddress());
        }

        /// <summary>
        /// Connects an <see cref="IChannel"/> to the remote peer.
        /// </summary>
        /// <param name="remoteAddress">The remote <see cref="EndPoint"/> to connect to.</param>
        /// <param name="localAddress">The local <see cref="EndPoint"/> to connect to.</param>
        /// <returns>The <see cref="IChannel"/>.</returns>
        public Task<IChannel> ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            Contract.Requires(remoteAddress != null);

            this.Validate();
            return this.DoResolveAndConnectAsync(remoteAddress, localAddress);
        }

        /// <summary>
        /// Performs DNS resolution for the remote endpoint and connects to it.
        /// </summary>
        /// <param name="remoteAddress">The remote <see cref="EndPoint"/> to connect to.</param>
        /// <param name="localAddress">The local <see cref="EndPoint"/> to connect the remote to.</param>
        /// <returns>The <see cref="IChannel"/>.</returns>
        async Task<IChannel> DoResolveAndConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
        {
            IChannel channel = await this.InitAndRegisterAsync();

            if (this.resolver.IsResolved(remoteAddress))
            {
                // Resolver has no idea about what to do with the specified remote address or it's resolved already.
                await DoConnectAsync(channel, remoteAddress, localAddress);
                return channel;
            }

            EndPoint resolvedAddress;
            try
            {
                resolvedAddress = await this.resolver.ResolveAsync(remoteAddress);
            }
            catch (Exception)
            {
                try
                {
                    await channel.CloseAsync();
                }
                catch (Exception ex)
                {
                    if (Logger.WarnEnabled) Logger.FailedToCloseChannel(channel, ex);
                }

                throw;
            }

            await DoConnectAsync(channel, resolvedAddress, localAddress);
            return channel;
        }

        static Task DoConnectAsync(IChannel channel,
            EndPoint remoteAddress, EndPoint localAddress)
        {
            // This method is invoked before channelRegistered() is triggered.  Give user handlers a chance to set up
            // the pipeline in its channelRegistered() implementation.
            var promise = new TaskCompletionSource();
            channel.EventLoop.Execute(() =>
            {
                try
                {
                    if (localAddress == null)
                    {
                        channel.ConnectAsync(remoteAddress).LinkOutcome(promise);
                    }
                    else
                    {
                        channel.ConnectAsync(remoteAddress, localAddress).LinkOutcome(promise);
                    }
                }
                catch (Exception ex)
                {
                    channel.CloseSafe();
                    promise.TrySetException(ex);
                }
            });
            return promise.Task;
        }

        protected override void Init(IChannel channel)
        {
            IChannelPipeline p = channel.Pipeline;
            p.AddLast(null, (string)null, this.Handler());

            ICollection<ChannelOptionValue> options = this.Options;
            SetChannelOptions(channel, options, Logger);

            ICollection<AttributeValue> attrs = this.Attributes;
            foreach (AttributeValue e in attrs)
            {
                e.Set(channel);
            }
        }

        public override Bootstrap Validate()
        {
            base.Validate();
            if (this.Handler() == null)
            {
                ThrowHelper.ThrowInvalidOperationException_HandlerNotSet();
            }
            return this;
        }

        public override Bootstrap Clone() => new Bootstrap(this);

        /// <summary>
        /// Returns a deep clone of this bootstrap which has the identical configuration except that it uses
        /// the given <see cref="IEventLoopGroup"/>. This method is useful when making multiple <see cref="IChannel"/>s with similar
        /// settings.
        /// </summary>
        public Bootstrap Clone(IEventLoopGroup group)
        {
            var bs = new Bootstrap(this);
            bs.Group(group);
            return bs;
        }

        public override string ToString()
        {
            if (this.remoteAddress == null)
            {
                return base.ToString();
            }

            var buf = StringBuilderManager.Allocate().Append(base.ToString());
            buf.Length = buf.Length - 1;

            return StringBuilderManager.ReturnAndFree(buf.Append(", remoteAddress: ")
                .Append(this.remoteAddress)
                .Append(')'));
        }
    }
}