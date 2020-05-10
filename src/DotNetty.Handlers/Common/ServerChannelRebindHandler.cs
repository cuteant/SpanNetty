#if !NET40
namespace DotNetty.Handlers
{
    using System;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv.Native;
    using Microsoft.Extensions.Logging;
    using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

    public sealed class ServerChannelRebindHandler : ChannelHandlerAdapter
    {
        private readonly ILogger Logger = TraceLogger.GetLogger<ServerChannelRebindHandler>();

        private readonly Action _doBindAction;
        private readonly int _delaySeconds;

        public ServerChannelRebindHandler(Action doBindAction) : this(doBindAction, 2) { }

        public ServerChannelRebindHandler(Action doBindAction, int delaySeconds)
        {
            if (doBindAction is null) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.doBindAction);
            if (delaySeconds <= 0) { delaySeconds = 2; }

            _doBindAction = doBindAction;
            _delaySeconds = delaySeconds;
        }

        /// <inheritdoc />
        public override bool IsSharable => true;

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            if (Logger.IsEnabled(MsLogLevel.Warning))
            {
                Logger.LogWarning(exception, "Channel {0} caught exception", context.Channel);
            }
            switch (exception)
            {
                case SocketException se when se.SocketErrorCode.IsSocketAbortError():
                case OperationException oe when oe.ErrorCode.IsConnectionAbortError():
                case ChannelException ce when (ce.InnerException is OperationException exc && exc.ErrorCode.IsConnectionAbortError()):
                    DoBind();
                    break;

                default:
                    context.FireExceptionCaught(exception);
                    break;
            }
        }

        private async void DoBind()
        {
            await Task.Delay(TimeSpan.FromSeconds(_delaySeconds));
            _doBindAction();
        }
    }
}
#endif
