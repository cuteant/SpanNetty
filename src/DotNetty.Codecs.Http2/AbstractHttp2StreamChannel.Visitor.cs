namespace DotNetty.Codecs.Http2
{
    partial class AbstractHttp2StreamChannel
    {
        internal static readonly IHttp2FrameStreamVisitor WritableVisitor = new Http2FrameStreamVisitor();

        sealed class Http2FrameStreamVisitor : IHttp2FrameStreamVisitor
        {
            public bool Visit(IHttp2FrameStream stream)
            {
                var childChannel = (AbstractHttp2StreamChannel)
                        ((DefaultHttp2FrameStream)stream)._attachment;
                childChannel.TrySetWritable();
                return true;
            }
        }
    }
}
