namespace Http2Helloworld.Server
{
    using DotNetty.Codecs.Http2;
    using Microsoft.Extensions.Logging;

    public class HelloWorldHttp2HandlerBuilder : AbstractHttp2ConnectionHandlerBuilder<HelloWorldHttp2Handler, HelloWorldHttp2HandlerBuilder>
    {
        static readonly IHttp2FrameLogger Logger = new Http2FrameMsLogger(LogLevel.Information, typeof(HelloWorldHttp2Handler));

        public HelloWorldHttp2HandlerBuilder()
        {
            this.FrameLogger = Logger;
        }

        protected override HelloWorldHttp2Handler Build(IHttp2ConnectionDecoder decoder, IHttp2ConnectionEncoder encoder, Http2Settings initialSettings)
        {
            HelloWorldHttp2Handler handler = new HelloWorldHttp2Handler(decoder, encoder, initialSettings);
            this.FrameListener = handler;
            return handler;
        }
    }
}
