// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http
{
    using System;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public class HttpServerExpectContinueHandler : ChannelHandlerAdapter
    {
        static readonly IFullHttpResponse ExpectationFailed = new DefaultFullHttpResponse(
            HttpVersion.Http11, HttpResponseStatus.ExpectationFailed, Unpooled.Empty);

        static readonly IFullHttpResponse Accept = new DefaultFullHttpResponse(
            HttpVersion.Http11, HttpResponseStatus.Continue, Unpooled.Empty);

        static HttpServerExpectContinueHandler()
        {
            _ = ExpectationFailed.Headers.Set(HttpHeaderNames.ContentLength, HttpHeaderValues.Zero);
            _ = Accept.Headers.Set(HttpHeaderNames.ContentLength, HttpHeaderValues.Zero);
        }

        protected virtual IHttpResponse AcceptMessage(IHttpRequest request) => (IHttpResponse)Accept.RetainedDuplicate();

        protected virtual IHttpResponse RejectResponse(IHttpRequest request) => (IHttpResponse)ExpectationFailed.RetainedDuplicate();

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            if (message is IHttpRequest req && HttpUtil.Is100ContinueExpected(req))
            {
                IHttpResponse accept = this.AcceptMessage(req);

                if (accept is null)
                {
                    // the expectation failed so we refuse the request.
                    IHttpResponse rejection = this.RejectResponse(req);
                    _ = ReferenceCountUtil.Release(message);
                    _ = context.WriteAndFlushAsync(rejection).ContinueWith(CloseOnFailureAction, context, TaskContinuationOptions.ExecuteSynchronously);
                    return;
                }

                _ = context.WriteAndFlushAsync(accept).ContinueWith(CloseOnFailureAction, context, TaskContinuationOptions.ExecuteSynchronously);
                _ = req.Headers.Remove(HttpHeaderNames.Expect);
            }
            _ = context.FireChannelRead(message);
        }

        static readonly Action<Task, object> CloseOnFailureAction = CloseOnFailure;
        static void CloseOnFailure(Task task, object state)
        {
            if (task.IsFaulted)
            {
                var context = (IChannelHandlerContext)state;
                _ = context.CloseAsync();
            }
            //return TaskUtil.Completed;
        }
    }
}
