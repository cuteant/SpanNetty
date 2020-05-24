namespace DotNetty.Codecs.Http.Tests.WebSockets.Extensions
{
    using DotNetty.Codecs.Http.WebSockets.Extensions;
    using Xunit;

    public sealed class WebSocketExtensionFilterProviderTest
    {
        [Fact]
        public void TestDefaultExtensionFilterProvider()
        {
            IWebSocketExtensionFilterProvider defaultProvider = WebSocketExtensionFilterProvider.Default;
            Assert.NotNull(defaultProvider);

            Assert.Equal(NeverSkipWebSocketExtensionFilter.Instance, defaultProvider.DecoderFilter);
            Assert.Equal(NeverSkipWebSocketExtensionFilter.Instance, defaultProvider.EncoderFilter);
        }
    }
}
