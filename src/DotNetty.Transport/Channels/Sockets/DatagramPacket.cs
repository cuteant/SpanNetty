// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System.Net;
    using DotNetty.Buffers;
    using DotNetty.Common;

    public sealed class DatagramPacket : DefaultAddressedEnvelope<IByteBuffer>, IByteBufferHolder
    {
        public DatagramPacket(IByteBuffer message, EndPoint recipient)
            : base(message, recipient)
        {
        }

        public DatagramPacket(IByteBuffer message, EndPoint sender, EndPoint recipient)
            : base(message, sender, recipient)
        {
        }

        public IByteBufferHolder Copy() => new DatagramPacket(Content.Copy(), Sender, Recipient);

        public IByteBufferHolder Duplicate() => new DatagramPacket(Content.Duplicate(), Sender, Recipient);

        public IByteBufferHolder RetainedDuplicate() => Replace(Content.RetainedDuplicate());

        public IByteBufferHolder Replace(IByteBuffer content) => new DatagramPacket(content, Recipient, Sender);

        public override IReferenceCounted Retain()
        {
            _ = base.Retain();
            return this;
        }

        public override IReferenceCounted Retain(int increment)
        {
            _ = base.Retain(increment);
            return this;
        }

        public override IReferenceCounted Touch()
        {
            _ = base.Touch();
            return this;
        }

        public override IReferenceCounted Touch(object hint)
        {
            _ = base.Touch(hint);
            return this;
        }
    }
}