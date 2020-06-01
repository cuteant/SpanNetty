// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System.Collections.Generic;
    using System.Net.Sockets;

    /// <summary>
    /// <see cref="AbstractSocketChannel{TChannel, TUnsafe}"/> base class for <see cref="IChannel"/>s that operate on messages.
    /// </summary>
    public abstract partial class AbstractSocketMessageChannel<TChannel, TUnsafe> : AbstractSocketChannel<TChannel, TUnsafe>
        where TChannel : AbstractSocketMessageChannel<TChannel, TUnsafe>
        where TUnsafe : AbstractSocketMessageChannel<TChannel, TUnsafe>.SocketMessageUnsafe, new()
    {
        /// <summary>
        /// Creates a new <see cref="AbstractSocketMessageChannel{TChannel, TUnsafe}"/> instance.
        /// </summary>
        /// <param name="parent">The parent <see cref="IChannel"/>. Pass <c>null</c> if there's no parent.</param>
        /// <param name="socket">The <see cref="Socket"/> used by the <see cref="IChannel"/> for communication.</param>
        protected AbstractSocketMessageChannel(IChannel parent, Socket socket)
            : base(parent, socket)
        {
        }

        //protected override IChannelUnsafe NewUnsafe() => new SocketMessageUnsafe(this); ## 苦竹 屏蔽 ##

        protected override void DoWrite(ChannelOutboundBuffer input)
        {
            while (true)
            {
                var msg = input.Current;
                if (msg is null)
                {
                    break;
                }
                try
                {
                    var done = false;
                    for (int i = Configuration.WriteSpinCount - 1; i >= 0; i--)
                    {
                        if (DoWriteMessage(msg, input))
                        {
                            done = true;
                            break;
                        }
                    }

                    if (done)
                    {
                        input.Remove();
                    }
                    else
                    {
                        // Did not write all messages.
                        ScheduleMessageWrite(msg);
                        break;
                    }
                }
                catch (SocketException e)
                {
                    if (ContinueOnWriteError)
                    {
                        input.Remove(e);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        protected abstract void ScheduleMessageWrite(object message);

        /// <summary>
        /// Returns <c>true</c> if we should continue the write loop on a write error.
        /// </summary>
        protected virtual bool ContinueOnWriteError => false;

        /// <summary>
        /// Reads messages into the given list and returns the amount which was read.
        /// </summary>
        /// <param name="buf">The list into which message objects should be inserted.</param>
        /// <returns>The number of messages which were read.</returns>
        protected abstract int DoReadMessages(List<object> buf);

        /// <summary>
        /// Writes a message to the underlying <see cref="IChannel"/>.
        /// </summary>
        /// <param name="msg">The message to be written.</param>
        /// <param name="input">The destination channel buffer for the message.</param>
        /// <returns><c>true</c> if the message was successfully written, otherwise <c>false</c>.</returns>
        protected abstract bool DoWriteMessage(object msg, ChannelOutboundBuffer input);
    }
}