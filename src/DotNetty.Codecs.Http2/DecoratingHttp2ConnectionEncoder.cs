// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// A decorator around another <see cref="IHttp2ConnectionEncoder"/> instance.
    /// </summary>
    public class DecoratingHttp2ConnectionEncoder : DecoratingHttp2FrameWriter, IHttp2ConnectionEncoder
    {
        readonly IHttp2ConnectionEncoder innerEncoder;

        public DecoratingHttp2ConnectionEncoder(IHttp2ConnectionEncoder encoder)
            : base(encoder)
        {
            if (null == encoder) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.encoder); }

            this.innerEncoder = encoder;
        }

        public IHttp2Connection Connection => this.innerEncoder.Connection;

        public IHttp2RemoteFlowController FlowController => this.innerEncoder.FlowController;

        public IHttp2FrameWriter FrameWriter => this.innerEncoder.FrameWriter;

        public Http2Settings PollSentSettings => this.innerEncoder.PollSentSettings;

        public virtual void LifecycleManager(IHttp2LifecycleManager lifecycleManager)
        {
            this.innerEncoder.LifecycleManager(lifecycleManager);
        }

        public virtual void RemoteSettings(Http2Settings settings)
        {
            this.innerEncoder.RemoteSettings(settings);
        }
    }
}
