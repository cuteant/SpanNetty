using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Codecs.Base64;
using DotNetty.Codecs.Http;
using DotNetty.Common;
using DotNetty.Common.Concurrency;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;

namespace DotNetty.Handlers.Proxy
{
    public class HttpProxyHandler : ProxyHandler
    {
        static readonly string PROTOCOL = "http";
        static readonly string AuthBasic = "basic";

        /// <summary>
        /// Wrapper for the HttpClientCodec to prevent it to be removed by other handlers by mistake (for example the WebSocket*Handshaker).
        ///  See:
        /// - https://github.com/netty/netty/issues/5201
        /// - https://github.com/netty/netty/issues/5070
        /// </summary>
        private readonly HttpClientCodecWrapper _codecWrapper = new HttpClientCodecWrapper();
        
        private readonly string _username;
        private readonly string _password;
        private readonly ICharSequence _authorization;
        private readonly HttpHeaders _outboundHeaders;
        private readonly bool _ignoreDefaultPortsInConnectHostHeader;
        
        HttpResponseStatus _status;
        HttpHeaders _inboundHeaders;

        public HttpProxyHandler(EndPoint proxyAddress)
            : this(proxyAddress, null)
        {
        }

        public HttpProxyHandler(EndPoint proxyAddress, HttpHeaders headers)
            : this(proxyAddress, headers, false)
        {
        }

        public HttpProxyHandler(EndPoint proxyAddress, HttpHeaders headers, bool ignoreDefaultPortsInConnectHostHeader)
            : base(proxyAddress)
        {
            _username = null;
            _password = null;
            _authorization = null;
            _outboundHeaders = headers;
            _ignoreDefaultPortsInConnectHostHeader = ignoreDefaultPortsInConnectHostHeader;
        }

        public HttpProxyHandler(EndPoint proxyAddress, string username, string password)
            : this(proxyAddress, username, password, null)
        {
        }

        public HttpProxyHandler(EndPoint proxyAddress, string username, string password, HttpHeaders headers)
            : this(proxyAddress, username, password, headers, false)
        {
        }

        public HttpProxyHandler(
            EndPoint proxyAddress,
            string username,
            string password,
            HttpHeaders headers,
            bool ignoreDefaultPortsInConnectHostHeader)
            : base(proxyAddress)
        {

            if (username is null)
            {
                throw new ArgumentNullException(nameof(username));
            }
            
            if (password is null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            IByteBuffer authz = Unpooled.CopiedBuffer(username + ':' + password, Encoding.UTF8);
            
            IByteBuffer authzBase64;
            try
            {
                authzBase64 = Base64.Encode(authz, false);
            }
            finally
            {
                authz.Release();    
            }

            try
            {
                _authorization = new AsciiString("Basic " + authzBase64.ToString(Encoding.ASCII));
            }
            finally
            {
                authzBase64.Release();
            }

            _outboundHeaders = headers;
            _ignoreDefaultPortsInConnectHostHeader = ignoreDefaultPortsInConnectHostHeader;
        }

        public override string Protocol => PROTOCOL;

        public override string AuthScheme => _authorization != null ? AuthBasic : AuthNone;

        public string Username => _username;

        public string Password => _password;

        protected override void AddCodec(IChannelHandlerContext ctx)
        {
            IChannelPipeline p = ctx.Channel.Pipeline;
            string name = ctx.Name;
            p.AddBefore(name, null, _codecWrapper);
        }

        protected override void RemoveEncoder(IChannelHandlerContext ctx)
        {
            _codecWrapper._codec.RemoveOutboundHandler();
        }

        protected override void RemoveDecoder(IChannelHandlerContext ctx)
        {
            _codecWrapper._codec.RemoveInboundHandler();
        }

        protected override object NewInitialMessage(IChannelHandlerContext ctx)
        {
            if (!TryParseEndpoint(DestinationAddress, out string hostnameString, out int port))
            {
                throw new NotSupportedException($"Endpoint {DestinationAddress} is not supported as http proxy destination");
            }
            
            string url = hostnameString + ":" + port;
            string hostHeader = _ignoreDefaultPortsInConnectHostHeader && (port == 80 || port == 443) ? hostnameString : url;

            IFullHttpRequest req = new DefaultFullHttpRequest(DotNetty.Codecs.Http.HttpVersion.Http11, HttpMethod.Connect, url, Unpooled.Empty, false);

            req.Headers.Set(HttpHeaderNames.Host, hostHeader);

            if (_authorization != null)
            {
                req.Headers.Set(HttpHeaderNames.ProxyAuthorization, _authorization);
            }

            if (_outboundHeaders != null)
            {
                req.Headers.Add(_outboundHeaders);
            }

            return req;
        }

        protected override bool HandleResponse(IChannelHandlerContext ctx, object response)
        {
            if (response is IHttpResponse)
            {
                if (_status != null)
                {
                    throw new HttpProxyConnectException(ExceptionMessage("too many responses"), /*headers=*/ null);
                }

                IHttpResponse res = (IHttpResponse)response;
                _status = res.Status;
                _inboundHeaders = res.Headers;
            }

            bool finished = response is ILastHttpContent;
            if (finished)
            {
                if (_status == null)
                {
                    throw new HttpProxyConnectException(ExceptionMessage("missing response"), _inboundHeaders);
                }

                if (_status.Code != 200)
                {
                    throw new HttpProxyConnectException(ExceptionMessage("status: " + _status), _inboundHeaders);
                }
            }

            return finished;
        }
        
        /// <summary>
        /// Formats the host string of an address so it can be used for computing an HTTP component
        /// such as a URL or a Host header
        /// </summary>
        /// <param name="addr">addr the address</param>
        /// <param name="hostnameString"></param>
        /// <param name="port"></param>
        /// <returns>the formatted String</returns>
        static bool TryParseEndpoint(EndPoint addr, out string hostnameString, out int port) 
        {
            hostnameString = null;
            port = 0;
            
            if (addr is DnsEndPoint eDns)
            {
                hostnameString = eDns.Host;
                port = eDns.Port;
                return true;
            } 
            else if (addr is IPEndPoint eIp)
            {
                port = eIp.Port;
                switch (addr.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        hostnameString = eIp.Address.ToString();
                        return true;
                    
                    case AddressFamily.InterNetworkV6:
                        hostnameString = $"[{eIp.Address}]";
                        return true;
                    
                    default:
                        return false;
                }
            }
            else
            {
                return false;
            }
        }
        
        private sealed class HttpClientCodecWrapper : ChannelDuplexHandler 
        {
            internal readonly HttpClientCodec _codec = new HttpClientCodec();

            public override void HandlerAdded(IChannelHandlerContext context)
                => _codec.HandlerAdded(context);

            public override void HandlerRemoved(IChannelHandlerContext context)
                => _codec.HandlerRemoved(context);

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception) 
                => _codec.ExceptionCaught(context, exception);

            public override void ChannelRegistered(IChannelHandlerContext context)
                => _codec.ChannelRegistered(context);

            public override void ChannelUnregistered(IChannelHandlerContext context)
                => _codec.ChannelUnregistered(context);

            public override void ChannelActive(IChannelHandlerContext context)
                => _codec.ChannelActive(context);

            public override void ChannelInactive(IChannelHandlerContext context)
                => _codec.ChannelInactive(context);

            public override void ChannelRead(IChannelHandlerContext context, object message) 
                => _codec.ChannelRead(context, message);

            public override void ChannelReadComplete(IChannelHandlerContext context)
                => _codec.ChannelReadComplete(context);

            public override void UserEventTriggered(IChannelHandlerContext context, object evt) 
                => _codec.UserEventTriggered(context, evt);

            public override void ChannelWritabilityChanged(IChannelHandlerContext context) 
                => _codec.ChannelWritabilityChanged(context);

            public override Task BindAsync(IChannelHandlerContext context, EndPoint localAddress) 
                => _codec.BindAsync(context, localAddress);

            public override Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress) 
                => _codec.ConnectAsync(context, remoteAddress, localAddress);

            public override void Disconnect(IChannelHandlerContext context, IPromise promise) 
                => _codec.Disconnect(context, promise);

            public override void Close(IChannelHandlerContext context, IPromise promise) 
                => _codec.Close(context, promise);

            public override void Deregister(IChannelHandlerContext context, IPromise promise) 
                => _codec.Deregister(context, promise);

            public override void Read(IChannelHandlerContext context) 
                => _codec.Read(context);

            public override void Write(IChannelHandlerContext context, object message, IPromise promise) 
                => _codec.Write(context, message, promise);

            public override void Flush(IChannelHandlerContext context) 
                => _codec.Flush(context);
        }
    }
}