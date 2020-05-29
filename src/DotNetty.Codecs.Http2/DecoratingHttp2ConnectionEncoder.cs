// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// A decorator around another <see cref="IHttp2ConnectionEncoder"/> instance.
    /// </summary>
    public class DecoratingHttp2ConnectionEncoder : DecoratingHttp2FrameWriter, IHttp2ConnectionEncoder, IHttp2SettingsReceivedConsumer
    {
        readonly IHttp2ConnectionEncoder _innerEncoder;

        public DecoratingHttp2ConnectionEncoder(IHttp2ConnectionEncoder encoder)
            : base(encoder)
        {
            if (encoder is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.encoder); }

            _innerEncoder = encoder;
        }

        public IHttp2Connection Connection => _innerEncoder.Connection;

        public IHttp2RemoteFlowController FlowController => _innerEncoder.FlowController;

        public IHttp2FrameWriter FrameWriter => _innerEncoder.FrameWriter;

        public Http2Settings PollSentSettings => _innerEncoder.PollSentSettings;

        public virtual void LifecycleManager(IHttp2LifecycleManager lifecycleManager)
        {
            _innerEncoder.LifecycleManager(lifecycleManager);
        }

        public virtual void RemoteSettings(Http2Settings settings)
        {
            _innerEncoder.RemoteSettings(settings);
        }

        public void ConsumeReceivedSettings(Http2Settings settings)
        {
            if (_innerEncoder is IHttp2SettingsReceivedConsumer receivedConsumer)
            {
                receivedConsumer.ConsumeReceivedSettings(settings);
            }
            else
            {
                ThrowHelper.ThrowInvalidOperationException_Delegate_is_not_IHttp2SettingsReceivedConsumer(_innerEncoder);
            }
        }
    }
}
