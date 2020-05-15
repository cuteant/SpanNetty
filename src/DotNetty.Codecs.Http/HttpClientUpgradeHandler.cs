// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    // Note HttpObjectAggregator already implements IChannelHandler
    public class HttpClientUpgradeHandler : HttpObjectAggregator
    {
        // User events that are fired to notify about upgrade status.
        public enum UpgradeEvent
        {
            // The Upgrade request was sent to the server.
            UpgradeIssued,

            // The Upgrade to the new protocol was successful.
            UpgradeSuccessful,

            // The Upgrade was unsuccessful due to the server not issuing
            // with a 101 Switching Protocols response.
            UpgradeRejected
        }

        public interface ISourceCodec
        {
            // Removes or disables the encoder of this codec so that the {@link UpgradeCodec} can send an initial greeting
            // (if any).
            void PrepareUpgradeFrom(IChannelHandlerContext ctx);

            // Removes this codec (i.e. all associated handlers) from the pipeline.
            void UpgradeFrom(IChannelHandlerContext ctx);
        }

        public interface IUpgradeCodec
        {
            // Returns the name of the protocol supported by this codec, as indicated by the {@code 'UPGRADE'} header.
            ICharSequence Protocol { get; }

            // Sets any protocol-specific headers required to the upgrade request. Returns the names of
            // all headers that were added. These headers will be used to populate the CONNECTION header.
            ICollection<ICharSequence> SetUpgradeHeaders(IChannelHandlerContext ctx, IHttpRequest upgradeRequest);

            ///
            // Performs an HTTP protocol upgrade from the source codec. This method is responsible for
            // adding all handlers required for the new protocol.
            // 
            // ctx the context for the current handler.
            // upgradeResponse the 101 Switching Protocols response that indicates that the server
            //            has switched to this protocol.
            void UpgradeTo(IChannelHandlerContext ctx, IFullHttpResponse upgradeResponse);
        }

        internal static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<HttpClientUpgradeHandler>();

        readonly ISourceCodec sourceCodec;
        readonly IUpgradeCodec upgradeCodec;
        bool upgradeRequested;

        public HttpClientUpgradeHandler(ISourceCodec sourceCodec, IUpgradeCodec upgradeCodec, int maxContentLength)
            : base(maxContentLength)
        {
            if (sourceCodec is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.sourceCodec); }
            if (upgradeCodec is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.upgradeCodec); }

            this.sourceCodec = sourceCodec;
            this.upgradeCodec = upgradeCodec;
        }

        public override void Write(IChannelHandlerContext context, object message, IPromise promise)
        {
            var request = message as IHttpRequest;
            if (request is null)
            {
                context.WriteAsync(message, promise);
                return;
            }

            if (this.upgradeRequested)
            {
                Util.SafeSetFailure(promise, ThrowHelper.GetInvalidOperationException_Attempting(), Logger);
                return;
            }

            this.upgradeRequested = true;
            this.SetUpgradeRequestHeaders(context, request);

            // Continue writing the request.
            context.WriteAsync(message, promise);

            // Notify that the upgrade request was issued.
            context.FireUserEventTriggered(UpgradeEvent.UpgradeIssued);
            // Now we wait for the next HTTP response to see if we switch protocols.
        }

        protected override void Decode(IChannelHandlerContext context, IHttpObject message, List<object> output)
        {
            IFullHttpResponse response = null;
            try
            {
                if (!this.upgradeRequested)
                {
                    ThrowHelper.ThrowInvalidOperationException_ReadHttpResponse();
                }

                if (message is IHttpResponse rep)
                {
                    if (!HttpResponseStatus.SwitchingProtocols.Equals(rep.Status))
                    {
                        // The server does not support the requested protocol, just remove this handler
                        // and continue processing HTTP.
                        // NOTE: not releasing the response since we're letting it propagate to the
                        // next handler.
                        context.FireUserEventTriggered(UpgradeEvent.UpgradeRejected);
                        RemoveThisHandler(context);
                        context.FireChannelRead(rep);
                        return;
                    }
                }

                if (message is IFullHttpResponse fullRep)
                {
                    response = fullRep;
                    // Need to retain since the base class will release after returning from this method.
                    response.Retain();
                    output.Add(response);
                }
                else
                {
                    // Call the base class to handle the aggregation of the full request.
                    base.Decode(context, message, output);
                    if (0u >= (uint)output.Count)
                    {
                        // The full request hasn't been created yet, still awaiting more data.
                        return;
                    }

                    Debug.Assert(output.Count == 1);
                    response = (IFullHttpResponse)output[0];
                }

                if (response.Headers.TryGet(HttpHeaderNames.Upgrade, out ICharSequence upgradeHeader) && !AsciiString.ContentEqualsIgnoreCase(this.upgradeCodec.Protocol, upgradeHeader))
                {
                    ThrowHelper.ThrowInvalidOperationException_UnexpectedUpgradeProtocol(upgradeHeader);
                }

                // Upgrade to the new protocol.
                this.sourceCodec.PrepareUpgradeFrom(context);
                this.upgradeCodec.UpgradeTo(context, response);

                // Notify that the upgrade to the new protocol completed successfully.
                context.FireUserEventTriggered(UpgradeEvent.UpgradeSuccessful);

                // We guarantee UPGRADE_SUCCESSFUL event will be arrived at the next handler
                // before http2 setting frame and http response.
                this.sourceCodec.UpgradeFrom(context);

                // We switched protocols, so we're done with the upgrade response.
                // Release it and clear it from the output.
                response.Release();
                output.Clear();
                RemoveThisHandler(context);
            }
            catch (Exception exception)
            {
                ReferenceCountUtil.Release(response);
                context.FireExceptionCaught(exception);
                RemoveThisHandler(context);
            }
        }

        static void RemoveThisHandler(IChannelHandlerContext ctx) => ctx.Pipeline.Remove(ctx.Name);

        void SetUpgradeRequestHeaders(IChannelHandlerContext ctx, IHttpRequest request)
        {
            // Set the UPGRADE header on the request.
            request.Headers.Set(HttpHeaderNames.Upgrade, this.upgradeCodec.Protocol);

            // Add all protocol-specific headers to the request.
            var connectionParts = new List<ICharSequence>(2);
            connectionParts.AddRange(this.upgradeCodec.SetUpgradeHeaders(ctx, request));

            // Set the CONNECTION header from the set of all protocol-specific headers that were added.
            var builder = StringBuilderManager.Allocate();
            foreach (ICharSequence part in connectionParts)
            {
                builder.Append(part);
                builder.Append(',');
            }
            builder.Append(HttpHeaderValues.Upgrade);
            request.Headers.Add(HttpHeaderNames.Connection, new StringCharSequence(StringBuilderManager.ReturnAndFree(builder)));
        }
    }
}
