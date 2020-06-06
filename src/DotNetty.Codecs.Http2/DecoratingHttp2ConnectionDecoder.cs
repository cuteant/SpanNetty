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
        readonly IHttp2ConnectionDecoder _innerDecoder;

        public DecoratingHttp2ConnectionDecoder(IHttp2ConnectionDecoder decoder)
        {
            if (decoder is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.decoder); }

            _innerDecoder = decoder;
        }

        public IHttp2Connection Connection => _innerDecoder.Connection;

        public IHttp2LocalFlowController FlowController => _innerDecoder.FlowController;

        public virtual IHttp2FrameListener FrameListener { get => _innerDecoder.FrameListener; set => _innerDecoder.FrameListener = value; }

        public Http2Settings LocalSettings => _innerDecoder.LocalSettings;


        public virtual void Close()
        {
            _innerDecoder.Close();
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose() => Close();

        public virtual void DecodeFrame(IChannelHandlerContext ctx, IByteBuffer input, List<object> output)
        {
            _innerDecoder.DecodeFrame(ctx, input, output);
        }

        public virtual void LifecycleManager(IHttp2LifecycleManager lifecycleManager)
        {
            _innerDecoder.LifecycleManager(lifecycleManager);
        }

        public virtual bool PrefaceReceived => _innerDecoder.PrefaceReceived;
    }
}
