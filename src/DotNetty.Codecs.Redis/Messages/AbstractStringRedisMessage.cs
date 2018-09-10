// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Redis.Messages
{
    using System.Text;
    using DotNetty.Common.Utilities;

    public abstract class AbstractStringRedisMessage : IRedisMessage
    {
        protected AbstractStringRedisMessage(string content)
        {
            if (null == content) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.content); }

            this.Content = content;
        }

        public string Content { get; }

        public override string ToString() =>
            new StringBuilder(StringUtil.SimpleClassName(this))
                .Append('[')
                .Append("content=")
                .Append(this.Content)
                .Append(']')
                .ToString();
    }
}