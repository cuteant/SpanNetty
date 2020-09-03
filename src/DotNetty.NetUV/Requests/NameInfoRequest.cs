/*
 * Copyright (c) Johnny Z. All rights reserved.
 *
 *   https://github.com/StormHub/NetUV
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com)
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.NetUV.Requests
{
    using System;
    using System.Net;
    using DotNetty.NetUV.Handles;
    using DotNetty.NetUV.Native;

    [Flags]
    public enum NameInfoFlags
    {
        None = 0,
        NoFullyQualifiedDomainName = 1, // NI_NOFQDN
        NumericHost = 2,                // NI_NUMERICHOST
        NameRequired = 4,               // NI_NAMEREQD
        NumericServiceAddress = 8,      // NI_NUMERICSERV
        Datagram = 16,                  // NI_DGRAM
    }

    public readonly struct NameInfo
    {
        internal NameInfo(string hostName, string service, Exception error)
        {
            HostName = hostName;
            Service = service;
            Error = error;
        }

        public string HostName { get; }

        public string Service { get; }

        public Exception Error { get; }
    }

    public sealed class NameInfoRequest : ScheduleRequest
    {
        internal static readonly uv_getnameinfo_cb NameInfoCallback = (r, s, h, ser) => OnNameInfoCallback(r, s, h, ser);

        private readonly RequestContext _handle;
        private Action<NameInfoRequest, NameInfo> _requestCallback;

        internal unsafe NameInfoRequest(LoopContext loop)
            : base(uv_req_type.UV_GETNAMEINFO)
        {
            if (loop is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.loop); }

            int size = NativeMethods.GetSize(uv_req_type.UV_GETNAMEINFO);
            if ((uint)(size - 1) > SharedConstants.TooBigOrNegative) { ThrowHelper.ThrowArgumentException_Positive(size, ExceptionArgument.size); }

            _handle = new RequestContext(RequestType, size, this);

            // Loop handle
            ((uv_getnameinfo_t*)_handle.Handle)->loop = loop.Handle;
        }

        internal override IntPtr InternalHandle => _handle.Handle;

        public unsafe NameInfoRequest Start(
            IPEndPoint endPoint,
            Action<NameInfoRequest, NameInfo> callback,
            NameInfoFlags flags = NameInfoFlags.None)
        {
            if (endPoint is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.endPoint); }
            if (callback is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback); }

            _handle.Validate();
            _requestCallback = callback;

            IntPtr internalHandle = InternalHandle;
            IntPtr loopHandle = ((uv_getaddrinfo_t*)internalHandle)->loop;
            NativeMethods.GetNameInfo(
                loopHandle,
                internalHandle,
                endPoint,
                flags,
                NameInfoCallback);

            return this;
        }

        public bool TryCancel() => Cancel();

        private void OnNameInfoCallback(int status, string hostname, string service)
        {
            OperationException error = null;
            if ((uint)status > SharedConstants.TooBigOrNegative) // < 0
            {
                error = NativeMethods.CreateError((uv_err_code)status);
            }

            var nameInfo = new NameInfo(hostname, service, error);
            _requestCallback?.Invoke(this, nameInfo);
        }

        private static void OnNameInfoCallback(IntPtr req, int status, string hostname, string service)
        {
            var nameInfo = RequestContext.GetTarget<NameInfoRequest>(req);
            nameInfo?.OnNameInfoCallback(status, hostname, service);
        }

        protected override void Close()
        {
            _requestCallback = null;
            _handle.Dispose();
        }
    }
}
