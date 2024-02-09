namespace Http2Helloworld.Client
{
    using DotNetty.Codecs.Http2;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using System;
    using System.Threading.Tasks;

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

        protected override void ChannelRead0(IChannelHandlerContext context, Http2Settings settings)
        {
            this.promise.Complete();

            // Only care about the first settings message
            context.Pipeline.Remove(this);
        }
    }
}
