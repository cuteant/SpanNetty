namespace Http2Helloworld.Client
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Http2;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class Http2SettingsHandler : SimpleChannelInboundHandler2<Http2Settings>
    {
        readonly IPromise promise;

        public Http2SettingsHandler(IPromise promise) => this.promise = promise;

        public async Task AwaitSettings(TimeSpan timeout)
        {
            if (!await TaskUtil.WaitAsync(this.promise.Task, timeout))
            {
                throw new InvalidOperationException("Timed out waiting for settings");
            }
            if (!this.promise.IsSuccess)
            {
                var cause = this.promise.Task.Exception.InnerException;
                throw new Http2RuntimeException(cause.Message, cause);
            }
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, Http2Settings msg)
        {
            this.promise.Complete();

            // Only care about the first settings message
            ctx.Pipeline.Remove(this);
        }
    }
}
