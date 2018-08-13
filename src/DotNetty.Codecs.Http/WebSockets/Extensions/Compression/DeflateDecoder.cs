// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets.Extensions.Compression
{
    using System.Collections.Generic;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Compression;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Embedded;

    abstract class DeflateDecoder : WebSocketExtensionDecoder
    {
        internal static readonly byte[] FrameTail = { 0x00, 0x00, 0xff, 0xff };

        readonly bool noContext;

        EmbeddedChannel decoder;

        protected DeflateDecoder(bool noContext)
        {
            this.noContext = noContext;
        }

        protected abstract bool AppendFrameTail(WebSocketFrame msg);

        protected abstract int NewRsv(WebSocketFrame msg);

        protected override void Decode(IChannelHandlerContext ctx, WebSocketFrame msg, List<object> output)
        {
            if (this.decoder == null)
            {
                //if (!(msg is TextWebSocketFrame) && !(msg is BinaryWebSocketFrame))
                //{
                //    ThrowHelper.ThrowCodecException_UnexpectedInitialFrameType(msg);
                //}
                switch (msg)
                {
                    case TextWebSocketFrame _:
                    case BinaryWebSocketFrame _:
                        break;
                    default:
                        ThrowHelper.ThrowCodecException_UnexpectedInitialFrameType(msg);
                        break;
                }

                this.decoder = new EmbeddedChannel(ZlibCodecFactory.NewZlibDecoder(ZlibWrapper.None));
            }

            bool readable = msg.Content.IsReadable();
            this.decoder.WriteInbound(msg.Content.Retain());
            if (this.AppendFrameTail(msg))
            {
                this.decoder.WriteInbound(Unpooled.WrappedBuffer(FrameTail));
            }

            CompositeByteBuffer compositeUncompressedContent = ctx.Allocator.CompositeDirectBuffer();
            while(true)
            {
                var partUncompressedContent = this.decoder.ReadInbound<IByteBuffer>();
                if (partUncompressedContent == null)
                {
                    break;
                }

                if (!partUncompressedContent.IsReadable())
                {
                    partUncompressedContent.Release();
                    continue;
                }

                compositeUncompressedContent.AddComponent(true, partUncompressedContent);
            }

            // Correctly handle empty frames
            // See https://github.com/netty/netty/issues/4348
            if (readable && compositeUncompressedContent.NumComponents <= 0)
            {
                compositeUncompressedContent.Release();
                ThrowHelper.ThrowCodecException_CannotReadUncompressedBuf();
            }

            if (msg.IsFinalFragment && this.noContext)
            {
                this.Cleanup();
            }

            WebSocketFrame outMsg = null;
            switch (msg)
            {
                case TextWebSocketFrame _:
                    outMsg = new TextWebSocketFrame(msg.IsFinalFragment, this.NewRsv(msg), compositeUncompressedContent);
                    break;
                case BinaryWebSocketFrame _:
                    outMsg = new BinaryWebSocketFrame(msg.IsFinalFragment, this.NewRsv(msg), compositeUncompressedContent);
                    break;
                case ContinuationWebSocketFrame _:
                    outMsg = new ContinuationWebSocketFrame(msg.IsFinalFragment, this.NewRsv(msg), compositeUncompressedContent);
                    break;
                default:
                    ThrowHelper.ThrowCodecException_UnexpectedFrameType(msg);
                    break;
            }
            output.Add(outMsg);
        }

        public override void HandlerRemoved(IChannelHandlerContext ctx)
        {
            this.Cleanup();
            base.HandlerRemoved(ctx);
        }

        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            this.Cleanup();
            base.ChannelInactive(ctx);
        }

        void Cleanup()
        {
            if (this.decoder != null)
            {
                // Clean-up the previous encoder if not cleaned up correctly.
                if (this.decoder.Finish())
                {
                    while(true)
                    {
                        var buf = this.decoder.ReadOutbound<IByteBuffer>();
                        if (buf == null)
                        {
                            break;
                        }
                        // Release the buffer
                        buf.Release();
                    }
                }
                this.decoder = null;
            }
        }
    }
}
