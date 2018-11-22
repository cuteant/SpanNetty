// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.ComponentModel;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;

    public interface IChannelUnsafe
    {
        /// <summary>
        /// Gets the assigned <see cref="IRecvByteBufAllocatorHandle"/> which will be used to allocate <see cref="IByteBuffer"/>'s when
        /// receiving data.
        /// </summary>
        IRecvByteBufAllocatorHandle RecvBufAllocHandle { get; }

        /// <summary>
        /// Register the <see cref="IChannel"/> and notify
        /// the <see cref="Task"/> once the registration was complete.
        /// </summary>
        /// <param name="eventLoop"></param>
        /// <returns></returns>
        Task RegisterAsync(IEventLoop eventLoop);

        void Deregister(IPromise promise);

        Task BindAsync(EndPoint localAddress);

        Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress);

        void Disconnect(IPromise promise);

        void Close(IPromise promise);

        void CloseForcibly();

        void BeginRead();

        void Write(object message, IPromise promise);

        void Flush();

        ChannelOutboundBuffer OutboundBuffer { get; }

        IPromise VoidPromise();

        [EditorBrowsable(EditorBrowsableState.Never)]
        void Initialize(IChannel channel);
    }
}