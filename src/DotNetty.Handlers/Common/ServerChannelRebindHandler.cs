namespace DotNetty.Handlers
{
    using System;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv.Native;

    public sealed class ServerChannelRebindHandler : ChannelHandlerAdapter
    {
        private readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ServerChannelRebindHandler>();

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
            if (Logger.WarnEnabled)
            {
                Logger.Warn($"Channel {context.Channel} caught exception", exception);
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
