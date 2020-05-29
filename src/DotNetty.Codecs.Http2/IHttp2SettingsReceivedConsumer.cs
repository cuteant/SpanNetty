namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Provides a Consumer like interface to consume remote settings received but not yet ACKed.
    /// </summary>
    public interface IHttp2SettingsReceivedConsumer
    {
        /// <summary>
        /// Consume the most recently received but not yet ACKed settings.
        /// </summary>
        /// <param name="settings"></param>
        void ConsumeReceivedSettings(Http2Settings settings);
    }
}
