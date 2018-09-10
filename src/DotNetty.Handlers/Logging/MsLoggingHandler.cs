// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Logging
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using CuteAnt.Pool;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;
    using Microsoft.Extensions.Logging;
    using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

    /// <summary>A <see cref="IChannelHandler" /> that logs all events using a logging framework.
    /// By default, all events are logged at <tt>DEBUG</tt> level.</summary>
    public class MsLoggingHandler : ChannelHandlerAdapter
    {
        protected readonly ILogger Logger;

        /// <summary>Creates a new instance whose logger name is the fully qualified class
        /// name of the instance with hex dump enabled.</summary>
        public MsLoggingHandler() : this(typeof(LoggingHandler)) { }

        /// <summary>Creates a new instance with the specified logger name.</summary>
        /// <param name="type">the class type to generate the logger for</param>
        public MsLoggingHandler(Type type)
        {
            if (type == null) { ThrowHelper.ThrowNullReferenceException(ExceptionArgument.type); }

            Logger = TraceLogger.GetLogger(type);
        }

        /// <summary>Creates a new instance with the specified logger name using the default log level.</summary>
        /// <param name="name">the name of the class to use for the logger</param>
        public MsLoggingHandler(string name)
        {
            if (name == null) { ThrowHelper.ThrowNullReferenceException(ExceptionArgument.name); }

            Logger = TraceLogger.GetLogger(name);
        }

        /// <inheritdoc />
        public override bool IsSharable => true;

        /// <inheritdoc />
        public override void ChannelRegistered(IChannelHandlerContext ctx)
        {
            if (Logger.IsEnabled(MsLogLevel.Debug))
            {
                Logger.LogDebug("Channel {0} registered", ctx.Channel);
            }
            ctx.FireChannelRegistered();
        }

        /// <inheritdoc />
        public override void ChannelUnregistered(IChannelHandlerContext ctx)
        {
            if (Logger.IsEnabled(MsLogLevel.Debug))
            {
                Logger.LogDebug("Channel {0} unregistered", ctx.Channel);
            }
            ctx.FireChannelUnregistered();
        }

        /// <inheritdoc />
        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            if (Logger.IsEnabled(MsLogLevel.Debug))
            {
                Logger.LogDebug("Channel {0} active", ctx.Channel);
            }
            ctx.FireChannelActive();
        }

        /// <inheritdoc />
        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            if (Logger.IsEnabled(MsLogLevel.Debug))
            {
                Logger.LogDebug("Channel {0} inactive", ctx.Channel);
            }
            ctx.FireChannelInactive();
        }

        /// <inheritdoc />
        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            if (Logger.IsEnabled(MsLogLevel.Warning))
            {
                Logger.LogError(cause, "Channel {0} caught exception", ctx.Channel);
            }
            ctx.FireExceptionCaught(cause);
        }

        /// <inheritdoc />
        public override void UserEventTriggered(IChannelHandlerContext ctx, object evt)
        {
            if (Logger.IsEnabled(MsLogLevel.Debug))
            {
                Logger.LogDebug("Channel {0} triggered user event [{1}]", ctx.Channel, evt);
            }
            ctx.FireUserEventTriggered(evt);
        }

        /// <inheritdoc />
        public override Task BindAsync(IChannelHandlerContext ctx, EndPoint localAddress)
        {
            if (Logger.IsEnabled(MsLogLevel.Information))
            {
                Logger.LogInformation("Channel {0} bind to address {1}", ctx.Channel, localAddress);
            }
            return ctx.BindAsync(localAddress);
        }

        /// <inheritdoc />
        public override Task ConnectAsync(IChannelHandlerContext ctx, EndPoint remoteAddress, EndPoint localAddress)
        {
            if (Logger.IsEnabled(MsLogLevel.Information))
            {
                Logger.LogInformation("Channel {0} connect (remote: {1}, local: {2})", ctx.Channel, remoteAddress, localAddress);
            }
            return ctx.ConnectAsync(remoteAddress, localAddress);
        }

        /// <inheritdoc />
        public override Task DisconnectAsync(IChannelHandlerContext ctx)
        {
            if (Logger.IsEnabled(MsLogLevel.Information))
            {
                Logger.LogInformation("Channel {0} disconnect", ctx.Channel);
            }
            return ctx.DisconnectAsync();
        }

        /// <inheritdoc />
        public override Task CloseAsync(IChannelHandlerContext ctx)
        {
            if (Logger.IsEnabled(MsLogLevel.Information))
            {
                Logger.LogInformation("Channel {0} close", ctx.Channel);
            }
            return ctx.CloseAsync();
        }

        /// <inheritdoc />
        public override Task DeregisterAsync(IChannelHandlerContext ctx)
        {
            if (Logger.IsEnabled(MsLogLevel.Debug))
            {
                Logger.LogDebug("Channel {0} deregister", ctx.Channel);
            }
            return ctx.DeregisterAsync();
        }

        /// <inheritdoc />
        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            if (Logger.IsEnabled(MsLogLevel.Trace))
            {
                Logger.LogTrace("Channel {0} received : {1}", ctx.Channel, FormatMessage(message));
            }
            ctx.FireChannelRead(message);
        }

        /// <inheritdoc />
        public override void ChannelReadComplete(IChannelHandlerContext ctx)
        {
            if (Logger.IsEnabled(MsLogLevel.Trace))
            {
                Logger.LogTrace("Channel {0} receive complete", ctx.Channel);
            }
            ctx.FireChannelReadComplete();
        }

        /// <inheritdoc />
        public override void ChannelWritabilityChanged(IChannelHandlerContext ctx)
        {
            if (Logger.IsEnabled(MsLogLevel.Debug))
            {
                Logger.LogDebug("Channel {0} writability", ctx.Channel);
            }
            ctx.FireChannelWritabilityChanged();
        }

        /// <inheritdoc />
        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            if (Logger.IsEnabled(MsLogLevel.Debug))
            {
                Logger.LogDebug("Channel {0} handler added", ctx.Channel);
            }
        }

        /// <inheritdoc />
        public override void HandlerRemoved(IChannelHandlerContext ctx)
        {
            if (Logger.IsEnabled(MsLogLevel.Debug))
            {
                Logger.LogDebug("Channel {0} handler removed", ctx.Channel);
            }
        }

        /// <inheritdoc />
        public override void Read(IChannelHandlerContext ctx)
        {
            if (Logger.IsEnabled(MsLogLevel.Trace))
            {
                Logger.LogTrace("Channel {0} reading", ctx.Channel);
            }
            ctx.Read();
        }

        /// <inheritdoc />
        public override Task WriteAsync(IChannelHandlerContext ctx, object msg)
        {
            if (Logger.IsEnabled(MsLogLevel.Trace))
            {
                Logger.LogTrace("Channel {0} writing: {1}", ctx.Channel, FormatMessage(msg));
            }
            return ctx.WriteAsync(msg);
        }

        /// <inheritdoc />
        public override void Flush(IChannelHandlerContext ctx)
        {
            if (Logger.IsEnabled(MsLogLevel.Trace))
            {
                Logger.LogTrace("Channel {0} flushing", ctx.Channel);
            }
            ctx.Flush();
        }

        /// <summary>Formats an event and returns the formatted message.</summary>
        /// <param name="arg">the argument of the event</param>
        public static object FormatMessage(object arg)
        {
            switch (arg)
            {
                case IByteBuffer byteBuffer:
                    return FormatByteBuffer(byteBuffer);

                case IByteBufferHolder byteBufferHolder:
                    return FormatByteBufferHolder(byteBufferHolder);

                default:
                    return arg;
            }
        }

        /// <summary>Generates the default log message of the specified event whose argument is a  <see cref="IByteBuffer" />.</summary>
        public static string FormatByteBuffer(IByteBuffer msg)
        {
            int length = msg.ReadableBytes;
            if (length == 0)
            {
                return $"0B";
            }
            else
            {
                int rows = length / 16 + (length % 15 == 0 ? 0 : 1) + 4;
                var buf = StringBuilderManager.Allocate(10 + 1 + 2 + rows * 80);

                buf.Append(length).Append('B').Append('\n');
                ByteBufferUtil.AppendPrettyHexDump(buf, msg);

                return StringBuilderManager.ReturnAndFree(buf);
            }
        }

        /// <summary>Generates the default log message of the specified event whose argument is a <see cref="IByteBufferHolder" />.</summary>
        public static string FormatByteBufferHolder(IByteBufferHolder msg)
        {
            string msgStr = msg.ToString();
            IByteBuffer content = msg.Content;
            int length = content.ReadableBytes;
            if (length == 0)
            {
                return $"{msgStr}, 0B";
            }
            else
            {
                int rows = length / 16 + (length % 15 == 0 ? 0 : 1) + 4;
                var buf = StringBuilderManager.Allocate(msgStr.Length + 2 + 10 + 1 + 2 + rows * 80);

                buf.Append(msgStr).Append(", ").Append(length).Append('B').Append('\n');
                ByteBufferUtil.AppendPrettyHexDump(buf, content);

                return StringBuilderManager.ReturnAndFree(buf);
            }
        }
    }
}