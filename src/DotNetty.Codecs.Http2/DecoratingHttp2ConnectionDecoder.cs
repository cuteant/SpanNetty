// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    /// <summary>
    /// Decorator around another <see cref="IHttp2ConnectionDecoder"/> instance.
    /// </summary>
    public class DecoratingHttp2ConnectionDecoder : IHttp2ConnectionDecoder
    {
        readonly IHttp2ConnectionDecoder innerDecoder;

        public DecoratingHttp2ConnectionDecoder(IHttp2ConnectionDecoder decoder)
        {
            if (null == decoder) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.decoder); }

            this.innerDecoder = decoder;
        }

        public IHttp2Connection Connection => this.innerDecoder.Connection;

        public IHttp2LocalFlowController FlowController => this.innerDecoder.FlowController;

        public IHttp2FrameListener FrameListener { get => this.innerDecoder.FrameListener; set => this.innerDecoder.FrameListener = value; }

        public Http2Settings LocalSettings => this.innerDecoder.LocalSettings;


        public virtual void Close()
        {
            this.innerDecoder.Close();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose() => this.Close();

        public virtual void DecodeFrame(IChannelHandlerContext ctx, IByteBuffer input, List<object> output)
        {
            this.innerDecoder.DecodeFrame(ctx, input, output);
        }

        public virtual void LifecycleManager(IHttp2LifecycleManager lifecycleManager)
        {
            this.innerDecoder.LifecycleManager(lifecycleManager);
        }

        public virtual bool PrefaceReceived => this.innerDecoder.PrefaceReceived;
    }
}
