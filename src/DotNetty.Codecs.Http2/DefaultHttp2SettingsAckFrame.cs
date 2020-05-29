using DotNetty.Common.Utilities;

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// The default <see cref="IHttp2SettingsAckFrame"/> implementation.
    /// </summary>
    public sealed class DefaultHttp2SettingsAckFrame : IHttp2SettingsAckFrame
    {
        public static readonly DefaultHttp2SettingsAckFrame Instance = new DefaultHttp2SettingsAckFrame();

        private DefaultHttp2SettingsAckFrame() { }

        public string Name => "SETTINGS(ACK)";

        public override string ToString() => StringUtil.SimpleClassName<DefaultHttp2SettingsAckFrame>();
    }
}
