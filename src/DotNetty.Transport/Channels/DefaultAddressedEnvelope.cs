// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System.Net;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    public class DefaultAddressedEnvelope<T> : IAddressedEnvelope<T>
    {
        public DefaultAddressedEnvelope(T content, EndPoint recipient)
            : this(content, null, recipient)
        {
        }

        public DefaultAddressedEnvelope(T content, EndPoint sender, EndPoint recipient)
        {
            if (content is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.content); }
            if (recipient is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.recipient); }

            Content = content;
            Sender = sender;
            Recipient = recipient;
        }

        public T Content { get; }

        public EndPoint Sender { get; }

        public EndPoint Recipient { get; }

        public int ReferenceCount
        {
            get
            {
                var counted = Content as IReferenceCounted;
                return counted?.ReferenceCount ?? 1;
            }
        }

        public virtual IReferenceCounted Retain()
        {
            _ = ReferenceCountUtil.Retain(Content);
            return this;
        }

        public virtual IReferenceCounted Retain(int increment)
        {
            _ = ReferenceCountUtil.Retain(Content, increment);
            return this;
        }

        public virtual IReferenceCounted Touch()
        {
            _ = ReferenceCountUtil.Touch(Content);
            return this;
        }

        public virtual IReferenceCounted Touch(object hint)
        {
            _ = ReferenceCountUtil.Touch(Content, hint);
            return this;
        }

        public bool Release() => ReferenceCountUtil.Release(Content);

        public bool Release(int decrement) => ReferenceCountUtil.Release(Content, decrement);

        public override string ToString() => $"DefaultAddressedEnvelope<{typeof(T)}>"
            + (Sender is object
                ? $"({Sender} => {Recipient}, {Content})"
                : $"(=> {Recipient}, {Content})");
    }
}