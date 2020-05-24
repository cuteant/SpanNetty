namespace DotNetty.Codecs.Http.WebSockets.Extensions
{
    /// <summary>
    /// Filter that is responsible to skip the evaluation of a certain extension
    /// according to standard.
    /// </summary>
    public interface IWebSocketExtensionFilter
    {
        bool MustSkip(WebSocketFrame frame);
    }
}
