
namespace DotNetty.Codecs.Http2.Tests
{
    using System;
    using Moq;
    using Xunit;

    public class DecoratingHttp2ConnectionEncoderTest
    {
        [Fact]
        public void TestConsumeReceivedSettingsThrows()
        {
            try
            {
                var encoder = new Mock<IHttp2ConnectionEncoder>();
                DecoratingHttp2ConnectionEncoder decoratingHttp2ConnectionEncoder =
                        new DecoratingHttp2ConnectionEncoder(encoder.Object);
                decoratingHttp2ConnectionEncoder.ConsumeReceivedSettings(Http2Settings.DefaultSettings());
                Assert.False(true);
            }
            catch (Exception exc)
            {
                Assert.IsType<InvalidOperationException>(exc);
            }
        }

        [Fact]
        public void TestConsumeReceivedSettingsDelegate()
        {
            var encoder = new Mock<ITestHttp2ConnectionEncoder>();
            DecoratingHttp2ConnectionEncoder decoratingHttp2ConnectionEncoder =
                    new DecoratingHttp2ConnectionEncoder(encoder.Object);

            Http2Settings settings = Http2Settings.DefaultSettings();
            decoratingHttp2ConnectionEncoder.ConsumeReceivedSettings(Http2Settings.DefaultSettings());
            //encoder.Verify(x => x.ConsumeReceivedSettings(It.Is<Http2Settings>(v => v == settings)), Times.Exactly(1));
            // Http2Settings.DefaultSettings每次返回新实例
            encoder.Verify(x => x.ConsumeReceivedSettings(It.IsAny<Http2Settings>()), Times.Exactly(1));
        }

        public interface ITestHttp2ConnectionEncoder : IHttp2ConnectionEncoder, IHttp2SettingsReceivedConsumer { }
    }
}
