// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public class Utf8FrameValidator : ChannelHandlerAdapter
    {
        int fragmentedFramesCount;
        readonly Utf8Validator utf8Validator;

        public Utf8FrameValidator() => this.utf8Validator = new Utf8Validator();

        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            if (message is WebSocketFrame frame)
            {
                // Processing for possible fragmented messages for text and binary
                // frames
                if (frame.IsFinalFragment)
                {
                    // Final frame of the sequence
                    switch (frame.Opcode)
                    {
                        case Opcode.Ping:
                            // Apparently ping frames are allowed in the middle of a fragmented message
                            break;

                        // Check text for UTF8 correctness
                        case Opcode.Text:
                            this.fragmentedFramesCount = 0;

                            // Check UTF-8 correctness for this payload
                            CheckUtf8String(ctx, frame.Content, this.utf8Validator);

                            // This does a second check to make sure UTF-8
                            // correctness for entire text message
                            this.utf8Validator.Finish();
                            break;

                        default:
                            this.fragmentedFramesCount = 0;

                            if (this.utf8Validator.IsChecking)
                            {
                                // Check UTF-8 correctness for this payload
                                CheckUtf8String(ctx, frame.Content, this.utf8Validator);

                                // This does a second check to make sure UTF-8
                                // correctness for entire text message
                                this.utf8Validator.Finish();
                            }
                            break;
                    }
                }
                else
                {
                    // Not final frame so we can expect more frames in the
                    // fragmented sequence
                    if (this.fragmentedFramesCount == 0)
                    {
                        // First text or binary frame for a fragmented set
                        if (frame.Opcode == Opcode.Text)
                        {
                            CheckUtf8String(ctx, frame.Content, this.utf8Validator);
                        }
                    }
                    else
                    {
                        // Subsequent frames - only check if init frame is text
                        if (this.utf8Validator.IsChecking)
                        {
                            CheckUtf8String(ctx, frame.Content, this.utf8Validator);
                        }
                    }

                    // Increment counter
                    this.fragmentedFramesCount++;
                }
            }

            base.ChannelRead(ctx, message);
        }

        static void CheckUtf8String(IChannelHandlerContext ctx, IByteBuffer buffer, Utf8Validator utf8Validator)
        {
            try
            {
                utf8Validator.Check(buffer);
            }
            catch (CorruptedFrameException)
            {
                if (ctx.Channel.Active)
                {
#if NET40
                    void closeOnComplete(Task t) => ctx.Channel.CloseAsync();
                    ctx.WriteAndFlushAsync(Unpooled.Empty).ContinueWith(closeOnComplete, TaskContinuationOptions.ExecuteSynchronously);
#else
                    ctx.WriteAndFlushAsync(Unpooled.Empty).ContinueWith(CloseOnComplete, ctx.Channel, TaskContinuationOptions.ExecuteSynchronously);
#endif
                }
            }
        }

        static void CloseOnComplete(Task t, object c) => ((IChannel)c).CloseAsync();
    }
}
