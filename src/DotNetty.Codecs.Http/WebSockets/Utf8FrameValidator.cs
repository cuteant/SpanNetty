// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using DotNetty.Buffers;
    using DotNetty.Transport.Channels;

    public class Utf8FrameValidator : ChannelHandlerAdapter
    {
        private int _fragmentedFramesCount;
        private readonly Utf8Validator _utf8Validator;

        public Utf8FrameValidator() => _utf8Validator = new Utf8Validator();

        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            if (message is WebSocketFrame frame)
            {
                try
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
                                _fragmentedFramesCount = 0;

                                // Check UTF-8 correctness for this payload
                                _utf8Validator.Check(frame.Content);

                                // This does a second check to make sure UTF-8
                                // correctness for entire text message
                                _utf8Validator.Finish();
                                break;

                            default:
                                _fragmentedFramesCount = 0;

                                if (_utf8Validator.IsChecking)
                                {
                                    // Check UTF-8 correctness for this payload
                                    _utf8Validator.Check(frame.Content);

                                    // This does a second check to make sure UTF-8
                                    // correctness for entire text message
                                    _utf8Validator.Finish();
                                }
                                break;
                        }
                    }
                    else
                    {
                        // Not final frame so we can expect more frames in the
                        // fragmented sequence
                        if (0u >= (uint)_fragmentedFramesCount)
                        {
                            // First text or binary frame for a fragmented set
                            if (frame.Opcode == Opcode.Text)
                            {
                                _utf8Validator.Check(frame.Content);
                            }
                        }
                        else
                        {
                            // Subsequent frames - only check if init frame is text
                            if (_utf8Validator.IsChecking)
                            {
                                _utf8Validator.Check(frame.Content);
                            }
                        }

                        // Increment counter
                        _fragmentedFramesCount++;
                    }
                }
                catch (CorruptedWebSocketFrameException)
                {
                    _ = frame.Release();
                    throw;
                }
            }

            base.ChannelRead(ctx, message);
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception exception)
        {
            if (exception is CorruptedFrameException && ctx.Channel.IsOpen)
            {
                _ = ctx.WriteAndFlushAsync(Unpooled.Empty).CloseOnComplete(ctx.Channel);
            }
            base.ExceptionCaught(ctx, exception);
        }
    }
}
